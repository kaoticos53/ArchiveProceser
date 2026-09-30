using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileFlow.Plugin.Integrations.UI.Services;
using FileFlow.Plugin.Integrations.UI.ViewModels;
using FileFlow.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

/// <summary>
/// El GESTOR DE PRESETS del transcodificador: su view model PORTABLE, ejercido contra un almacén de mentira.
///
/// <para>Lo que se mide aquí es la REGLA del producto —qué se escribe al guardar, qué no se puede borrar, qué
/// se pregunta antes de destruir— y no el pintado: la ventana de la versión anterior y el cuerpo del host son vistas
/// de este mismo view model, así que una regla que se mueva a una de las dos vistas desaparece de aquí. El
/// almacén es de mentira a propósito: la prueba no escribe en el fichero de presets de quien la corre, y
/// «se guardó» se comprueba por lo que el almacén RECIBIÓ, no por la referencia que el view model tiene en la
/// mano (que cambiaría aunque el guardado se olvidara).</para>
/// </summary>
public class MediaPresetManagerViewModelTests
{
    private static readonly IReadOnlyList<MediaPreset> Catalog =
    [
        new MediaPreset
        {
            Id = "preset-mp3",
            Name = "Extraer Audio MP3",
            Description = "Extrae la pista de audio principal.",
            OutputExtension = ".mp3",
            FfmpegArguments = "-vn -c:a libmp3lame -b:a 192k",
            Category = "Audio",
            IsSystemDefault = true
        },
        new MediaPreset
        {
            Id = "preset-mio",
            Name = "Mi Preset",
            Description = "El que me hice yo.",
            OutputExtension = ".mov",
            FfmpegArguments = "-c:v prores",
            Category = "Video",
            IsSystemDefault = false
        }
    ];

    [Fact]
    public void TheManager_ShouldOpenWithTheStoreCatalog_AndTheFirstOneChosen()
    {
        var store = new FakePresetStore(Catalog);
        var vm = new MediaPresetManagerViewModel(store, new RecordingDialogService());

        vm.Presets.Should().HaveCount(2, "el gestor enseña el catálogo del almacén, no una lista propia");
        vm.SelectedPreset!.Id.Should().Be("preset-mp3");
        vm.PresetName.Should().Be("Extraer Audio MP3", "el formulario se llena con el preset elegido");
        vm.OutputExtension.Should().Be(".mp3");
        vm.Category.Should().Be("Audio");
    }

    [Fact]
    public void Saving_ShouldWriteThroughTheStore_NotOnlyInTheList()
    {
        var store = new FakePresetStore(Catalog);
        var dialogs = new RecordingDialogService();
        var vm = new MediaPresetManagerViewModel(store, dialogs);

        vm.PresetDescription = "Descripción editada por la prueba";
        vm.FfmpegArguments = "-vn -c:a libopus -b:a 96k";
        vm.SaveCurrentCommand.Execute(null);

        store.Saves.Should().ContainSingle("«Guardar» tiene que llegar al almacén que lee el nodo que transcodifica");
        store.Saves[0].Id.Should().Be("preset-mp3");
        store.Saves[0].Description.Should().Be("Descripción editada por la prueba");
        store.Saves[0].FfmpegArguments.Should().Be("-vn -c:a libopus -b:a 96k");

        store.GetPresets().Single(p => p.Id == "preset-mp3").Description
            .Should().Be("Descripción editada por la prueba", "y el catálogo del almacén queda con lo nuevo");
        dialogs.InformationMessages.Should().NotBeEmpty("el gestor avisa de que guardó");
    }

    [Fact]
    public void Saving_ShouldNormalizeTheExtension()
    {
        var store = new FakePresetStore(Catalog);
        var vm = new MediaPresetManagerViewModel(store, new RecordingDialogService());

        vm.OutputExtension = "mp4";
        vm.SaveCurrentCommand.Execute(null);

        store.Saves.Should().ContainSingle();

        // La extensión sin punto no es una extensión para el motor: se normaliza ANTES de guardar.
        store.Saves[0].OutputExtension.Should().Be(".mp4");
        vm.OutputExtension.Should().Be(".mp4");
    }

    [Fact]
    public async Task DeletingASystemPreset_ShouldWarn_AndNotDelete()
    {
        var store = new FakePresetStore(Catalog);
        var dialogs = new RecordingDialogService();
        var vm = new MediaPresetManagerViewModel(store, dialogs);

        await vm.DeletePresetCommand.ExecuteAsync(null);

        store.Deletes.Should().BeEmpty("los presets del sistema no se borran");
        store.GetPresets().Should().HaveCount(2);
        dialogs.WarningMessages.Should().NotBeEmpty("y se explica por qué, en vez de no hacer nada en silencio");
        dialogs.ConfirmationQuestions.Should().BeEmpty("a lo que no se puede borrar no se le pregunta");
    }

    [Fact]
    public async Task DeletingAUserPreset_ShouldAskFirst_AndDeleteWhenConfirmed()
    {
        var store = new FakePresetStore(Catalog);
        var dialogs = new RecordingDialogService { ConfirmationAnswer = true };
        var vm = new MediaPresetManagerViewModel(store, dialogs);

        vm.SelectedPreset = vm.Presets.Single(p => p.Id == "preset-mio");
        await vm.DeletePresetCommand.ExecuteAsync(null);

        dialogs.ConfirmationQuestions.Should().ContainSingle("borrar algo pregunta una vez, y por la vía asíncrona")
            .Which.Should().Contain("Mi Preset", "y la pregunta nombra lo que se va a borrar");
        store.Deletes.Should().ContainSingle().Which.Should().Be("preset-mio");
        store.GetPresets().Should().ContainSingle();
    }

    [Fact]
    public async Task DeletingAUserPreset_ShouldNotDelete_WhenNotConfirmed()
    {
        var store = new FakePresetStore(Catalog);
        var dialogs = new RecordingDialogService { ConfirmationAnswer = false };
        var vm = new MediaPresetManagerViewModel(store, dialogs);

        vm.SelectedPreset = vm.Presets.Single(p => p.Id == "preset-mio");
        await vm.DeletePresetCommand.ExecuteAsync(null);

        // Las DOS mitades de la regla: se pregunta SIEMPRE, y un «no» no borra nada. Una orden que no
        // pregunta y otra que borra sin esperar la respuesta se caen cada una por su lado.
        dialogs.ConfirmationQuestions.Should().ContainSingle("un «no» no puede saltarse la pregunta");
        store.Deletes.Should().BeEmpty("un «no» no borra nada");
        store.GetPresets().Should().HaveCount(2);
    }

    [Fact]
    public async Task Resetting_ShouldAsk_AndResetTheStore()
    {
        var store = new FakePresetStore(Catalog);
        var dialogs = new RecordingDialogService { ConfirmationAnswer = true };
        var vm = new MediaPresetManagerViewModel(store, dialogs);

        await vm.ResetDefaultsCommand.ExecuteAsync(null);

        dialogs.ConfirmationQuestions.Should().ContainSingle("el restablecimiento también se pregunta");
        store.Resets.Should().Be(1, "el restablecimiento se le pide al almacén, no se simula en una lista local");
    }

    [Fact]
    public async Task Resetting_ShouldNotReset_WhenNotConfirmed()
    {
        var store = new FakePresetStore(Catalog);
        var dialogs = new RecordingDialogService { ConfirmationAnswer = false };
        var vm = new MediaPresetManagerViewModel(store, dialogs);

        await vm.ResetDefaultsCommand.ExecuteAsync(null);

        dialogs.ConfirmationQuestions.Should().ContainSingle("la pregunta se hace igual");
        store.Resets.Should().Be(0, "y un «no» deja el catálogo del usuario como estaba");
    }

    [Fact]
    public void TheFactoryPreset_ShouldBeCreatedInTheStore_AndChosen()
    {
        var store = new FakePresetStore(Catalog);
        var vm = new MediaPresetManagerViewModel(store, new RecordingDialogService());

        vm.NewPresetCommand.Execute(null);

        store.Saves.Should().ContainSingle();
        store.GetPresets().Should().HaveCount(3);
        vm.Presets.Should().HaveCount(3);
        vm.SelectedPreset!.Id.Should().Be(store.Saves[0].Id, "el preset nuevo queda elegido para editarlo");
        vm.SelectedPreset.IsSystemDefault.Should().BeFalse();
        vm.OutputExtension.Should().Be(".mp4");
    }

    [Fact]
    public void ChoosingAPreset_ShouldFillTheForm_EvenWhenItsCategoryIsUnknown()
    {
        var store = new FakePresetStore(Catalog);
        var vm = new MediaPresetManagerViewModel(store, new RecordingDialogService());

        vm.SelectedPreset = vm.Presets.Single(p => p.Id == "preset-mio");
        vm.PresetName.Should().Be("Mi Preset");
        vm.OutputExtension.Should().Be(".mov");
        vm.Category.Should().Be("Video");

        // Un preset importado con una categoría propia no puede aparecer en un desplegable en blanco: se añade
        // a la lista de categorías (y sin ella, al guardar se sustituía en silencio por «Video»).
        vm.SelectedPreset = new MediaPreset { Id = "importado", Name = "Importado", Category = "Documental" };
        vm.Category.Should().Be("Documental");
        vm.Categories.Should().Contain("Documental");
    }

    /// <summary>
    /// El almacén de la prueba: guarda su propia copia de cada preset y ANOTA lo que le piden, para que
    /// «se guardó» se pueda medir por el recado recibido y no por el objeto que el llamador tiene en la mano.
    /// </summary>
    private sealed class FakePresetStore : IMediaPresetStore
    {
        private readonly List<MediaPreset> _presets;

        public FakePresetStore(IEnumerable<MediaPreset> presets) => _presets = [.. presets.Select(Clone)];

        public List<MediaPreset> Saves { get; } = [];

        public List<string> Deletes { get; } = [];

        public int Resets { get; private set; }

        public IReadOnlyList<MediaPreset> GetPresets() => _presets.Select(Clone).ToList();

        public List<string> GetPresetNames() => _presets.Select(p => p.Name).ToList();

        public MediaPreset? GetPresetByName(string name) =>
            _presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) is { } found
                ? Clone(found)
                : null;

        public void SavePreset(MediaPreset preset)
        {
            Saves.Add(Clone(preset));

            int at = _presets.FindIndex(p => p.Id == preset.Id || string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
            if (at >= 0)
            {
                _presets[at] = Clone(preset);
            }
            else
            {
                _presets.Add(Clone(preset));
            }
        }

        public bool DeletePreset(string presetId)
        {
            MediaPreset? target = _presets.FirstOrDefault(p => p.Id == presetId && !p.IsSystemDefault);
            if (target is null)
            {
                return false;
            }

            Deletes.Add(presetId);
            _presets.Remove(target);
            return true;
        }

        public void ResetToDefaults()
        {
            Resets++;
            _presets.Clear();
            _presets.AddRange(Catalog.Select(Clone));
        }

        private static MediaPreset Clone(MediaPreset preset) => new()
        {
            Id = preset.Id,
            Name = preset.Name,
            Description = preset.Description,
            OutputExtension = preset.OutputExtension,
            FfmpegArguments = preset.FfmpegArguments,
            Category = preset.Category,
            IsSystemDefault = preset.IsSystemDefault
        };
    }
}
