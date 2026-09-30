using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFlow.Sdk.Localization;
using FileFlow.Sdk.Services;

namespace FileFlow.Plugin.Archives.UI.ViewModels;

/// <summary>
/// El GESTOR DE CONTRASEÑAS de los nodos de descompresión, sin toolkit: el texto de la lista (una clave por
/// línea), cuántas lleva, qué se guarda en el nodo y la lectura/escritura del .txt de importación y exportación.
///
/// <para><b>Por qué existe.</b> Esta lógica vivía en el code-behind de la ventana del plugin —que sólo el host
/// con el toolkit de UI puede montar—, así que ningún otro host podía ofrecer el gestor sin
/// reescribirlo (y una segunda copia de la regla es una segunda verdad). Ahora el nodo declara la superficie al
/// SDK (<see cref="FileFlow.Sdk.Descriptors.INodeDialogSurfaceProvider"/>) con este view model como contenido:
/// la versión anterior lo pinta en su ventana nativa del host y un host WinUI en un cuerpo suyo, y los dos escriben la
/// misma lista en el mismo parámetro del nodo.</para>
///
/// <para><b>Qué decide y qué NO decide la vista.</b> Aquí queda el QUÉ: qué es una clave, cuántas hay, cómo se
/// guardan (separadas por «; » en <c>PasswordList</c>) y qué se lee o escribe en el .txt. Lo que la vista
/// decide es de cada host: dónde está el archivo —su selector es del host— y cuándo se cierra la superficie.</para>
/// </summary>
public partial class PasswordManagerViewModel : ObservableObject
{
    /// <summary>
    /// Los separadores aceptados: los saltos de línea (la lista que el usuario pega puede venir con cualquiera) y
    /// el «; », que es como la guarda el nodo en <c>PasswordList</c> y lo que lee el motor de descompresión al
    /// probarlas en orden. Sin el «; » aquí, abrir el gestor de un nodo que ya tenía claves las mostraba todas en
    /// una sola línea —medido con el ratón: el gestor reabría con «alfa; beta» en vez de una por línea—.
    /// </summary>
    private static readonly string[] Separators = [";", "\r\n", "\r", "\n"];

    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;
    private readonly Action<string>? _onSaved;

    /// <summary>
    /// El gestor sobre la lista actual del nodo. <paramref name="onSaved"/> es la vuelta al nodo —lo único que
    /// sabe escribir su parámetro—; sin ella el gestor enseña y no guarda (el camino del diseñador de XAML).
    /// </summary>
    public PasswordManagerViewModel(
        string? currentPasswords = null,
        Action<string>? onSaved = null,
        IDialogService? dialogService = null,
        ILocalizationService? localizationService = null)
    {
        _onSaved = onSaved;
        _dialogService = dialogService ?? NullDialogService.Instance;
        _loc = localizationService ?? LocalizationManager.Instance;

        Text = string.Join(Environment.NewLine, Split(currentPasswords ?? string.Empty));
    }

    /// <summary>El texto del editor: una clave por línea (lo que el usuario ve y edita).</summary>
    [ObservableProperty]
    private string _text = string.Empty;

    /// <summary>Las claves, ya normalizadas: sin líneas vacías y sin espacios de sobra.</summary>
    public IReadOnlyList<string> Passwords => Split(Text);

    /// <summary>Cuántas claves lleva la lista (lo que la superficie cuenta en su esquina).</summary>
    public int LoadedCount => Passwords.Count;

    /// <summary>El recuento con su texto del diccionario (se lee en el idioma del usuario).</summary>
    public string CountLabel =>
        _loc.GetFormattedString("PasswordManager_MsgLoadedCount", "{0} clave(s) cargada(s)", LoadedCount);

    /// <summary>
    /// La lista en la forma en que la guarda el nodo: separada por «; », que es lo que lee el motor de
    /// descompresión al probarlas en orden.
    /// </summary>
    public string PasswordsText => string.Join("; ", Passwords);

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(LoadedCount));
        OnPropertyChanged(nameof(CountLabel));
    }

    /// <summary>Añade contenido a la lista (lo que trae un .txt importado), sin dejar una línea en blanco de más.</summary>
    public void Append(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        string separator = string.IsNullOrWhiteSpace(Text) ? string.Empty : Environment.NewLine;
        Text = Text + separator + content.Trim();
    }

    /// <summary>Guarda: escribe la lista en el nodo por la vuelta que le dio quien abrió el gestor.</summary>
    public void Save() => _onSaved?.Invoke(PasswordsText);

    /// <summary>
    /// Importa un .txt: el archivo lo elige la VISTA (el selector es del host) y su contenido se añade aquí,
    /// porque la regla —una clave por línea— es del producto.
    /// </summary>
    public async Task ImportFromAsync(string path)
    {
        try
        {
            Append(await File.ReadAllTextAsync(path));
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(
                _loc.GetFormattedString("PasswordManager_MsgImportError", "Error al importar archivo: {0}", ex.Message),
                _loc.GetString("Error", "Error"));
        }
    }

    /// <summary>Exporta la lista al .txt que eligió la vista.</summary>
    public async Task ExportToAsync(string path)
    {
        try
        {
            await File.WriteAllTextAsync(path, Text);
            _dialogService.ShowInformation(
                _loc.GetFormattedString("PasswordManager_MsgExportSuccess", "Contraseñas exportadas con éxito a:\n{0}", path),
                _loc.GetString("Success", "Éxito"));
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(
                _loc.GetFormattedString("PasswordManager_MsgExportError", "Error al exportar archivo: {0}", ex.Message),
                _loc.GetString("Error", "Error"));
        }
    }

    /// <summary>Las claves del texto: se corta por cualquier separador y se descartan las vacías.</summary>
    private static IReadOnlyList<string> Split(string text) =>
        text.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
