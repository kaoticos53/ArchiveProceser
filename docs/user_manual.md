# 📖 User Manual & Node Reference Guide
## **FileFlow Studio v2.0**
*Modular File Automation, Batch Processing & DAG Pipeline Platform*
*Runtime .NET 9 | C# 13 | GNU GPLv3 License | Copyright © 2026 RGLara*

---

## 📑 Table of Contents

1. [Introduction & Architectural Philosophy](#1-introduction--architectural-philosophy)
2. [Visual Editor Core Concepts](#2-visual-editor-core-concepts)
   - [Interactive Node Canvas (Nodify)](#interactive-node-canvas-nodify)
   - [File Item Pipeline Context (`FileItemContext`)](#file-item-pipeline-context-fileitemcontext)
   - [Nested Sub-workflows & Macros (Breadcrumbs)](#nested-sub-workflows--macros-breadcrumbs)
   - [Real-time Connection Telemetry Badges](#real-time-connection-telemetry-badges)
   - [QuickLook Previewer & Node Inspector](#quicklook-previewer--node-inspector)
3. [Execution Modes & Data Safety](#3-execution-modes--data-safety)
   - [Standard Parallel Execution](#standard-parallel-execution)
   - [Virtual Simulation Mode ("Dry Run")](#virtual-simulation-mode-dry-run)
   - [Continuous Real-time Watchdog Mode](#continuous-real-time-watchdog-mode)
   - [Transactional LIFO Rollback System](#transactional-lifo-rollback-system)
   - [Interactive Debugging with Breakpoints](#interactive-debugging-with-breakpoints)
4. [Token & Dynamic Template Engine](#4-token--dynamic-template-engine)
   - [Syntax & Token Domains](#syntax--token-domains)
   - [Comprehensive Token Reference](#comprehensive-token-reference)
5. [Complete Node Catalog (57 DAG Nodes)](#5-complete-node-catalog-57-dag-nodes)
   - [📁 Category 1: FileSystem (Disk I/O & Lifecycle)](#-category-1-filesystem-14-nodes)
   - [🗜️ Category 2: Archives (Compression & Extraction)](#️-category-2-archives-3-nodes)
   - [🖼️ Category 3: Images (Graphic Processing & EXIF)](#️-category-3-images-4-nodes)
   - [🌐 Category 4: Network & Remote Storage (Multi-Protocol Hubs)](#-category-4-network--remote-storage-2-unified-nodes)
   - [🤖 Category 5: AI & Machine Learning (Local ONNX Models)](#-category-5-ai--machine-learning-8-nodes)
   - [📄 Category 6: Documents & PDFs (Splitting, Merging & Extraction)](#-category-6-documents--pdfs-4-nodes)
   - [📊 Category 7: Data & Tabular Files (Excel, CSV, SQLite)](#-category-7-data--tabular-files-3-nodes)
   - [⚙️ Category 8: Logic & Control Flow (Routing & Synchronization)](#️-category-8-logic--control-flow-6-nodes)
   - [🔐 Category 9: Hashing & Security (Cryptography & Deduplication)](#-category-9-hashing--security-3-nodes)
   - [📜 Category 10: Scripting & Extensibility (C# Roslyn & JS)](#-category-10-scripting--extensibility-3-nodes)
   - [🔌 Category 11: Integrations & CLI (External Tools & FFmpeg)](#-category-11-integrations--cli-5-nodes)
6. [Step-by-Step Educational Tutorials](#6-step-by-step-educational-tutorials)
   - [Tutorial A: Automated Photo Organization & Optimization](#tutorial-a-automated-photo-organization--optimization)
   - [Tutorial B: Remote SFTP Ingestion, Unpacking & Consolidated Report](#tutorial-b-remote-sftp-ingestion-unpacking--consolidated-report)
   - [Tutorial C: AI Pipeline with Local OCR & PII Anonymization](#tutorial-c-ai-pipeline-with-local-ocr--pii-anonymization)
7. [Keyboard Shortcuts & Productivity](#7-keyboard-shortcuts--productivity)

---

## 1. Introduction & Architectural Philosophy

**FileFlow Studio** is a modern visual workflow automation engine and batch file transformation platform inspired by state-of-the-art tools like *n8n*, *ComfyUI*, and *Node-RED*, engineered specifically on **.NET 9** and **C# 13**.

### Core Tenets:
- **🛡️ Non-Destructive by Default**: Input files (`OriginalPath`) are never modified or destroyed in place. Source file lifecycle manipulation (preserving, quarantining, recycling, or deleting) is strictly delegated to the specialized `OriginalFileActionNode`.
- **🧩 Decoupled Microkernel Architecture (ADR-006)**: Every plugin domain (`FileFlow.Plugin.*`) is completely autonomous with zero UI dependencies and co-located localization resources (`Strings.resx` / `Strings.es.resx`).
- **⚡ High-Performance Asynchronous Channels**: Multi-threaded parallel processing powered by `System.Threading.Channels` and `TPL Dataflow`, achieving over **82,000 telemetry events/second**.
- **🌐 Universal Network Symmetry**: Unified download and upload hubs covering **HTTP/HTTPS**, **FTP/FTPS**, **SFTP/SSH**, **WebDAV/Nextcloud**, and **SMB/Windows Shares** with reactive parameter visibility.

---

## 2. Visual Editor Core Concepts

### Interactive Node Canvas (Nodify)
Model pipelines by dragging and connecting nodes from the **Toolbox**:
- **Input Ports (Left Edge)**: Receive incoming items (`In`, `BranchA`, `Files`).
- **Output Ports (Right Edge)**: Emit transformed files or conditional branch items (`Out`, `Done`, `Error`, `Matched`, `Unmatched`).
- **Status LED**:
  - ⚪ *Idle*: Waiting for input items.
  - 🔵 *Pulsing Blue (Running)*: Processing files in real time.
  - 🟢 *Green (Completed)*: Successfully finished batch.
  - 🔴 *Red (Faulted)*: Error captured (routed to `Error` port without halting the pipeline).
- **Breakpoint Toggle**: Click the red circle icon to pause execution when a file arrives at the node.

### File Item Pipeline Context (`FileItemContext`)
The fundamental data record travelling across workflow connections:
```csharp
public sealed record FileItemContext
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CurrentPath { get; set; }       // Current file path in the pipeline step
    public string OriginalPath { get; init; }      // Immutable source path
    public long FileSizeBytes { get; set; }        // Exact byte size
    public bool IsDirectory { get; set; }
    public Dictionary<string, object?> Metadata { get; } // Enriched key-value data (EXIF, Hash, OCR, AI)
    public HashSet<string> Tags { get; }          // Quick taxonomy tags
    public List<string> ExecutionLog { get; }     // Audit transformation trail
}
```

### Nested Sub-workflows & Macros (Breadcrumbs)
Encapsulate complex pipelines into reusable composite nodes:
1. Double-click a sub-workflow node to enter its internal editor.
2. The top breadcrumb bar tracks hierarchy: `Root Workflow ❯ Extraction Macro ❯ Sanitization`.
3. Click any parent breadcrumb to commit changes and return to the main canvas.

### Real-time Connection Telemetry Badges
Each wire dynamically renders a live counter badge (e.g., `⚡ 2,450 items`) showing item flow rates and eliminating bottlenecks instantly.

### QuickLook Previewer & Node Inspector
- Press `Spacebar` or click `👁️ QuickLook` on any node or log entry to inspect images, documents, raw text, or metadata tables.
- The **Node Inspector Panel** exposes parameters with rich controls (file pickers, sliders, checkboxes, and reactive protocol dropdowns).

---

## 3. Execution Modes & Data Safety

### Standard Parallel Execution
Press **"▶ Run Workflow"** (`F5`) to process files using hardware-aware adaptive concurrency.

### Virtual Simulation Mode ("Dry Run")
1. Check **"Dry Run"** before execution.
2. The engine computes paths, checks conditions, generates hashes, and resolves templates **without writing, moving, or deleting any files on disk**.
3. Inspect planned operations (`PlannedAction`) in the console prior to committing.

### Continuous Real-time Watchdog Mode
- Toggle **"👁️ Watchdog Mode"** to monitor input folders for new files and trigger processing immediately on disk events.

### Transactional LIFO Rollback System
If you need to revert changes:
1. Click **"↩ Rollback"** on the control bar.
2. Operations (renames, relocations) are undone in reverse order (Last-In, First-Out).

### Interactive Debugging with Breakpoints
- Click **"🐛 Debug Workflow"**.
- Execution halts when an item reaches a breakpoint.
- Use **"Step Into (F10)"** to advance node by node while observing live metadata diffs in the inspector.

---

## 4. Token & Dynamic Template Engine

The `VariableTemplateResolver` engine allows powerful token substitution in filenames, folders, payloads, and CLI parameters.

### Syntax & Token Domains
`{Domain:Key:Modifier}` or `{Variable}`

### Comprehensive Token Reference

| Token | Output Example | Description |
| :--- | :--- | :--- |
| `{FileName}` | `report.pdf` | Full filename with extension |
| `{FileNameNoExt}` | `report` | Filename without extension |
| `{Ext}` | `pdf` | Lowercase extension without dot |
| `{ParentDir}` | `Invoices_2026` | Name of parent directory |
| `{CreationDate:yyyyMMdd}` | `20260903` | Formatted file creation date |
| `{ModifiedDate:yyyy-MM-dd}`| `2026-09-03` | Formatted last modification date |
| `{Now:yyyyMMdd_HHmmss}` | `20260903_205000` | Current system timestamp |
| `{FileSize:MB}` | `14.50` | Formatted file size in Megabytes |
| `{FileSize:KB}` | `14848.0` | Formatted file size in Kilobytes |
| `{Hash:SHA256}` | `e3b0c44298...` | Full SHA-256 cryptographic checksum |
| `{Hash:SHA256:8}` | `e3b0c442` | SHA-256 hash truncated to 8 characters |
| `{Hash:MD5:6}` | `d41d8c` | MD5 hash truncated to 6 characters |
| `{Exif:CameraModel}` | `Nikon Z8` | Camera model from EXIF metadata |
| `{Exif:DateTimeOriginal}` | `2026:08:15 14:20:00` | Original image capture timestamp |
| `{Ocr:Text}` | `Invoice #1024` | Text extracted via local OCR engine |
| `{Env:USERPROFILE}` | `C:\Users\User` | Operating system environment variable |
| `{Meta:CustomKey}` | `DynamicValue` | Metadata injected by upstream nodes |

---

## 5. Complete Node Catalog (57 DAG Nodes)

---

### 📁 Category 1: FileSystem (14 Nodes)

1. **`FolderSourceNode`**: Discovers and emits files with extension filters, recursive scanning, and real-time folder watching.
2. **`DestinationSinkNode`**: Consolidates processed files with collision strategies (`Overwrite`, `Skip`, `RenameIncremental`).
3. **`AdvancedRenamerNode`**: Batch file renaming with token templates, character sanitization, and live preview.
4. **`FileRelocatorNode`**: Moves, copies, or hard-links files to calculated destinations with optional SHA-256 validation.
5. **`SafeRecycleDeleteNode`**: Safe recycling by sending files to the Windows Recycle Bin (`SHFileOperationW`).
6. **`OriginalFileActionNode`**: Centralized lifecycle management for source files (`Keep`, `MoveToRecycleBin`, `MoveToQuarantine`).
7. **`OperationReportNode`**: Generates interactive reports in `HTML`, `Markdown`, `Text`, `JSON`, or `CSV` format with full audit history.
8. **`DirectoryInspectorNode`**: Classifies folder structure (single archive vs. mixed contents).
9. **`EmptyDirectoryCleanerNode`**: Cleans up leftover empty directory trees after moving or unpacking files.
10. **`DocumentProcessorNode`**: Unified metadata extractor for `.pdf`, `.docx`, `.txt`, `.csv`, and `.json` documents.
11. **`VariableInjectorNode`**: Injects custom variables and calculated expressions into file metadata.
12. **`LogOutputNode`**: Emits custom formatted diagnostic log messages to the console.
13. **`FileAttributeNode`**: Modifies file system attributes (ReadOnly, Hidden, Archive, Timestamps).
14. **`PathSplitterNode`**: Decomposes paths into discrete token variables.

---

### 🗜️ Category 2: Archives (3 Nodes)

1. **`SmartUnpackNode`**: Universal archive extraction (ZIP, RAR, 7Z, TAR, GZ) with redundant folder flattening and Zip Slip protection.
2. **`ArchiveCompressorNode`**: Compresses single or batch files into ZIP, 7Z, TAR, or GZ archives with configurable compression algorithms.
3. **`ArchiveFilterNode`**: Detects and isolates split multi-volume archives (`.part1.rar`, `.z01`).

---

### 🖼️ Category 3: Images (4 Nodes)

1. **`ImageOptimizerNode`**: Resizes, optimizes, and converts images to WebP, JPEG, or PNG, reporting exact byte savings.
2. **`ExifMetadataNode`**: Extracts EXIF camera metadata (make, model, GPS coordinates, date taken).
3. **`ImageWatermarkNode`**: Applies text or image watermarks with configurable positioning, opacity, and scaling.
4. **`ImageMetadataStripperNode`**: Strips sensitive GPS and camera metadata prior to publishing.

---

### 🌐 Category 4: Network & Remote Storage (2 Unified Nodes)

1. **`NetworkDownloadNode`** *(Universal Download Hub)*:
   - Supports 5 symmetric protocols: **HTTP/HTTPS**, **FTP/FTPS**, **SFTP (SSH)**, **WebDAV (Nextcloud/ownCloud)**, and **SMB (Windows Network Shares / NAS)**.
   - Dynamic parameter visibility adapting in real time to the selected protocol.
2. **`NetworkUploadNode`** *(Universal Upload & Transfer Hub)*:
   - Supports 5 symmetric protocols: **HTTP POST/PUT**, **FTP/FTPS**, **SFTP (SSH)**, **WebDAV**, and **SMB**.
   - Resilient transfers with auto-retry, password/private key SSH authentication, and remote directory creation.

---

### 🤖 Category 5: AI & Machine Learning (8 Nodes)

1. **`SmartImageClassifierNode`**: Offline image classification using local ONNX neural networks (e.g. ResNet, MobileNet).
2. **`PromptObjectDetectorNode`**: Text-prompted zero-shot object detection via YOLO-World or Grounding DINO in ONNX.
3. **`LocalOcrNode`**: Fast offline optical character recognition for scanned receipts, documents, and images.
4. **`WhisperAudioTranscriberNode`**: High-accuracy local speech-to-text audio/video transcription with OpenAI Whisper ONNX.
5. **`FaceDetectorNode`**: Detects faces in images, calculating bounding boxes and face counts.
6. **`ZeroShotSemanticSearchNode`**: Semantic text classification and document routing without fine-tuning.
7. **`PiiAnonymizerNode`**: Detects and redacts personally identifiable information (SSN, credit cards, names, emails) using NER models.
8. **`SuperResolutionUpscalerNode`**: Deep learning image upscaling and super-resolution enhancement.

---

### 📄 Category 6: Documents & PDFs (4 Nodes)

1. **`PdfMergeNode`**: Merges multiple PDF files into a single master document.
2. **`PdfSplitNode`**: Splits multi-page PDF documents into single pages or specified page ranges.
3. **`PdfTextExtractorNode`**: Extracts structured text and layout data from PDF files using `PdfPig`.
4. **`PdfMetadataNode`**: Reads and updates standard PDF metadata (Title, Author, Subject, Keywords).

---

### 📊 Category 7: Data & Tabular Files (3 Nodes)

1. **`ExcelReaderNode`**: High-performance streaming reader for `.xlsx` / `.xls` spreadsheets via `MiniExcel`.
2. **`CsvProcessorNode`**: Parses, filters, converts, and formats delimited text files (CSV/TSV).
3. **`DataLookupNode`**: Instant $O(1)$ in-memory relational lookup against master reference tables.

---

### ⚙️ Category 8: Logic & Control Flow (6 Nodes)

1. **`SwitchCaseNode`**: Multi-way conditional branch router matching extensions, file sizes, or metadata.
2. **`ExpressionFilterNode`**: Boolean predicate filter with logical operators (`Equal`, `Contains`, `GreaterThan`, `RegexMatch`).
3. **`BatchBufferNode`**: Gathers items until a batch size threshold or timeout is reached before emitting.
4. **`ThrottleDelayNode`**: Rate limits emission rate to prevent overwhelming downstream disk or network endpoints.
5. **`ForkJoinBarrierNode`**: Synchronizes concurrent branches, waiting for all sibling items to arrive before releasing.
6. **`VariableInjectorNode`**: Injects static and dynamic variables into item context.

---

### 🔐 Category 9: Hashing & Security (3 Nodes)

1. **`HashCalculatorNode`**: Computes cryptographic checksums (SHA-256, SHA-512, MD5, SHA-1, xxHash).
2. **`DeduplicationFilterNode`**: Filters duplicate files in real time by comparing memory-cached hash signatures.
3. **`ChecksumVerifierNode`**: Validates files against `.sha256` checksum files or expected metadata hashes.

---

### 📜 Category 10: Scripting & Extensibility (3 Nodes)

1. **`CustomScriptNode`**: Custom logic execution with dual support for **C# (Roslyn JIT)** and **JavaScript (Jint sandbox)**.
2. **`ScriptStudio`**: Integrated IDE with syntax highlighting, live testing console, and `.ffscript` templates.
3. **`PythonScriptNode`**: Dispatches items through external Python environments for specialized data science workloads.

---

### 🔌 Category 11: Integrations & CLI (5 Nodes)

1. **`CliExecutionNode`**: Executes command-line processes (PowerShell, CMD, binaries) capturing stdout/stderr into metadata.
2. **`WebhookNotificationNode`**: Dispatches HTTP POST/PUT alerts with custom JSON payloads to Discord, Slack, or webhook endpoints.
3. **`MediaTranscoderNode`**: Transcodes video and audio streams using integrated FFmpeg profiles.
4. **`SqliteDatabaseSinkNode`**: Inserts structured audit and file records into local SQLite database tables.
5. **`MessageQueuePublisherNode`**: Publishes event metadata to message queues (RabbitMQ, MQTT).

---

## 6. Step-by-Step Educational Tutorials

### Tutorial A: Automated Photo Organization & Optimization
**Goal**: Scan an SD card, extract EXIF data, convert images to modern WebP, and organize them into folders by year, month, and camera model.

1. **`FolderSourceNode`**:
   - `SourcePath`: `E:\DCIM\100NIKON`
   - `ExtensionFilter`: `*.jpg, *.jpeg, *.png`
2. Connect `Out` to **`ExifMetadataNode`** (extracts camera make, model, and date).
3. Connect `Out` to **`ImageOptimizerNode`**:
   - `TargetFormat`: `WebP`
   - `Quality`: `85`
4. Connect `Out` to **`DestinationSinkNode`**:
   - `DestinationRoot`: `D:\Organized_Photos\{Exif:Make}_{Exif:CameraModel}\{CreationDate:yyyy}\{CreationDate:MM}`
   - `ConflictStrategy`: `AutoIncrement`
5. Connect `Done` to **`OriginalFileActionNode`**:
   - `ActionType`: `MoveToQuarantine` (safely backs up source files).

---

### Tutorial B: Remote SFTP Ingestion, Unpacking & Consolidated Report
**Goal**: Download daily backups from an SSH server, extract contents, discard duplicate files, and produce an interactive HTML audit report.

1. **`NetworkDownloadNode`**:
   - `Protocol`: `SFTP`
   - `Host`: `backup.company.com` | `Username`: `backup_operator`
   - `RemoteFilePath`: `/var/backups/nightly.zip`
   - `DestinationFolder`: `C:\Temp\Ingest`
2. Connect `Out` to **`SmartUnpackNode`** (extracts all nested files).
3. Connect `Out` to **`HashCalculatorNode`** (`Algorithm: SHA256`).
4. Connect `Out` to **`DeduplicationFilterNode`**:
   - `Unique` output $\rightarrow$ Connect to **`DestinationSinkNode`** (`DestinationRoot: D:\Clean_Store`).
   - `Duplicate` output $\rightarrow$ Connect to **`SafeRecycleDeleteNode`** (sends to Windows Recycle Bin).
5. Connect `Unique` to **`OperationReportNode`**:
   - `ReportFormat`: `HTML`
   - `AutoOpenReport`: `true`

---

### Tutorial C: AI Pipeline with Local OCR & PII Anonymization
**Goal**: Ingest confidential scanned invoices, perform local OCR, and anonymize sensitive personal data before archiving.

1. **`FolderSourceNode`** (`SourcePath: C:\Incoming_Invoices`).
2. Connect to **`LocalOcrNode`** (extracts text without sending data to cloud APIs).
3. Connect to **`PiiAnonymizerNode`** (masks credit card numbers, IDs, and names).
4. Connect to **`DestinationSinkNode`** (`DestinationRoot: C:\Archived_Invoices`).

---

## 7. Keyboard Shortcuts & Productivity

| Shortcut | Action |
| :--- | :--- |
| `F5` | Run current workflow |
| `Ctrl + F5` | Run in Virtual Simulation Mode (Dry Run) |
| `F10` | Step Into next node during debugging |
| `Ctrl + Z` | Undo last canvas action |
| `Ctrl + Y` | Redo last canvas action |
| `Ctrl + S` | Save current workflow (`.json`) |
| `Ctrl + O` | Open workflow file |
| `Ctrl + N` | Create new blank workflow |
| `Spacebar` | Open QuickLook preview for selected item |
| `Delete` | Delete selected node or connection |
| `Ctrl + F` | Search nodes in Toolbox |
| `Ctrl + Mouse Wheel` | Zoom in / Zoom out on canvas |

---

*Official FileFlow Studio Documentation. Distributed under GNU General Public License v3.0.*
