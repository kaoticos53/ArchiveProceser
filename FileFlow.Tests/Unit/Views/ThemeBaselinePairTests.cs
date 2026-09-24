using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Views;

/// <summary>
/// Guardia de las <b>parejas de tema</b> del directorio de líneas base: cada línea base en claro tiene que
/// ser realmente clara y no un duplicado de la oscura.
///
/// <para>La comparación contra línea base no distingue este caso: si alguien regenera una superficie en claro
/// mientras la captura usa el tema oscuro (basta con cambiar el preset que recibe el test, o copiar el PNG),
/// la imagen <i>pasa</i> — es coherente consigo misma — y lo que queda congelado es una mentira: una segunda
/// copia del tema oscuro con nombre de tema claro, que además tapa cualquier regresión del claro porque nunca
/// se vería un claro de verdad. Es exactamente el estado del que veníamos: no había líneas base del producto en
/// claro y ningún test podía notarlo.</para>
///
/// <para>Las dos comprobaciones se hacen <b>sobre los archivos</b> (no capturan nada), así que no dependen de
/// las fuentes ni del compositor y tardan milisegundos. Van en la colección de capturas por el mismo motivo
/// por el que van los tests que las escriben: el directorio de líneas base es el estado compartido de esa
/// colección, y aquí se lee lo que allí se escribe.</para>
/// </summary>
[Collection(VisualSnapshotsCollection.Name)]
public class ThemeBaselinePairTests
{
    /// <summary>
    /// Cuánto más luminosa tiene que ser la versión clara. Medido: la pareja más apretada del repositorio va de
    /// 54 (oscuro) a 207 (claro) — 153 puntos de diferencia —, así que 60 deja margen para temas nuevos más
    /// suaves sin permitir que un duplicado pase por claro.
    /// </summary>
    private const double MinimumLuminanceGain = 60;

    /// <summary>
    /// Qué proporción de píxeles tiene que cambiar entre los dos temas. Medido: la pareja que menos cambia lo
    /// hace en el 85 % de la imagen; un duplicado cambiaría el 0 %. El umbral está lejos de los dos.
    /// </summary>
    private const double MinimumDifferingRatio = 0.6;

    [Fact]
    public void EveryLightBaseline_ShouldHaveADarkTwin()
    {
        var orphans = LightBaselines()
            .Where(name => !File.Exists(VisualSnapshot.BaselinePath(DarkTwinOf(name))))
            .Select(name => $"{name} → falta {DarkTwinOf(name)}.png")
            .ToArray();

        orphans.Should().BeEmpty(
            "cada superficie capturada en claro debe capturarse también en oscuro (el test que la captura en " +
            "claro y el que la captura en oscuro se añaden y se retiran juntos): {0}",
            string.Join("; ", orphans));
    }

    [Fact]
    public void EveryLightBaseline_ShouldBeLighterThanItsDarkTwin()
    {
        var findings = new List<string>();

        foreach (string name in LightBaselines())
        {
            string darkName = DarkTwinOf(name);
            string darkPath = VisualSnapshot.BaselinePath(darkName);

            if (!File.Exists(darkPath))
            {
                continue; // Lo nombra la guardia de parejas; aquí sólo se comparan.
            }

            byte[] light = File.ReadAllBytes(VisualSnapshot.BaselinePath(name));
            byte[] dark = File.ReadAllBytes(darkPath);

            double lightLuminance = VisualSnapshot.MeanLuminance(light);
            double darkLuminance = VisualSnapshot.MeanLuminance(dark);
            double differing = VisualSnapshot.DifferingPixelRatio(dark, light);

            if (lightLuminance - darkLuminance < MinimumLuminanceGain)
            {
                findings.Add(
                    $"'{name}' es casi tan oscura como '{darkName}': luminancia {lightLuminance:F1} frente a " +
                    $"{darkLuminance:F1} (se exige {MinimumLuminanceGain} puntos más). Lo más probable es que se " +
                    "haya regenerado con el preset oscuro.");
            }

            if (differing < MinimumDifferingRatio)
            {
                findings.Add(
                    $"'{name}' sólo cambia el {differing:P1} de sus píxeles respecto de '{darkName}' (se exige " +
                    $"{MinimumDifferingRatio:P0}): parece una copia de la captura oscura.");
            }
        }

        findings.Should().BeEmpty("{0}", string.Join(" | ", findings));
    }

    private static IEnumerable<string> LightBaselines() =>
        Directory.GetFiles(VisualSnapshot.BaselineDirectoryPath, "*-light.png")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .OrderBy(name => name, StringComparer.Ordinal);

    private static string DarkTwinOf(string lightName) => lightName[..^"-light".Length] + "-dark";
}
