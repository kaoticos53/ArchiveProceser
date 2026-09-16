---
name: calibrated-benchmarks
description: Write deterministic performance/benchmark tests using inline calibration, median of repeated measurements, and relative thresholds instead of flaky fixed wall-clock limits
---

# Calibrated Benchmarks

Your goal is to help me write performance tests that detect real regressions without ever failing
because the CI machine, a laptop on battery, or parallel test contention made the process slower
than a hardcoded number of milliseconds.

## When to use

Use this skill whenever you are asked to:

- Write or review a test that asserts on elapsed time, throughput, or ops/second
- Fix a flaky performance test (passes locally, fails in CI, or fails only under parallel suites)
- Replace "this must finish in under 2500 ms" style assertions with something deterministic

Do **not** use it for micro-benchmarking meant to produce publishable numbers (use BenchmarkDotNet
or similar for that). This pattern produces *regression detectors*, not measurements.

## The core idea

A fixed wall-clock threshold measures the **machine**, not the **code**. The same code passes on a
28-core desktop and fails on a shared CI runner. A calibrated threshold measures the machine too —
immediately before the assertion, on the same process — so machine speed cancels out:

```
threshold = (time the machine just demonstrated for this exact workload) × safety factor
assert measured time ≤ threshold
```

A real regression (an accidental SQL query in a loop, a lost vectorization, a clone that started
reallocating) slows the workload relative to what the machine just proved it can do. Normal noise
— contention from parallel test collections, JIT warmup variance, thermal throttling — slows the
calibration and the measurement together, so the ratio holds.

## Instructions

### 1. Never assert a fixed wall-clock threshold

Banish patterns like `sw.ElapsedMilliseconds < 2500` or `throughput > 50 MB/s` from test suites.
If you find one in an existing test, convert it to the calibrated pattern.

### 2. Calibrate inline on the exact workload

Before measuring for the assertion, run the **same** workload 3 times with no assertion attached:

- It calibrates the machine's current speed on this exact work (allocation patterns, JIT tiering,
  cache behavior — all included, because it is the same code).
- It doubles as JIT warmup, which is what a cold first run needs most (a cold first run can easily
  be 5-10× slower than steady state on the same hardware).

Calibration must use the full workload — do not calibrate on a "representative subset"; the point
is that regressions in the real workload make it slower too.

### 3. Measure several times and assert on median AND worst

In a parallel test suite, other collections compete for CPU and a single measurement can come back
contaminated (2-3× slower than the others). Run the workload 3 times:

- Assert the **median** — it absorbs the contaminated sample.
- Assert the **worst** against the (generous) calibrated threshold — it catches sustained slowdowns.

Both assertions use the same calibrated threshold. The worst-repetition check is the regression
detector; the median check documents what the suite can expect.

### 4. Use a large relative factor

Default the safety factor to **×40** the calibration. Empirically: normal suite-contention variance
stays under ×3, while real regressions (an O(n) inside an O(1) path, a query per row) blow past ×40
immediately. For workloads with structural variance beyond CPU scheduling — real disk I/O, network —
raise it (×80 is a reasonable starting point) and say why in a comment.

### 5. Make failures diagnosable

When the assertion fails, the message must contain everything needed to act: the calibration value,
the median, the worst repetition, the threshold, and the factor. A bare "expected < 2500 ms, got
3100 ms" forces the next developer to re-derive the whole context. If your test framework supports
diagnostic output (xUnit's `ITestOutputHelper`), also write a report line there — it lands in the
TRX/logs even on success.

### 6. Assert correctness, not just speed

A benchmark test that only checks "fast enough" can pass while producing nothing. Where the
workload produces results (telemetry written to a store, files moved by an engine, hashes computed),
assert the exact expected results. A deterministic correctness assertion is worth more than any
timing assertion.

### 7. Exclude drainage from measured time

If the workload needs draining between runs (flushing a channel, committing a batch), that drainage
is *not* the work under test. Run it between measurements, outside the timed section — but also run
it between calibration iterations, so calibration and measurement have identical shapes.

## What must never be asserted

In a parallel test process, these are **not attributable** to the test that reads them — the shared
heap, GC, and thread pool belong to every concurrent collection. They may be *reported* as
diagnostics, never asserted:

- GC collection counts (`GC.CollectionCount`), heap size deltas, allocation deltas
- Thread-pool statistics
- Process-wide memory (`Process.WorkingSet64` and friends)

Deterministic state owned by the test itself (rows in a test-only SQLite DB, items in a
test-only store) is fine to assert.

## Reference implementation

A battle-tested C#/xUnit skeleton (~60 lines, dependency-free beyond xUnit + any assertion library):

```csharp
public static class CalibratedBenchmark
{
    public const int WarmupIterations = 3;      // calibration == JIT warmup
    public const int RepeatMeasurements = 3;    // median absorbs contaminated samples
    public const double TimeLimitFactor = 40.0; // ×80 for real-disk-I/O workloads

    public static double MeasureAndAssert(
        ITestOutputHelper output, string label, Action work,
        Action? betweenMeasurements = null, double timeLimitFactor = TimeLimitFactor)
    {
        // 1) Calibrate: full workload, no assertion, warms the JIT.
        var calibration = Stopwatch.StartNew();
        for (int i = 0; i < WarmupIterations; i++) { work(); betweenMeasurements?.Invoke(); }
        calibration.Stop();
        double calibrationSeconds = calibration.Elapsed.TotalSeconds / WarmupIterations;

        // 2) Measure with hot JIT.
        var measured = new double[RepeatMeasurements];
        for (int r = 0; r < RepeatMeasurements; r++)
        {
            var sw = Stopwatch.StartNew();
            work();
            sw.Stop();
            measured[r] = sw.Elapsed.TotalSeconds;
            betweenMeasurements?.Invoke();
        }

        // 3) Assert on median AND worst against the calibrated threshold.
        var sorted = measured.OrderBy(x => x).ToArray();
        double median = sorted[sorted.Length / 2];
        double worst = sorted[^1];
        double threshold = calibrationSeconds * timeLimitFactor;

        output.WriteLine($"=== {label} === cal {calibrationSeconds:F3}s, median {median:F3}s, " +
                         $"worst {worst:F3}s, threshold {threshold:F3}s ({timeLimitFactor}x)");

        worst.Should().BeLessThanOrEqualTo(threshold,
            $"{label}: regression makes the workload >{timeLimitFactor}x slower than what this " +
            $"machine just demonstrated (cal {calibrationSeconds:F3}s, median {median:F3}s, " +
            $"worst {worst:F3}s, threshold {threshold:F3}s)");
        return median;
    }
}
```

The pattern ports directly to other ecosystems: `criterion`-style loops in Rust, `t.timeit()` in
Python, `performance.now()` pairs in Node — the three invariants are always inline calibration on
the exact workload, median over repetitions, and a relative threshold with a documented factor.

## Provenance

Extracted from the FileFlow Studio test suite (`FileFlow.Tests/TestHelpers/CalibratedBenchmark.cs`),
where it replaced five fixed-threshold benchmarks whose cold-run margins were as thin as ×1.27 —
intermittent failures waiting for a slow CI runner. Validated under: isolated runs, full parallel
suite, and induced 100% CPU saturation (28 spinner processes) without a single timing failure.
