namespace FileFlow.App.Themes;

/// <summary>
/// Define la estructura de un tema visual completo de la aplicación, incluyendo paleta de colores,
/// tipografías, tamaños, radios de borde y parámetros de sombreado.
/// </summary>
public sealed class ThemeDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nuevo Tema";
    public string Description { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; } = false;
    public bool IsDark { get; set; } = true;

    // --- Paleta de Fondos y Contenedores ---
    public string AppBackground { get; set; } = "#1E1E1E";
    public string BgDark { get; set; } = "#1E1E1E";
    public string BgEditor { get; set; } = "#202020";
    public string BgCard { get; set; } = "#282828";
    public string BgSurface { get; set; } = "#1E1E1E";
    public string BgHeader { get; set; } = "#323232";
    public string BgHover { get; set; } = "#383838";

    // --- Colores de Acento y Estados ---
    public string AccentPrimary { get; set; } = "#568AF2";
    public string AccentHover { get; set; } = "#3D72D9";
    public string AccentGlow { get; set; } = "#709EFF";
    public string AccentSuccess { get; set; } = "#10B981";
    public string AccentWarning { get; set; } = "#F59E0B";
    public string AccentError { get; set; } = "#EF4444";
    public string AccentCyan { get; set; } = "#06B6D4";
    public string AccentPurple { get; set; } = "#A855F7";

    // --- Textos y Bordes ---
    public string TextPrimary { get; set; } = "#EAEAEA";
    public string TextSecondary { get; set; } = "#9E9E9E";

    /// <summary>
    /// Color de texto atenuado (telemetría secundaria, breadcrumbs, metadatos).
    /// Debe mantener un contraste mínimo AA (4.5:1) sobre <see cref="BgSurface"/> y <see cref="BgCard"/>.
    /// </summary>
    public string TextMuted { get; set; } = "#7C8698";

    /// <summary>Texto sobre superficies de acento saturadas (botones primarios, badges de estado).</summary>
    public string TextOnAccent { get; set; } = "#FFFFFF";
    public string BorderDark { get; set; } = "#3C3C3C";
    public string BorderSubtle { get; set; } = "#2D2D2D";
    public string GridLine { get; set; } = "#282828";

    // --- Barras de Desplazamiento ---
    public string ScrollbarThumb { get; set; } = "#384152";
    public string ScrollbarThumbHover { get; set; } = "#4F5B73";

    // --- Cable Conector de Nodos ---
    public string WireColorStart { get; set; } = "#818CF8";
    public string WireColorMid { get; set; } = "#6366F1";
    public string WireColorEnd { get; set; } = "#C084FC";

    // --- Tipografía y Escala Visual ---
    public string FontFamily { get; set; } = "Segoe UI Variable Text, Segoe UI, sans-serif";
    public string CodeFontFamily { get; set; } = "Cascadia Code, Consolas, monospace";

    /// <summary>
    /// Tamaño base de la interfaz. De él se deriva la escala tipográfica completa
    /// (<c>FontSizeMicro</c> … <c>FontSizeDisplay</c>), así que un solo ajuste reescala toda la aplicación.
    /// </summary>
    public double BaseFontSize { get; set; } = 12.0;

    /// <summary>
    /// Radio base de la interfaz. De él se deriva la escala <c>RadiusXs</c> … <c>RadiusXxl</c>,
    /// de modo que un solo ajuste cambia la redondez global.
    /// </summary>
    public double CornerRadius { get; set; } = 6.0;

    /// <summary>
    /// Unidad del ritmo de espaciado (densidad). De ella se derivan <c>Space1</c> … <c>Space9</c> (valores
    /// sueltos) y <c>Pad1</c> … <c>Pad9</c> (mismos valores como <c>Thickness</c> para rellenos y márgenes).
    /// Con 4.0 la escala es 2·4·6·8·12·16·20·24·32.
    /// </summary>
    public double SpacingUnit { get; set; } = 4.0;

    /// <summary>Desenfoque base de la escala de elevación (<c>Elev1</c> … <c>Elev4</c> y los resplandores).</summary>
    public double NodeShadowBlur { get; set; } = 24.0;

    /// <summary>
    /// Opacidad base de la escala de elevación. Los temas claros usan valores bajos para que la profundidad
    /// se lea sin ensuciar las superficies.
    /// </summary>
    public double NodeShadowOpacity { get; set; } = 0.55;

    public ThemeDefinition Clone()
    {
        return new ThemeDefinition
        {
            Id = this.Id,
            Name = this.Name,
            Description = this.Description,
            IsBuiltIn = this.IsBuiltIn,
            IsDark = this.IsDark,
            AppBackground = this.AppBackground,
            BgDark = this.BgDark,
            BgEditor = this.BgEditor,
            BgCard = this.BgCard,
            BgSurface = this.BgSurface,
            BgHeader = this.BgHeader,
            BgHover = this.BgHover,
            AccentPrimary = this.AccentPrimary,
            AccentHover = this.AccentHover,
            AccentGlow = this.AccentGlow,
            AccentSuccess = this.AccentSuccess,
            AccentWarning = this.AccentWarning,
            AccentError = this.AccentError,
            AccentCyan = this.AccentCyan,
            AccentPurple = this.AccentPurple,
            TextPrimary = this.TextPrimary,
            TextSecondary = this.TextSecondary,
            TextMuted = this.TextMuted,
            TextOnAccent = this.TextOnAccent,
            BorderDark = this.BorderDark,
            BorderSubtle = this.BorderSubtle,
            GridLine = this.GridLine,
            ScrollbarThumb = this.ScrollbarThumb,
            ScrollbarThumbHover = this.ScrollbarThumbHover,
            WireColorStart = this.WireColorStart,
            WireColorMid = this.WireColorMid,
            WireColorEnd = this.WireColorEnd,
            FontFamily = this.FontFamily,
            CodeFontFamily = this.CodeFontFamily,
            BaseFontSize = this.BaseFontSize,
            CornerRadius = this.CornerRadius,
            SpacingUnit = this.SpacingUnit,
            NodeShadowBlur = this.NodeShadowBlur,
            NodeShadowOpacity = this.NodeShadowOpacity
        };
    }
}
