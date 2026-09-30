using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using FileFlow.App.Uno.Controls;
using FileFlow.App.Uno.Platform;
using FileFlow.Plugin.AI;
using FileFlow.Sdk.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFlow.App.Uno;

/// <summary>
/// El sondeo de la superficie de AJUSTES del host (hito 255), en un <b>modo propio</b>
/// (<c>--selfcheck-settings</c>) porque su medición es invasiva por naturaleza: cambia el tema y el idioma
/// —estado global de la aplicación— y los sondeos del lienzo que ya corrían miden un árbol que no tolera esa
/// mudanza a mitad (medido: dejarlos convivir hace fallar la sonda de selección, la de paneles y la de foco,
/// que no tienen nada que ver con los ajustes). Un proceso limpio para esta superficie es, además, el reparto
/// del fixture UIA: la escena se monta, se asienta y sólo entonces se mide.
///
/// <para><b>Los dos tiempos</b>: el primer callback despliega la superficie y deja visible su sección de
/// apariencia; el layout asienta (el enlace de un control recién hecho visible no está vivo en el mismo
/// callback) y el segundo mide. El veredicto sale por el código de salida y por
/// <c>selfcheck-settings-report.txt</c>, junto al ejecutable.</para>
///
/// <para><b>Quién afirma</b>: este archivo MIDE y afirma —es el dueño de sus dos tiempos y de su veredicto—;
/// la regla que lo fija (modo propio, dos tiempos, informe propio) vive en
/// <c>UnoSettingsSurfaceGuardTests</c>.</para>
/// </summary>
internal static class SelfCheckSettings
{
    /// <summary>
    /// El sondeo de la superficie de AJUSTES (hito 255), en un <b>modo propio</b> (<c>--selfcheck-settings</c>)
    /// porque su medición es invasiva por naturaleza: cambia el tema y el idioma —estado global de la
    /// aplicación— y los sondeos del lienzo que ya corrían miden un árbol que no tolera esa mudanza a mitad
    /// (medido: dejarlos convivir hace fallar la sonda de selección, la de paneles y la de foco, que no tienen
    /// nada que ver con los ajustes). Un proceso limpio para esta superficie es, además, el reparto del
    /// fixture UIA: la escena se monta, se asienta y sólo entonces se mide.
    ///
    /// <para><b>Los dos tiempos</b>: el primer callback despliega la superficie y deja visible su sección de
    /// apariencia; el layout asienta (el enlace de un control recién hecho visible no está vivo en el mismo
    /// callback) y el segundo mide. El veredicto sale por el código de salida y por
    /// <c>selfcheck-settings-report.txt</c>.</para>
    /// </summary>
    public static int Run(Window window, DispatcherQueue dispatcher)
    {
        new Thread(() =>
        {
            var report = new StringBuilder();
            bool ok = false;

            try
            {
                // Primer tiempo: desplegar y dejar la sección de apariencia a la vista.
                bool opened = false;
                var openedGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = SelfCheckTree.Find<SettingsPanel>(window.Content);
                        opened = panel is not null && panel.OpenForMeasurement();
                    }
                    catch (Exception ex)
                    {
                        report.AppendLine("EXCEPCIÓN al desplegar la superficie: " + ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        openedGate.Set();
                    }
                });
                openedGate.Wait(TimeSpan.FromSeconds(10));

                // El asentamiento del layout (el mismo tiempo de espera que el fixture del 245).
                Thread.Sleep(800);

                // Segundo tiempo: medir, restaurar y recoger.
                var measuredGate = new ManualResetEventSlim(false);
                (bool Opened, bool Startup, bool Catalog, bool ThemeApplied, bool ThemeRestored, bool LanguageChanged,
                    bool LanguageRestored, bool PreferenceStored, bool Restored, string Detail) result = default;
                (bool SixSections, string Detail) census = default;
                (bool Ok, string Detail) aiModels = default;
                (bool Ok, string Detail) updates = default;
                string? failure = null;
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = SelfCheckTree.Find<SettingsPanel>(window.Content);
                        if (panel is null)
                        {
                            failure = "la superficie de ajustes no está montada en la ventana";
                            return;
                        }

                        result = panel.ProbeSettingsSurface();
                        census = panel.MeasureSectionCensus();
                        panel.ShowPortedSection("AiModels");
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        measuredGate.Set();
                    }
                });
                measuredGate.Wait(TimeSpan.FromSeconds(30));

                // Las dos SECCIONES portadas en el hito 261 se miden con su propio tiempo: conmutar y medir en
                // el mismo tic mediría un control cuyo enlace todavía no ha corrido —la lección que el sondeo
                // de esta superficie ya trae escrita—, así que cada una se deja visible, se le da el pase de
                // layout y se mide después.
                Thread.Sleep(400);
                var aiGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = SelfCheckTree.Find<SettingsPanel>(window.Content);
                        if (panel is not null)
                        {
                            aiModels = panel.MeasureAiModelsSection();
                            panel.ShowPortedSection("Updates");
                        }
                    }
                    catch (Exception ex)
                    {
                        aiModels = (false, ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        aiGate.Set();
                    }
                });
                aiGate.Wait(TimeSpan.FromSeconds(15));

                Thread.Sleep(400);
                var updatesGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = SelfCheckTree.Find<SettingsPanel>(window.Content);
                        if (panel is not null)
                        {
                            updates = panel.MeasureUpdatesSection();
                            panel.ShowPortedSection("Appearance");
                        }
                    }
                    catch (Exception ex)
                    {
                        updates = (false, ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        updatesGate.Set();
                    }
                });
                updatesGate.Wait(TimeSpan.FromSeconds(15));

                // ── Hito 262: el editor de URLs por modelo, de punta a punta ────────────────────────────────
                //
                // Se pulsa la ACCIÓN DIBUJADA de la fila (su peer de automatización, el mismo canal que un
                // lector de pantalla), se escribe en la CAJA REAL del editor que se abre, se pulsa su botón
                // primario y se comprueba DÓNDE acaba lo escrito: en el almacén del gestor del núcleo, que es
                // el mismo sitio donde lo escribe la versión anterior. Después se deja la configuración del usuario
                // como estaba (instantánea previa, restaurada por las APIs del propio gestor).
                const string probeUrl = "https://probe.invalid/fileflow.model.onnx";
                string urlModelId = string.Empty;
                string urlExpectedName = string.Empty;
                bool urlActionPressed = false;
                bool urlSurfaceUp = false;
                string urlKeyWhileOpen = string.Empty;
                string urlBodyModelName = string.Empty;
                string urlBodyValue = string.Empty;
                string urlCountAfterEdit = "";
                string urlVmValueAfterEdit = string.Empty;
                bool urlClosedBySave = false;
                bool urlChangeLanded = false;
                bool urlRestored = false;
                var originalUrls = new List<string>();
                bool originalWasCustom = false;

                Thread.Sleep(400);
                var urlSeamGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = SelfCheckTree.Find<SettingsPanel>(window.Content);
                        if (panel is not null)
                        {
                            panel.ShowPortedSection("AiModels");
                            urlModelId = panel.FirstModelId ?? string.Empty;
                            if (urlModelId.Length > 0)
                            {
                                originalWasCustom = AiModelManager.HasCustomUrls(urlModelId);
                                originalUrls = [.. AiModelManager.GetConfiguredUrls(urlModelId)];
                                urlExpectedName = AiModelManager.Catalog.TryGetValue(urlModelId, out var info)
                                    ? info.FriendlyName
                                    : urlModelId;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        urlSeamGate.Set();
                    }
                });
                urlSeamGate.Wait(TimeSpan.FromSeconds(15));
                Thread.Sleep(500);

                var urlPressGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var panel = SelfCheckTree.Find<SettingsPanel>(window.Content);
                        Button? action = panel?.FirstModelUrlActionButton();
                        urlActionPressed = action is not null && UnoWindowService.Press(action);
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        urlPressGate.Set();
                    }
                });
                urlPressGate.Wait(TimeSpan.FromSeconds(15));
                urlSurfaceUp = WaitFor(() => UnoWindowService.ActiveWindowKey == DialogKeys.AiModelUrlsConfig, 8000);

                Thread.Sleep(400);
                var urlReadGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        if (UnoWindowService.ActiveDialog?.Content is AiModelUrlsConfigBody body)
                        {
                            urlKeyWhileOpen = UnoWindowService.ActiveWindowKey ?? "—";
                            urlBodyModelName = body.ModelNameShown;
                            urlBodyValue = body.Vm.UrlsText;
                            body.UrlsEditor.Text = probeUrl;   // la caja REAL, en dos sentidos
                            urlCountAfterEdit = body.CountText;
                            urlVmValueAfterEdit = body.Vm.UrlsText;
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        urlReadGate.Set();
                    }
                });
                urlReadGate.Wait(TimeSpan.FromSeconds(15));
                Thread.Sleep(200);

                var urlSaveGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        UnoWindowService.Press(UnoWindowService.ActivePrimaryButton);
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        urlSaveGate.Set();
                    }
                });
                urlSaveGate.Wait(TimeSpan.FromSeconds(15));
                urlClosedBySave = WaitFor(() => UnoWindowService.ActiveWindowKey is null, 8000);

                Thread.Sleep(200);
                var urlVerifyGate = new ManualResetEventSlim(false);
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        if (urlModelId.Length > 0)
                        {
                            urlChangeLanded = AiModelManager.GetConfiguredUrls(urlModelId)
                                .Any(u => string.Equals(u, probeUrl, StringComparison.OrdinalIgnoreCase));

                            // La sonda mide, no configura: se devuelve la instantánea que se tomó antes.
                            if (originalWasCustom)
                            {
                                AiModelManager.SetCustomUrls(urlModelId, originalUrls);
                            }
                            else
                            {
                                AiModelUrlConfig.ResetCustomUrls(urlModelId);
                            }

                            urlRestored = AiModelManager.HasCustomUrls(urlModelId) == originalWasCustom
                                && AiModelManager.GetConfiguredUrls(urlModelId)
                                    .SequenceEqual(originalUrls, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex.GetType().Name + ": " + ex.Message;
                    }
                    finally
                    {
                        urlVerifyGate.Set();
                    }
                });
                urlVerifyGate.Wait(TimeSpan.FromSeconds(15));

                report.AppendLine("=== Sondeo de la superficie de AJUSTES del host Uno (hitos 255, 261 y 262) ===");

                if (failure is not null)
                {
                    report.AppendLine("[FALLO] la medición lanzó: " + failure);
                }
                else
                {
                    void Check(bool condition, string what)
                    {
                        report.AppendLine((condition ? "[OK]   " : "[FALLO]") + " " + what);
                        ok &= condition;
                    }

                    ok = opened && !string.IsNullOrEmpty(result.Detail);
                    Check(result.Opened, "la superficie de ajustes se abre con sus seis secciones");
                    Check(result.Startup, "el tema y el idioma GUARDADOS del usuario son los que están puestos al arrancar");
                    Check(result.Catalog, "los catálogos del view model portable están poblados (temas, idiomas, estrategias de conflicto y niveles de log)");
                    Check(result.ThemeApplied, "el tema elegido en su desplegable llega al gestor de temas y el token del host cambia de color");
                    Check(result.ThemeRestored, "la superficie devuelve el tema que estaba aplicado al entrar");
                    Check(result.LanguageChanged, "el idioma elegido reescribe los textos del marco en caliente (diccionario del host)");
                    Check(result.LanguageRestored, "la superficie devuelve el idioma que estaba al entrar");
                    Check(result.PreferenceStored, "una preferencia editada en su campo llega al servicio de preferencias (write-through)");
                    Check(result.Restored, "la superficie deja las preferencias guardadas del usuario como estaban al entrar");

                    // ── Las dos secciones portadas en el hito 261: Modelos de IA y Actualizaciones ──
                    Check(census.SixSections,
                        "la superficie tiene las SEIS secciones con su rótulo: almacenamiento, apariencia, rendimiento, herramientas, modelos de IA y actualizaciones");

                    // ── El editor de URLs por modelo (hito 262), ejercido de punta a punta ──
                    Check(urlActionPressed && urlSurfaceUp,
                        "la acción «URLs» de la fila del catálogo abre el editor del modelo (fila pulsada=" + urlActionPressed
                        + ", clave abierta=" + urlKeyWhileOpen + ")");
                    Check(urlBodyModelName.Length > 0 && urlBodyModelName == urlExpectedName,
                        "y el editor es la vista del view model portable del modelo elegido ('" + urlBodyModelName
                        + "', esperado '" + urlExpectedName + "')");
                    Check(!string.IsNullOrWhiteSpace(urlBodyValue),
                        "el editor se abre con las URLs que el gestor tiene configuradas para ese modelo ("
                        + urlBodyValue.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length + " URL(s))");
                    Check(urlVmValueAfterEdit.Trim() == probeUrl && urlCountAfterEdit.Contains("1 URL"),
                        "escribir en su caja llega al view model y su recuento se pone al día ('" + urlCountAfterEdit.Trim() + "')");
                    Check(urlClosedBySave && urlChangeLanded,
                        "y pulsar Guardar escribe el cambio DONDE LO ESCRIBE EL ESCRITORIO (el almacén del gestor del núcleo: "
                        + (urlChangeLanded ? "contiene la URL escrita" : "NO contiene la URL escrita") + ")");
                    Check(urlRestored,
                        "la sonda deja la configuración del usuario como estaba (instantánea previa restaurada por el gestor)");
                }

                if (!string.IsNullOrEmpty(result.Detail))
                {
                    report.AppendLine("       [medición] " + result.Detail);
                }

                if (failure is null)
                {
                    report.AppendLine((aiModels.Ok ? "[OK]   " : "[FALLO]") +
                        " la sección «Modelos de IA» enseña el catálogo del gestor del núcleo, su carpeta y su estado");
                    ok &= aiModels.Ok;
                    report.AppendLine((updates.Ok ? "[OK]   " : "[FALLO]") +
                        " la sección «Actualizaciones» enseña la versión, el formato, los canales y la comprobación automática del view model portable");
                    ok &= updates.Ok;
                }

                if (!string.IsNullOrEmpty(census.Detail))
                {
                    report.AppendLine("       [medición] " + census.Detail);
                }

                report.AppendLine("       [medición] " + aiModels.Detail);
                report.AppendLine("       [medición] " + updates.Detail);
                report.AppendLine("       [medición] editor de URLs: modelo=" + (urlModelId.Length > 0 ? urlModelId : "—")
                    + " leído=" + urlBodyValue.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length
                    + " escrito='" + urlVmValueAfterEdit + "'"
                    + " en el gestor=" + (urlChangeLanded ? "sí" : "no")
                    + " restaurado=" + urlRestored);

                report.AppendLine(ok
                    ? "=== RESULTADO: VERIFICADO (la superficie de ajustes escribe y restaura) ==="
                    : "=== RESULTADO: FALLOS (ver [FALLO] arriba) ===");
            }
            catch (Exception ex)
            {
                report.AppendLine("[FALLO] el sondeo de ajustes murió: " + ex.GetType().Name + ": " + ex.Message);
            }

            // La espera de la sonda de ajustes: los pasos de esta superficie se dan en tics del hilo de UI
            // separados (medir un control cuyo enlace no ha corrido mide otra cosa), así que entre uno y el
            // siguiente hay que esperar un EFECTO observable —el modal que se abre, el que se cierra—, no un
            // tiempo fijo a ciegas.
            bool WaitFor(Func<bool> condition, int milliseconds)
            {
                var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
                while (DateTime.UtcNow < until)
                {
                    if (condition())
                    {
                        return true;
                    }

                    Thread.Sleep(50);
                }

                return condition();
            }

            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selfcheck-settings-report.txt"), report.ToString());
            }
            catch
            {
            }

            Console.Out.Flush();
            Console.WriteLine(report.ToString());
            Console.Out.Flush();
            Environment.Exit(ok ? 0 : 1);
        })
        {
            IsBackground = true,
            Name = "RuntimeSettingsProbe"
        }.Start();

        return -1;
    }
}
