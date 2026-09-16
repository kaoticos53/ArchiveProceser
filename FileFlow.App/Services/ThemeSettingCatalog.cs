using System.Collections.Generic;

namespace FileFlow.App.Services;

/// <summary>Tipo de control con el que se edita un ajuste del tema.</summary>
public enum ThemeSettingKind
{
    /// <summary>Color en formato <c>#RRGGBB</c> / <c>#AARRGGBB</c>.</summary>
    Color,

    /// <summary>Valor numérico con rango y paso (radios, tamaños, densidades, sombras).</summary>
    Number,

    /// <summary>Elección entre opciones conocidas (familias tipográficas).</summary>
    Choice
}

/// <summary>
/// Descripción de un ajuste editable del tema: a qué propiedad de <c>ThemeDefinition</c> corresponde,
/// con qué control se edita, y cómo se etiqueta en la interfaz.
/// </summary>
/// <param name="Property">Nombre exacto de la propiedad de <c>ThemeDefinition</c>. Es la clave de cobertura.</param>
/// <param name="Kind">Control de edición.</param>
/// <param name="LabelKey">Clave de localización del rótulo.</param>
/// <param name="SectionKey">Clave de localización de la sección a la que pertenece.</param>
/// <param name="Minimum">Valor mínimo (sólo <see cref="ThemeSettingKind.Number"/>).</param>
/// <param name="Maximum">Valor máximo (sólo <see cref="ThemeSettingKind.Number"/>).</param>
/// <param name="Step">Incremento del control numérico.</param>
/// <param name="Options">Opciones disponibles (sólo <see cref="ThemeSettingKind.Choice"/>).</param>
public sealed record ThemeSettingDescriptor(
    string Property,
    ThemeSettingKind Kind,
    string LabelKey,
    string SectionKey,
    double Minimum = 0,
    double Maximum = 0,
    double Step = 1,
    IReadOnlyList<string>? Options = null);

/// <summary>
/// Catálogo de ajustes del Theme Studio.
///
/// Es la fuente única de verdad de «qué se puede personalizar»: la vista se genera a partir de esta tabla
/// (no hay una fila escrita a mano por ajuste), y una guardia comprueba que **toda** propiedad visual de
/// <c>ThemeDefinition</c> esté cubierta aquí o declarada en <see cref="NotEditableYet"/> con su motivo.
/// Así, añadir un color al tema sin exponerlo en el Studio hace fallar la suite en lugar de pasar inadvertido.
/// </summary>
public static class ThemeSettingCatalog
{
    // Secciones (orden de presentación)
    public const string SectionSurfaces = "ThemeStudio_Section_Surfaces";
    public const string SectionAccents = "ThemeStudio_Section_Accents";
    public const string SectionText = "ThemeStudio_Section_Text";
    public const string SectionBorders = "ThemeStudio_Section_Borders";
    public const string SectionScrollbars = "ThemeStudio_Section_Scrollbars";
    public const string SectionWire = "ThemeStudio_Section_Wire";
    public const string SectionTypography = "ThemeStudio_Section_Typography";
    public const string SectionShape = "ThemeStudio_Section_Shape";
    public const string SectionElevation = "ThemeStudio_Section_Elevation";

    /// <summary>Claves de sección en orden de presentación.</summary>
    public static IReadOnlyList<string> Sections { get; } =
    [
        SectionSurfaces,
        SectionAccents,
        SectionText,
        SectionBorders,
        SectionScrollbars,
        SectionWire,
        SectionTypography,
        SectionShape,
        SectionElevation
    ];

    /// <summary>Propiedades de <c>ThemeDefinition</c> que todavía no tienen control, con su motivo.</summary>
    public static IReadOnlyDictionary<string, string> NotEditableYet { get; } = new Dictionary<string, string>
    {
        ["Id"] = "Identidad técnica del tema: se asigna al crear o importar.",
        ["Name"] = "Se edita en el campo de nombre de la cabecera del editor.",
        ["Description"] = "Metadato descriptivo; no afecta a ningún token visual.",
        ["IsBuiltIn"] = "Marca de fábrica; la gestiona el servicio de temas.",
        ["IsDark"] = "Deriva la variante clara/oscura de FluentTheme, no un token propio."
    };

    /// <summary>Ajustes editables, en orden de presentación.</summary>
    public static IReadOnlyList<ThemeSettingDescriptor> Settings { get; } =
    [
        // Superficies
        new("AppBackground", ThemeSettingKind.Color, "ThemeStudio_Set_AppBackground", SectionSurfaces),
        new("BgDark", ThemeSettingKind.Color, "ThemeStudio_Set_BgDark", SectionSurfaces),
        new("BgEditor", ThemeSettingKind.Color, "ThemeStudio_Set_BgEditor", SectionSurfaces),
        new("BgSurface", ThemeSettingKind.Color, "ThemeStudio_Set_BgSurface", SectionSurfaces),
        new("BgCard", ThemeSettingKind.Color, "ThemeStudio_Set_BgCard", SectionSurfaces),
        new("BgHeader", ThemeSettingKind.Color, "ThemeStudio_Set_BgHeader", SectionSurfaces),
        new("BgHover", ThemeSettingKind.Color, "ThemeStudio_Set_BgHover", SectionSurfaces),

        // Acentos y estados
        new("AccentPrimary", ThemeSettingKind.Color, "ThemeStudio_Set_AccentPrimary", SectionAccents),
        new("AccentHover", ThemeSettingKind.Color, "ThemeStudio_Set_AccentHover", SectionAccents),
        new("AccentGlow", ThemeSettingKind.Color, "ThemeStudio_Set_AccentGlow", SectionAccents),
        new("AccentSuccess", ThemeSettingKind.Color, "ThemeStudio_Set_AccentSuccess", SectionAccents),
        new("AccentWarning", ThemeSettingKind.Color, "ThemeStudio_Set_AccentWarning", SectionAccents),
        new("AccentError", ThemeSettingKind.Color, "ThemeStudio_Set_AccentError", SectionAccents),
        new("AccentCyan", ThemeSettingKind.Color, "ThemeStudio_Set_AccentCyan", SectionAccents),
        new("AccentPurple", ThemeSettingKind.Color, "ThemeStudio_Set_AccentPurple", SectionAccents),

        // Textos
        new("TextPrimary", ThemeSettingKind.Color, "ThemeStudio_Set_TextPrimary", SectionText),
        new("TextSecondary", ThemeSettingKind.Color, "ThemeStudio_Set_TextSecondary", SectionText),
        new("TextMuted", ThemeSettingKind.Color, "ThemeStudio_Set_TextMuted", SectionText),
        new("TextOnAccent", ThemeSettingKind.Color, "ThemeStudio_Set_TextOnAccent", SectionText),

        // Bordes y rejilla
        new("BorderDark", ThemeSettingKind.Color, "ThemeStudio_Set_BorderDark", SectionBorders),
        new("BorderSubtle", ThemeSettingKind.Color, "ThemeStudio_Set_BorderSubtle", SectionBorders),
        new("GridLine", ThemeSettingKind.Color, "ThemeStudio_Set_GridLine", SectionBorders),

        // Barras de desplazamiento
        new("ScrollbarThumb", ThemeSettingKind.Color, "ThemeStudio_Set_ScrollbarThumb", SectionScrollbars),
        new("ScrollbarThumbHover", ThemeSettingKind.Color, "ThemeStudio_Set_ScrollbarThumbHover", SectionScrollbars),

        // Cable de conexión (gradiente)
        new("WireColorStart", ThemeSettingKind.Color, "ThemeStudio_Set_WireColorStart", SectionWire),
        new("WireColorMid", ThemeSettingKind.Color, "ThemeStudio_Set_WireColorMid", SectionWire),
        new("WireColorEnd", ThemeSettingKind.Color, "ThemeStudio_Set_WireColorEnd", SectionWire),

        // Tipografía
        new("FontFamily", ThemeSettingKind.Choice, "ThemeStudio_Set_FontFamily", SectionTypography,
            Options: ["Segoe UI", "Segoe UI Variable Text", "Inter", "Roboto", "Outfit", "Arial", "Ubuntu", "Tahoma", "Verdana"]),
        new("CodeFontFamily", ThemeSettingKind.Choice, "ThemeStudio_Set_CodeFontFamily", SectionTypography,
            Options: ["Cascadia Code, Consolas, monospace", "Cascadia Code", "Consolas", "Fira Code", "Courier New", "monospace"]),
        new("BaseFontSize", ThemeSettingKind.Number, "ThemeStudio_Set_BaseFontSize", SectionTypography, 9, 20, 0.5),

        // Forma y densidad
        new("CornerRadius", ThemeSettingKind.Number, "ThemeStudio_Set_CornerRadius", SectionShape, 0, 24, 1),
        new("SpacingUnit", ThemeSettingKind.Number, "ThemeStudio_Set_SpacingUnit", SectionShape, 2, 8, 1),

        // Elevación
        new("NodeShadowBlur", ThemeSettingKind.Number, "ThemeStudio_Set_NodeShadowBlur", SectionElevation, 0, 48, 2),
        new("NodeShadowOpacity", ThemeSettingKind.Number, "ThemeStudio_Set_NodeShadowOpacity", SectionElevation, 0, 1, 0.05)
    ];
}
