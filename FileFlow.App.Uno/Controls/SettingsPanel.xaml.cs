using System;
using System.Linq;
using System.Text;
using FileFlow.App.Services;
using FileFlow.App.Themes;
using FileFlow.App.ViewModels;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileFlow.App.Uno.Controls;

/// <summary>
/// La superficie de <b>ajustes</b> del host Uno — la que el usuario abre desde la cabecera de la ventana.
///
/// <para><b>Qué es y qué no</b>: la vista es del host (XAML de WinUI), pero <b>todo</b> lo que se edita,
/// se valida y se persiste vive en el <see cref="WorkflowSettingsViewModel"/> portable — el MISMO que
/// alimenta la ventana de ajustes de la versión anterior. Esta clase no guarda preferencias por su cuenta: cada
/// control escribe una propiedad del view model y el pie ejecuta sus comandos canónicos
/// (<c>SaveSettingsCommand</c> → <c>UpdatePreferences</c> + <c>SetCulture</c> + <c>SetThemeById</c>;
/// <c>CancelSettingsCommand</c> → el <c>RequestClose</c> del propio VM).</para>
///
/// <para><b>Las secciones portadas</b>: Almacenamiento y Rutas, Apariencia e Idioma (tema + idioma, la
/// sección que el cajón de la versión anterior también ofrece), Rendimiento y Ejecución y Herramientas Externas.
/// Las dos secciones de la ventana de la versión anterior que NO se portan —<i>Actualizaciones</i> y <i>Modelos de
/// IA</i>— quedan declaradas en el plan de la rebanada 5: la primera necesita el servicio de actualización
/// y su diálogo modal; la segunda, el gestor de modelos. «Lo que no llega queda declarado, nunca
/// fingido».</para>
///
/// <para><b>Los pickers</b>: los comandos <c>Browse*</c> del view model usan el contrato SÍNCRONO del
/// <c>IFileDialogService</c>, que en este host devuelve <c>null</c> cuando lo llama el hilo de UI (medido y
/// declarado en <c>UnoFileDialogService</c>). El host usa aquí la variante asíncrona real —el picker se
/// encola sin bloquear el hilo— y escribe el resultado en la MISMA propiedad que el VM habría escrito.</para>
/// </summary>
public sealed partial class SettingsPanel : UserControl
{
    private readonly IFileDialogService _fileDialog;
    private WorkflowSettingsViewModel? _vm;

    public SettingsPanel()
    {
        InitializeComponent();

        _fileDialog = App.Services.GetRequiredService<IFileDialogService>();

        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        ApplyLocalization();
    }

    /// <summary>
    /// El view model portable que la superficie consume. Se data el <c>DataContext</c> con él: los
    /// <c>{Binding}</c> del XAML apuntan a las propiedades del VM del núcleo (tema, idioma, rutas,
    /// rendimiento), sin una copia local de ninguna.
    /// </summary>
    public WorkflowSettingsViewModel? Vm
    {
        get => _vm;
        set
        {
            if (ReferenceEquals(_vm, value))
            {
                return;
            }

            if (_vm is not null)
            {
                _vm.RequestClose -= OnVmRequestClose;
            }

            _vm = value;

            if (_vm is not null)
            {
                // El cierre lo pide el VM (el mismo contrato que cierra la ventana de la versión anterior):
                // guardar cierra, cancelar cierra. La vista no decide por su cuenta cuándo se acabó.
                _vm.RequestClose += OnVmRequestClose;
            }

            DataContext = _vm;
        }
    }

    /// <summary>
    /// Construye el view model de la superficie con el reparto de dependencias de la versión anterior: el que ya
    /// viva en el contenedor del núcleo manda, y si no, los mismos singulares que resuelve la ventana de
    /// ajustes de la versión anterior (preferencias, temas, localización y plantillas) más los adaptadores de
    /// diálogo de ESTE host — el explorador asíncrono que aquí sí funciona desde el hilo de UI.
    /// </summary>
    public static WorkflowSettingsViewModel CreateViewModel(IServiceProvider services) =>
        services.GetService<WorkflowSettingsViewModel>() ?? new WorkflowSettingsViewModel(
            UserPreferencesService.Instance,
            FileFlow.Core.Services.ExternalToolsService.Instance,
            ThemeManager.Instance,
            LocalizationManager.Instance,
            services.GetRequiredService<IFileDialogService>(),
            services.GetRequiredService<IDialogService>(),
            services.GetService<AiModelManagerViewModel>() ?? new AiModelManagerViewModel());

    /// <summary>Si la superficie está desplegada (lo que el sondeo y la guardia observan).</summary>
    public bool IsOpen => OverlayRoot.Visibility == Visibility.Visible;

    /// <summary>Los cuerpos de las seis secciones: todas materializadas, la conmutación es de visibilidad.</summary>
    private ScrollViewer[] SectionPanes =>
        [StoragePane, AppearancePane, PerformancePane, ToolsPane, AiModelsPane, UpdatesPane];

    /// <summary>Los botones del conmutador de secciones (uno por cuerpo, en el mismo orden).</summary>
    private RadioButton[] SectionButtons =>
        [StorageTabButton, AppearanceTabButton, PerformanceTabButton, ToolsTabButton, AiModelsTabButton, UpdatesTabButton];

    /// <summary>
    /// Conmuta la sección visible. Es el mismo camino que recorre el botón del conmutador: el sondeo lo usa
    /// para medir las secciones que no arrancan seleccionadas.
    /// </summary>
    internal void ShowSection(string section)
    {
        // La sección de modelos de IA enseña la carpeta del gestor y el estado de cada modelo: se refresca al
        // entrar, que es cuando el dato puede haber cambiado (el usuario pudo borrar o descargar desde el
        // asistente de descarga, que es la MISMA pieza del núcleo).
        if (string.Equals(section, "AiModels", StringComparison.Ordinal))
        {
            RefreshAiModelsSection();
        }

        for (int i = 0; i < SectionPanes.Length; i++)
        {
            bool active = string.Equals(SectionButtons[i].Tag as string, section, StringComparison.Ordinal);
            SectionPanes[i].Visibility = active ? Visibility.Visible : Visibility.Collapsed;

            if (active)
            {
                SectionButtons[i].IsChecked = true;
            }

            UpdateTabButtonStyle(SectionButtons[i], active);
        }
    }

    private static readonly SolidColorBrush TransparentBrush = new(Microsoft.UI.Colors.Transparent);

    private static Brush? ThemeBrush(string key)
        => Application.Current.Resources.TryGetValue(key, out object? value) && value is Brush brush
            ? brush
            : null;

    private void UpdateTabButtonStyle(RadioButton button, bool isSelected)
    {
        if (isSelected)
        {
            button.Background = ThemeBrush("CanvasSurfaceBrush");
            button.Foreground = ThemeBrush("CanvasAccentGlowBrush");
            button.BorderBrush = ThemeBrush("CanvasBorderBrush");
            button.BorderThickness = new Thickness(1);
            button.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }
        else
        {
            button.Background = TransparentBrush;
            button.Foreground = ThemeBrush("CanvasSecondaryBrush");
            button.BorderBrush = TransparentBrush;
            button.BorderThickness = new Thickness(1);
            button.FontWeight = Microsoft.UI.Text.FontWeights.Medium;
        }
    }

    private void OnTabPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: not true } rb)
        {
            rb.Foreground = ThemeBrush("CanvasTextBrush");
        }
    }

    private void OnTabPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: not true } rb)
        {
            rb.Foreground = ThemeBrush("CanvasSecondaryBrush");
        }
    }

    private void OnSectionClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string section })
        {
            ShowSection(section);
        }
    }

    /// <summary>
    /// Despliega la superficie con las preferencias <b>vigentes</b>: <c>Initialize</c> es el del núcleo, el
    /// mismo que corre al abrir la ventana de la versión anterior (lee las preferencias y traduce las guardadas a
    /// las opciones que los desplegables sí ofrecen).
    /// </summary>
    public void Open(string? currentGlobalOutputDir = null)
    {
        _vm?.Initialize(currentGlobalOutputDir);
        SyncNumericFields();
        ShowSection("Storage");
        StatusText.Text = string.Empty;
        OverlayRoot.Visibility = Visibility.Visible;
    }

    /// <summary>Recoge la superficie sin escribir nada (el camino de cancelar y del velo).</summary>
    public void Close() => OverlayRoot.Visibility = Visibility.Collapsed;

    /// <summary>El cierre pedido por el propio view model (guardar o cancelar).</summary>
    private void OnVmRequestClose(bool saved)
    {
        var loc = LocalizationManager.Instance;
        StatusText.Text = saved
            ? loc.GetString("Uno_Settings_Saved", "Ajustes guardados.")
            : loc.GetString("Uno_Settings_Discarded", "Cambios descartados.");
        Close();
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Manos de la superficie: los mismos comandos del VM que ejecuta la versión anterior
    // ───────────────────────────────────────────────────────────────────────────────

    private void OnSaveClicked(object sender, RoutedEventArgs e) => _vm?.SaveSettingsCommand.Execute(null);

    private void OnCancelClicked(object sender, RoutedEventArgs e) => _vm?.CancelSettingsCommand.Execute(null);

    private void OnCloseClicked(object sender, RoutedEventArgs e) => _vm?.CancelSettingsCommand.Execute(null);

    private void OnScrimTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) =>
        _vm?.CancelSettingsCommand.Execute(null);

    // ───────────────────────────────────────────────────────────────────────────────
    // Sección 5 (modelos de IA): las órdenes del GESTOR, que es una sola pieza del núcleo
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La sección enseña la carpeta del gestor y DEJA PREGUNTADO su estado, sin descargar nada por su cuenta:
    /// los modelos instalados se leen del disco (el gestor ya lo hizo al construirse), que es exactamente lo
    /// que hace la pestaña de la versión anterior al abrirse.
    /// </summary>
    private void RefreshAiModelsSection()
    {
        var manager = _vm?.AiModelManager;
        if (manager is null)
        {
            return;
        }

        AiModelsDirText.Text = manager.ModelsDirectory;
        manager.RefreshStatusCommand.Execute(null);
        AiModelsEmptyLabel.Visibility = manager.HasModels ? Visibility.Collapsed : Visibility.Visible;
        AiModelsErrorBox.Visibility = manager.HasDownloadError ? Visibility.Visible : Visibility.Collapsed;
        AiModelsErrorText.Text = manager.LastDownloadErrorMessage ?? string.Empty;
    }

    private void OnAiModelsRefreshClicked(object sender, RoutedEventArgs e) => RefreshAiModelsSection();

    private async void OnAiModelsDownloadMissingClicked(object sender, RoutedEventArgs e)
    {
        if (_vm?.AiModelManager is { } manager)
        {
            await manager.DownloadMissingModelsCommand.ExecuteAsync(null);
            RefreshAiModelsSection();
        }
    }

    private void OnAiModelsOpenFolderClicked(object sender, RoutedEventArgs e) =>
        _vm?.AiModelManager.OpenModelsFolderCommand.Execute(null);

    private async void OnAiModelRowActionClicked(object sender, RoutedEventArgs e)
    {
        if (_vm?.AiModelManager is not { } manager || sender is not FrameworkElement { DataContext: var item })
        {
            return;
        }

        string? action = (sender as FrameworkElement)?.Tag as string;
        if (item is FileFlow.App.ViewModels.AiModelItemViewModel model)
        {
            switch (action)
            {
                case "delete":
                    manager.DeleteModelCommand.Execute(model);
                    RefreshAiModelsSection();
                    break;

                case "urls":
                    // La orden canónica del gestor: pide el diálogo por el catálogo del host, que ahora SÍ lo
                    // sirve (hito 262). La vista no edita URLs por su cuenta; el cambio lo escribe el view
                    // model del editor en el mismo sitio que la versión anterior.
                    await manager.ConfigureUrlsCommand.ExecuteAsync(model);
                    RefreshAiModelsSection();
                    break;

                default:
                    await manager.DownloadModelCommand.ExecuteAsync(model);
                    RefreshAiModelsSection();
                    break;
            }
        }
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Sección 6 (actualizaciones): la comprobación es la del núcleo, en su servicio
    // ───────────────────────────────────────────────────────────────────────────────

    private async void OnCheckUpdatesNowClicked(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
        {
            await _vm.CheckForUpdatesNowCommand.ExecuteAsync(null);
        }
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Manos del resto de la superficie
    // ───────────────────────────────────────────────────────────────────────────────

    private void OnCleanTempNowClicked(object sender, RoutedEventArgs e) =>
        _vm?.CleanTemporaryFilesNowCommand.Execute(null);

    private void OnClearCheckpointsClicked(object sender, RoutedEventArgs e) =>
        _vm?.ClearCheckpointsCommand.Execute(null);

    private async void OnAutoDetectClicked(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
        {
            await _vm.AutoDetectToolsCommand.ExecuteAsync(null);
        }
    }

    private async void OnBrowseFolderClicked(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not FrameworkElement { Tag: string target })
        {
            return;
        }

        string title = LocalizationManager.Instance.GetString(TargetLabelKey(target), target);
        string? folder = await _fileDialog.ShowFolderBrowserDialogAsync(title);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            AssignTarget(_vm, target, folder);
        }
    }

    private async void OnBrowseFileClicked(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not FrameworkElement { Tag: string target })
        {
            return;
        }

        string title = LocalizationManager.Instance.GetString(TargetLabelKey(target), target);
        string? file = await _fileDialog.ShowOpenFileDialogAsync(title, "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(file))
        {
            AssignTarget(_vm, target, file);
        }
    }

    /// <summary>
    /// La clave localizada de cada destino del explorador. Es la tabla que ata el <c>Tag</c> de un botón con
    /// el texto que el panel ya pinta: una fila nueva (un parámetro con explorar) entra por aquí y por la
    /// asignación, y la guardia la ve.
    /// </summary>
    private static string TargetLabelKey(string target) => target switch
    {
        "GlobalOutputDir" => "Uno_Settings_GlobalOutputDirTitle",
        "TempWorkingDir" => "Uno_Settings_TempWorkingDirTitle",
        "FfmpegPath" => "Uno_Settings_Ffmpeg",
        "FfprobePath" => "Uno_Settings_Ffprobe",
        "SevenZipPath" => "Uno_Settings_SevenZip",
        "PythonPath" => "Uno_Settings_Python",
        _ => "Uno_Settings_Open",
    };

    /// <summary>El write-back del explorador: la MISMA propiedad que el comando del VM habría escrito.</summary>
    private static void AssignTarget(WorkflowSettingsViewModel vm, string target, string value)
    {
        switch (target)
        {
            case "GlobalOutputDir":
                vm.GlobalOutputDir = value;
                break;
            case "TempWorkingDir":
                vm.TempWorkingDir = value;
                break;
            case "FfmpegPath":
                vm.FfmpegPath = value;
                break;
            case "FfprobePath":
                vm.FfprobePath = value;
                break;
            case "SevenZipPath":
                vm.SevenZipPath = value;
                break;
            case "PythonPath":
                vm.PythonPath = value;
                break;
        }
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Campos numéricos (NumberBox.Value es double; el VM guarda int)
    // ───────────────────────────────────────────────────────────────────────────────

    private void SyncNumericFields()
    {
        if (_vm is null)
        {
            return;
        }

        AutoSaveIntervalBox.Value = _vm.AutoSaveIntervalMinutes;
        MaxLogEntriesBox.Value = _vm.MaxLogEntries;
        MaxCpuThreadsBox.Value = _vm.MaxParallelThreads;
    }

    private void OnAutoSaveIntervalChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_vm is not null && !double.IsNaN(args.NewValue))
        {
            int value = (int)args.NewValue;
            if (_vm.AutoSaveIntervalMinutes != value)
            {
                _vm.AutoSaveIntervalMinutes = value;
            }
        }
    }

    private void OnMaxLogEntriesChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_vm is not null && !double.IsNaN(args.NewValue))
        {
            int value = (int)args.NewValue;
            if (_vm.MaxLogEntries != value)
            {
                _vm.MaxLogEntries = value;
            }
        }
    }

    private void OnMaxCpuThreadsChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_vm is not null && !double.IsNaN(args.NewValue))
        {
            int value = (int)args.NewValue;
            if (_vm.MaxParallelThreads != value)
            {
                _vm.MaxParallelThreads = value;
            }
        }
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // Localización en caliente
    // ───────────────────────────────────────────────────────────────────────────────

    private void OnLanguageChanged(object? sender, System.Globalization.CultureInfo e) => ApplyLocalization();

    /// <summary>
    /// Aplica los textos vigentes. Es el patrón del resto del marco del host: las cadenas salen de
    /// <see cref="LocalizationManager"/> (diccionario del host, claves <c>Uno_*</c> en los dos idiomas) y se
    /// reescriben al cambiar el idioma — el idioma del panel incluido, que es lo que hace visible la
    /// conmutación.
    /// </summary>
    private void ApplyLocalization()
    {
        var loc = LocalizationManager.Instance;

        HeaderTitle.Text = loc.GetString("Uno_Settings_HeaderTitle", "Ajustes de FileFlow Studio");
        HeaderSubtitle.Text = loc.GetString("Uno_Settings_HeaderSubtitle", "Preferencias globales persistentes de almacenamiento, apariencia, rendimiento y herramientas externas.");
        CloseButton.Content = loc.GetString("Uno_Settings_Close", "Cerrar los ajustes");

        StorageTabButton.Content = loc.GetString("Uno_Settings_TabStorage", "Almacenamiento y Rutas");
        AppearanceTabButton.Content = loc.GetString("Uno_Settings_TabAppearance", "Apariencia e Idioma");
        PerformanceTabButton.Content = loc.GetString("Uno_Settings_TabPerformance", "Rendimiento y Ejecución");
        ToolsTabButton.Content = loc.GetString("Uno_Settings_TabTools", "Herramientas Externas");
        AiModelsTabButton.Content = loc.GetString("Settings_TabAiModels", "Modelos de IA");
        UpdatesTabButton.Content = loc.GetString("Settings_TabUpdates", "Actualizaciones");

        GlobalOutputLabel.Text = loc.GetString("Uno_Settings_GlobalOutputDirTitle", "Ruta de salida global por defecto");
        TempDirLabel.Text = loc.GetString("Uno_Settings_TempWorkingDirTitle", "Directorio de trabajo temporal");
        ConflictLabel.Text = loc.GetString("Uno_Settings_ConflictStrategyTitle", "Estrategia de conflicto de archivos por defecto");
        AutoSaveCheck.Content = loc.GetString("Uno_Settings_EnableAutoSave", "Activar el guardado automático del proyecto");
        AutoSaveIntervalLabel.Text = loc.GetString("Uno_Settings_AutoSaveInterval", "Intervalo de autoguardado (minutos):");
        AutoCleanCheck.Content = loc.GetString("Uno_Settings_AutoCleanIntermediate", "Limpiar los archivos temporales intermedios al terminar un flujo");
        CleanStaleCheck.Content = loc.GetString("Uno_Settings_CleanStaleTemp", "Purgar los directorios temporales huérfanos al arrancar la aplicación");
        CleanTempNowButton.Content = loc.GetString("Uno_Settings_CleanTempNow", "Limpiar el espacio temporal ahora");

        LanguageLabel.Text = loc.GetString("Uno_Settings_LanguageLabel", "Idioma:");
        ThemeLabel.Text = loc.GetString("Uno_Settings_DefaultThemeTitle", "Tema visual por defecto");
        CompactToolboxCheck.Content = loc.GetString("Uno_Settings_CompactToolbox", "Arrancar el cajón de nodos en modo lista compacta");
        AutoScrollCheck.Content = loc.GetString("Uno_Settings_AutoScrollConsole", "Desplazar la consola automáticamente al recibir mensajes");
        MaxLogEntriesLabel.Text = loc.GetString("Uno_Settings_MaxLogEntries", "Máximo de líneas en la consola:");
        LinesUnlimitedLabel.Text = loc.GetString("Uno_Settings_LinesUnlimited", "líneas (0 = sin límite)");

        MaxCpuThreadsLabel.Text = loc.GetString("Uno_Settings_MaxCpuThreads", "Hilos de CPU máximos:");
        ThreadsLabel.Text = loc.GetString("Uno_Settings_ThreadsLabel", "hilos");
        DryRunCheck.Content = loc.GetString("Uno_Settings_DryRunDefault", "Arrancar siempre con el modo simulación (dry-run) activado");
        LogLevelLabel.Text = loc.GetString("Uno_Settings_LogLevelTitle", "Nivel mínimo de registro");
        CheckpointingCheck.Content = loc.GetString("Uno_Settings_Checkpointing", "Activar los puntos de control para reanudar flujos interrumpidos");
        ClearCheckpointsButton.Content = loc.GetString("Uno_Settings_ClearCheckpoints", "Borrar los puntos de control");
        AutoUnloadAiCheck.Content = loc.GetString("Uno_Settings_AutoUnloadAi", "Descargar los modelos de IA al terminar el flujo");

        ToolsTitleLabel.Text = loc.GetString("Uno_Settings_ToolsTitle", "Rutas de los ejecutables del sistema");
        FfmpegLabel.Text = loc.GetString("Uno_Settings_Ffmpeg", "FFmpeg (transcodificación de vídeo / audio)");
        FfprobeLabel.Text = loc.GetString("Uno_Settings_Ffprobe", "FFprobe (inspección de metadatos multimedia)");
        SevenZipLabel.Text = loc.GetString("Uno_Settings_SevenZip", "7-Zip (archivos ZIP / 7z / RAR)");
        PythonLabel.Text = loc.GetString("Uno_Settings_Python", "Python (scripts y automatización de CLI)");
        AutoDetectButton.Content = loc.GetString("Uno_Settings_AutoDetect", "Detectar herramientas");

        // Sección 5: modelos de IA (los textos son los de la versión anterior: la misma superficie, contada igual).
        AiModelsTitleLabel.Text = loc.GetString("AiModelManager_HeaderTitle", "Gestión de Modelos de IA");
        AiModelsSubtitleLabel.Text = loc.GetString("AiModelManager_HeaderSubtitle", "Descarga y gestiona los modelos de IA locales necesarios para los nodos inteligentes.");
        AiModelsDirLabel.Text = loc.GetString("Settings_AiModels_DirLabel", "Carpeta de modelos");
        AiModelsRefreshButton.Content = loc.GetString("Settings_AiModels_Refresh", "Actualizar estado");
        AiModelsDownloadMissingButton.Content = loc.GetString("Settings_AiModels_DownloadAll", "Descargar todos los modelos");
        AiModelsOpenFolderButton.Content = loc.GetString("Settings_AiModels_OpenDir", "Abrir carpeta");
        AiModelsEmptyLabel.Text = loc.GetString("AiModelManager_EmptyState", "No hay modelos en el catálogo.");

        // Sección 6: actualizaciones.
        UpdatesTitleLabel.Text = loc.GetString("Settings_UpdatesTitle", "Actualizaciones");
        UpdatesDescLabel.Text = loc.GetString("Settings_UpdatesDesc", "Configura cómo se comprueban las nuevas versiones de FileFlow Studio.");
        UpdateCurrentVersionLabel.Text = loc.GetString("Update_CurrentVersionLabel", "Versión actual");
        UpdatePackagingLabel.Text = loc.GetString("Update_PackagingFormatLabel", "Formato del paquete");
        UpdateLastCheckLabel.Text = loc.GetString("Settings_LastUpdateCheck", "Última comprobación");
        UpdateChannelLabel.Text = loc.GetString("Settings_UpdateChannelTitle", "Canal de actualizaciones");
        AutoCheckUpdatesCheck.Content = loc.GetString("Settings_AutoCheckUpdates", "Buscar actualizaciones automáticamente al iniciar");
        CheckUpdatesButton.Content = loc.GetString("Settings_CheckUpdatesNowBtn", "Buscar actualizaciones ahora");

        RefreshAiModelsSection();

        string browse = loc.GetString("Uno_Settings_Browse", "Examinar");
        BrowseGlobalOutputButton.Content = browse;
        BrowseTempDirButton.Content = browse;
        BrowseFfmpegButton.Content = browse;
        BrowseFfprobeButton.Content = browse;
        BrowseSevenZipButton.Content = browse;
        BrowsePythonButton.Content = browse;

        CancelButton.Content = loc.GetString("Uno_Settings_Cancel", "Cancelar");
        SaveButton.Content = loc.GetString("Uno_Settings_Save", "Guardar ajustes");
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // El sondeo en runtime de la superficie (lo corre RuntimeSelfCheck en su modo propio)
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Primer tiempo del sondeo: despliega la superficie con las preferencias <b>vigentes</b> y deja visible
    /// la sección de apariencia (idioma y tema), que es la que el segundo tiempo mide.
    ///
    /// <para><b>Por qué en dos tiempos</b>: el enlace de un control sólo está vivo después de un pase de
    /// layout con su sección visible — mover la visibilidad y leer el control en el MISMO callback mide un
    /// control que todavía no ha enlazado (medido: el desplegable no escribía su valor y el sondeo cantaba un
    /// fallo que no era del producto). El corredor del sondeo da este primer tiempo, deja asentar el layout y
    /// pide después la medición: el mismo reparto del fixture UIA del hito 245, que también monta la escena y
    /// la deja asentar antes de que nadie la mida.</para>
    /// </summary>
    internal bool OpenForMeasurement()
    {
        Open();
        ShowSection("Appearance");
        return IsOpen;
    }

    /// <summary>
    /// El censo de secciones del host (hito 261): las SEIS con su rótulo. Se mide sin depender de ningún
    /// enlace (son los cuerpos y los botones de la vista), que es lo único que se puede afirmar en el mismo
    /// tic en que se conmuta.
    /// </summary>
    internal (bool SixSections, string Detail) MeasureSectionCensus()
    {
        // Un botón puede EXISTIR y no poder pulsarse: con la tira en una sola línea, los seis rótulos
        // pedían más ancho que el panel de 800 y la pestaña «Actualizaciones» nacía recortada contra el
        // borde —caja vacía, fuera del alcance del ratón y sin scroll ni rueda que la alcanzara— mientras
        // este censo, que sólo miraba la declaración, seguía diciendo «seis». Desde el hito 272 el censo
        // exige que cada botón tenga CAJA propia DENTRO del panel.
        var boxes = SectionButtons.Select(BoxInPanel).ToArray();
        bool six = SectionPanes.Length == 6 && SectionButtons.Length == 6
            && SectionButtons.All(b => b.Content is string text && !string.IsNullOrWhiteSpace(text))
            && boxes.All(box => box.Width > 0 && box.Height > 0
                && box.Left >= -0.5 && box.Top >= -0.5
                && box.Right <= PanelHost.Width + 0.5 && box.Bottom <= PanelHost.Height + 0.5);

        return (six, "secciones=" + SectionPanes.Length + " / botones=" + SectionButtons.Length
            + " rótulos=" + string.Join(" | ", SectionButtons.Select(b => b.Content as string))
            + " cajas=" + string.Join(" ", boxes.Select(b => $"{b.Width:F0}x{b.Height:F0}@({b.Left:F0},{b.Top:F0})")));
    }

    /// <summary>La caja de un elemento en el sistema del PANEL (el que tiene ancho y alto fijos).</summary>
    private Windows.Foundation.Rect BoxInPanel(FrameworkElement element) =>
        element.TransformToVisual(PanelHost).TransformBounds(
            new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));

    /// <summary>
    /// Despliega una de las secciones portadas en el hito 261 para poder medirla en un tiempo aparte.
    ///
    /// <para><b>Por qué VOLVER a desplegar la superficie</b>: el sondeo de la superficie la recoge al terminar
    /// (mide, no configura), y con el panel recogido los controles de una sección no llegan a cargarse
    /// —medido: la lista de modelos tenía el view model puesto y cero filas, y el desplegable de canales, cero
    /// opciones—. Es la misma lección de siempre: un enlace no está vivo hasta que el control está cargado, y un
    /// control recogido no lo está. Aquí se despliega SIN volver a inicializar (el sondeo ya midió eso).</para>
    /// </summary>
    internal void ShowPortedSection(string section)
    {
        OverlayRoot.Visibility = Visibility.Visible;
        ShowSection(section);
    }

    /// <summary>
    /// La sección <b>Modelos de IA</b> (hito 261), medida DESPUÉS del pase de layout —la lista sólo tiene
    /// sus filas cuando el enlace ha corrido—: el catálogo del gestor del núcleo, su carpeta y su estado.
    /// </summary>
    /// <summary>
    /// El id del primer modelo del catálogo: el que la sonda usa para ejercer la edición de URLs (hito 262).
    /// </summary>
    internal string? FirstModelId =>
        _vm?.AiModelManager.Models.Count > 0 ? _vm.AiModelManager.Models[0].ModelId : null;

    /// <summary>
    /// El botón REAL de la acción «URLs» de la primera fila del catálogo, tal y como está dibujado. La sonda
    /// lo pulsa por su peer de automatización —el mismo canal que un lector de pantalla— en vez de llamar al
    /// comando por su cuenta: lo que se mide es la fila que el usuario ve, no un camino que la sonda invente.
    /// </summary>
    internal Button? FirstModelUrlActionButton()
    {
        DependencyObject? container = AiModelsList.ContainerFromIndex(0);
        return container is null ? null : FindUrlAction(container);
    }

    private static Button? FindUrlAction(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is Button { Tag: "urls" } button)
            {
                return button;
            }

            Button? found = FindUrlAction(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    internal (bool Ok, string Detail) MeasureAiModelsSection()
    {
        var manager = _vm?.AiModelManager;
        int modelsInList = AiModelsList.Items.Count;

        bool ok = AiModelsPane.Visibility == Visibility.Visible
            && manager is not null
            && manager.Models.Count > 0
            && modelsInList == manager.Models.Count
            && ReferenceEquals(AiModelsList.ItemsSource, manager.Models)
            && !string.IsNullOrWhiteSpace(AiModelsDirText.Text)
            && !string.IsNullOrWhiteSpace(AiModelsEmptyLabel.Text);

        return (ok, "iA: modelos=" + (manager?.Models.Count ?? -1)
            + " en lista=" + modelsInList
            + " carpeta='" + AiModelsDirText.Text + "'"
            + " resumen='" + (manager?.InstalledSummary ?? "—") + "'"
            + " | carga: lista=" + AiModelsList.IsLoaded
            + " dc=" + (AiModelsList.DataContext?.GetType().Name ?? "null")
            + " fuente=" + (AiModelsList.ItemsSource?.GetType().Name ?? "null")
            + " vm=" + (_vm?.GetType().Name ?? "null"));
    }

    /// <summary>
    /// La sección <b>Actualizaciones</b> (hito 261): versión, formato del paquete, canales y la comprobación
    /// automática, todos del view model portable (los mismos datos que enseña la pestaña de la versión anterior).
    /// No lanza ninguna comprobación de red: mide lo que la sección ENSEÑA.
    /// </summary>
    internal (bool Ok, string Detail) MeasureUpdatesSection()
    {
        var vm = _vm;
        int channelsInCombo = UpdateChannelCombo.Items.Count;

        bool ok = UpdatesPane.Visibility == Visibility.Visible
            && vm is not null
            && !string.IsNullOrWhiteSpace(vm.CurrentVersionDisplay)
            && UpdateCurrentVersionText.Text == vm.CurrentVersionDisplay
            && vm.UpdateChannels.Count > 0
            && channelsInCombo == vm.UpdateChannels.Count
            && AutoCheckUpdatesCheck.IsChecked == vm.AutoCheckForUpdates
            && !string.IsNullOrWhiteSpace(UpdateChannelLabel.Text);

        return (ok, "actualizaciones: versión='" + UpdateCurrentVersionText.Text
            + "' vm='" + (vm?.CurrentVersionDisplay ?? "—")
            + "' canales=" + channelsInCombo
            + " automática=" + AutoCheckUpdatesCheck.IsChecked
            + " formato='" + (vm?.PackagingFormatDisplay ?? "—")
            + "' última='" + (vm?.LastUpdateCheckDisplay ?? "—") + "'"
            + " | carga: texto=" + UpdateCurrentVersionText.IsLoaded
            + " combo=" + UpdateChannelCombo.IsLoaded
            + " fuente=" + (UpdateChannelCombo.ItemsSource?.GetType().Name ?? "null")
            + " dc=" + (UpdateChannelCombo.DataContext?.GetType().Name ?? "null"));
    }

    /// <summary>
    /// Segundo tiempo del sondeo: mide la superficie por sus caminos reales, sin puntero. Comprueba que los
    /// catálogos del view model están poblados; mueve el <b>tema</b> desde su desplegable y el guardado del pie
    /// (el id llega al gestor de temas y el token que pinta el lienzo cambia de color); conmuta el
    /// <b>idioma</b> con su selector (los textos del marco tienen que reescribirse de verdad, por el
    /// diccionario del host, no por el fallback del código) y escribe una <b>preferencia</b> desde su campo
    /// numérico (el write-through llega al servicio de preferencias). Todo se restaura por la misma superficie
    /// y la superficie se recoge: el sondeo mide, no configura.
    /// </summary>
    internal (bool Opened, bool Startup, bool Catalog, bool ThemeApplied, bool ThemeRestored, bool LanguageChanged,
        bool LanguageRestored, bool PreferenceStored, bool Restored, string Detail) ProbeSettingsSurface()
    {
        var vm = _vm;
        if (vm is null)
        {
            return (false, false, false, false, false, false, false, false, false,
                "la superficie no tiene view model");
        }

        var loc = LocalizationManager.Instance;
        var themes = ThemeManager.Instance;
        var prefs = UserPreferencesService.Instance;

        // Lo APLICADO (lo que se ve) y lo GUARDADO (lo que el usuario tenía escrito) son dos cosas distintas:
        // el tema que el host tiene aplicado puede no ser la preferencia guardada, y el sondeo tiene que
        // devolver las dos tal cual estaban — la preferencia del usuario no se toca ni se "normaliza".
        string appliedThemeId = themes.CurrentThemeId;
        string storedTheme = prefs.Preferences.ActiveTheme;
        string storedLanguage = prefs.Preferences.Language;
        int storedThreads = prefs.Preferences.MaxParallelThreads;

        var detail = new StringBuilder();
        bool opened = false, startup = false, catalog = false, themeApplied = false, themeRestored = false;
        bool languageChanged = false, languageRestored = false, stored = false, restored = false;

        try
        {
            // 0. La superficie está desplegada y con sus cuatro secciones (el primer tiempo la abrió).
            opened = IsOpen && SectionPanes.Length == 6 && SectionButtons.Length == 6;
            detail.Append("panel=").Append(opened ? "abierto" : "NO abierto")
                  .Append(" secciones=").Append(SectionPanes.Length);

            // 0.b El ARRANQUE: lo GUARDADO es lo APLICADO. Es la mitad visible de esta superficie —el tema y
            //     el idioma que el usuario eligió tienen que estar puestos al abrir la aplicación— y quien
            //     los tiene que leer es el host: una preferencia es un DATO, y el núcleo no aplica el tema ni
            //     la cultura por su cuenta. Se mide ANTES de tocar nada y contra el valor GUARDADO (no contra
            //     lo que la superficie muestre, que es el mecanismo que se mide después), así que lo que este
            //     renglón delata es el arranque y no la propia medición. El identificador guardado se compara
            //     ya traducido: las preferencias viejas escriben el nombre del enumerado ('Dark') y el
            //     catálogo usa el suyo ('dark_fluent'), y esa traducción es del mismo gestor de temas que usa
            //     el arranque de la versión anterior.
            string canonicalStoredTheme = ThemeManager.ResolveThemeId(storedTheme) ?? ThemeManager.DefaultThemeId;
            string storedLanguageCode = storedLanguage.Length >= 2 ? storedLanguage[..2] : storedLanguage;
            startup = string.Equals(canonicalStoredTheme, appliedThemeId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(storedLanguageCode, loc.CurrentLanguage, StringComparison.OrdinalIgnoreCase);
            detail.Append(" | arranque: tema guardado='").Append(storedTheme).Append("'->'")
                  .Append(canonicalStoredTheme).Append("' aplicado='").Append(appliedThemeId)
                  .Append("' idioma guardado='").Append(storedLanguage)
                  .Append("' cultura='").Append(loc.CurrentLanguage).Append('\'');

            // 1. Catálogos: el del tema (catálogo real + el que sigue al sistema) y los del view model.
            int expectedThemes = CustomThemeService.Instance.GetAllThemes().Count + 1;
            catalog = vm.Themes.Count == expectedThemes
                && vm.AvailableLanguages.Count >= 2
                && vm.ConflictStrategies.Count == 3
                && vm.LogLevels.Count == 4;
            detail.Append(" | temas=").Append(vm.Themes.Count).Append('/').Append(expectedThemes)
                  .Append(" idiomas=").Append(vm.AvailableLanguages.Count)
                  .Append(" estrategias=").Append(vm.ConflictStrategies.Count)
                  .Append(" niveles=").Append(vm.LogLevels.Count);

            // 2. Tema: del desplegable al gestor de temas, y el token del host (el que pinta el lienzo)
            //    cambia de color. Se mide el pincel publicado, no la intención del view model.
            // El tema de la prueba se elige por ser DISTINTO del aplicado, no por un índice fijo: con el
            // índice fijo, un arranque que tuviera otro tema puesto elegía el que ya estaba, el token no
            // cambiaba y el sondeo cantaba un fallo de la superficie cuando el desplegable no había movido
            // nada (medido con la preferencia guardada y el tema aplicado en desacuerdo). La entrada del
            // sistema se descarta: su color lo decide Windows, no el tema.
            var requestedTheme = vm.Themes.FirstOrDefault(t =>
                !string.Equals(t.Id, themes.CurrentThemeId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(t.Id, ThemeManager.SystemThemeId, StringComparison.OrdinalIgnoreCase));
            ThemeCombo.SelectedItem = requestedTheme;
            var brushBefore = CanvasBackgroundColor();
            SaveThroughTheSurface();
            var brushAfter = CanvasBackgroundColor();

            themeApplied = requestedTheme is not null
                && string.Equals(vm.SelectedThemeId, requestedTheme.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(themes.CurrentThemeId, requestedTheme.Id, StringComparison.OrdinalIgnoreCase)
                && brushAfter == NodeCardViewModel.ParseHex(requestedTheme.BgEditor)
                && brushAfter != brushBefore;
            detail.Append(" | tema elegido='").Append(requestedTheme?.Id ?? "<ninguno>")
                  .Append("' vm='").Append(vm.SelectedThemeId)
                  .Append("' aplicado='").Append(themes.CurrentThemeId)
                  .Append("' token=").Append(brushAfter).Append(" (antes ").Append(brushBefore).Append(')');

            // 3. Idioma por su selector: la cultura del núcleo cambia y los textos del marco se reescriben
            //    (el botón de guardar es la prueba de que la cadena sale del diccionario del host). El
            //    idioma de la prueba es el OTRO del que el usuario tenía, y la vuelta es a SU idioma: el
            //    sondeo no puede dejar una preferencia que el usuario no eligió.
            string textBefore = SaveButton.Content?.ToString() ?? string.Empty;
            string probeLanguage = string.Equals(storedLanguage, "en-US", StringComparison.OrdinalIgnoreCase)
                ? "es-ES"
                : "en-US";

            LanguageCombo.SelectedValue = probeLanguage;
            SaveThroughTheSurface();
            var textInProbeLanguage = SaveButton.Content?.ToString();
            languageChanged = string.Equals(loc.CurrentLanguage, probeLanguage[..2], StringComparison.OrdinalIgnoreCase)
                && string.Equals(textInProbeLanguage, ExpectedSaveText(probeLanguage), StringComparison.Ordinal)
                && !string.Equals(textInProbeLanguage, textBefore, StringComparison.Ordinal);
            detail.Append(" | idioma pedido='").Append(probeLanguage)
                  .Append("' vm='").Append(vm.SelectedLanguage)
                  .Append("' cultura='").Append(loc.CurrentLanguage)
                  .Append("' guardar='").Append(textInProbeLanguage).Append('\'');

            LanguageCombo.SelectedValue = storedLanguage;
            SaveThroughTheSurface();
            languageRestored = string.Equals(loc.CurrentLanguage, storedLanguage[..2], StringComparison.OrdinalIgnoreCase)
                && string.Equals(SaveButton.Content?.ToString(), ExpectedSaveText(storedLanguage), StringComparison.Ordinal);
            detail.Append(" | vuelta idioma cultura='").Append(loc.CurrentLanguage)
                  .Append("' guardar='").Append(SaveButton.Content).Append('\'');

            // 4. Una preferencia numérica por su campo: el write-through llega al servicio de preferencias.
            MaxCpuThreadsBox.Value = storedThreads + 1;
            SaveThroughTheSurface();
            stored = prefs.Preferences.MaxParallelThreads == storedThreads + 1;
            detail.Append(" | hilos=").Append(storedThreads).Append("->").Append(prefs.Preferences.MaxParallelThreads);

            // 5. Restauración por la misma superficie: el tema aplicado, la preferencia guardada y los hilos.
            var applied = vm.Themes.FirstOrDefault(t =>
                string.Equals(t.Id, appliedThemeId, StringComparison.OrdinalIgnoreCase));
            if (applied is not null)
            {
                ThemeCombo.SelectedItem = applied;
            }

            MaxCpuThreadsBox.Value = storedThreads;
            SaveThroughTheSurface();

            themeRestored = applied is not null
                && string.Equals(themes.CurrentThemeId, appliedThemeId, StringComparison.OrdinalIgnoreCase);

            restored = string.Equals(prefs.Preferences.ActiveTheme, storedTheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(prefs.Preferences.Language, storedLanguage, StringComparison.OrdinalIgnoreCase)
                && prefs.Preferences.MaxParallelThreads == storedThreads;
            detail.Append(" | restaurado: tema aplicado='").Append(appliedThemeId).Append("->")
                  .Append(themes.CurrentThemeId).Append("' guardado='").Append(storedTheme).Append("->")
                  .Append(prefs.Preferences.ActiveTheme).Append("' idioma='").Append(storedLanguage).Append("->")
                  .Append(prefs.Preferences.Language).Append("' hilos='").Append(storedThreads).Append("->")
                  .Append(prefs.Preferences.MaxParallelThreads).Append('\'');
        }
        finally
        {
            // Red de seguridad: si algún paso lanzó, la máquina del usuario se queda con lo que tenía (el
            // sondeo mide, no configura). Deshace por el MISMO camino por el que la superficie escribe: los
            // setters del view model portable y su comando de guardado. El host no tiene una segunda vía de
            // escritura de preferencias — lo que el sondeo devuelve y lo que el usuario guarda son la misma
            // línea de código, y por eso no puede haber dos verdades sobre el fichero de preferencias.
            vm.SelectedThemeId = storedTheme;
            vm.SelectedLanguage = storedLanguage;
            vm.MaxParallelThreads = storedThreads;
            SaveThroughTheSurface();

            // Lo APLICADO y lo GUARDADO no son lo mismo: el tema que se ve se devuelve por el identificador
            // que tenía aplicado (puede no ser el que el usuario tiene escrito) y la cultura, por su idioma.
            themes.SetThemeById(appliedThemeId);
            loc.SetCulture(storedLanguage);

            // La superficie se recoge SIEMPRE: el sondeo no deja el marco de la aplicación tocado.
            Close();
        }

        return (opened, startup, catalog, themeApplied, themeRestored, languageChanged, languageRestored,
            stored, restored, detail.ToString());
    }

    /// <summary>El guardado del pie, por el MISMO comando que el botón (no una copia de la persistencia).</summary>
    private void SaveThroughTheSurface() => _vm?.SaveSettingsCommand.Execute(null);

    /// <summary>
    /// El texto que el botón de guardar tiene que llevar en cada idioma, según el diccionario del host
    /// (<c>Resources/Strings.resx</c> y <c>Strings.es.resx</c>). El sondeo lo usa como testigo de que la
    /// cadena sale del diccionario y no del fallback incrustado en el código; la guardia del censo ata estas
    /// dos literales a las entradas del diccionario, para que no puedan separarse.
    /// </summary>
    private static string ExpectedSaveText(string languageCode) =>
        string.Equals(languageCode, "en-US", StringComparison.OrdinalIgnoreCase) ? "Save settings" : "Guardar ajustes";

    /// <summary>El color que el host tiene publicado para el fondo del lienzo (el token que pinta el tema).</summary>
    private static Windows.UI.Color CanvasBackgroundColor() =>
        Application.Current?.Resources["CanvasBackgroundBrush"] is SolidColorBrush brush
            ? brush.Color
            : default;
}
