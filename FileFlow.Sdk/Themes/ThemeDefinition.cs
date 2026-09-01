namespace FileFlow.Sdk.Themes;

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
    public string AppBackground { get; set; } = "#0D1117";
    public string BgDark { get; set; } = "#0D1117";
    public string BgEditor { get; set; } = "#10131B";
    public string BgCard { get; set; } = "#161B22";
    public string BgSurface { get; set; } = "#131720";
    public string BgHeader { get; set; } = "#1A1F29";
    public string BgHover { get; set; } = "#21262D";

    // --- Colores de Acento y Estados ---
    public string AccentPrimary { get; set; } = "#6366F1";
    public string AccentHover { get; set; } = "#4F46E5";
    public string AccentGlow { get; set; } = "#818CF8";
    public string AccentSuccess { get; set; } = "#10B981";
    public string AccentWarning { get; set; } = "#F59E0B";
    public string AccentError { get; set; } = "#EF4444";
    public string AccentCyan { get; set; } = "#06B6D4";
    public string AccentPurple { get; set; } = "#A855F7";

    // --- Textos y Bordes ---
    public string TextPrimary { get; set; } = "#F0F6FC";
    public string TextSecondary { get; set; } = "#8B949E";
    public string BorderDark { get; set; } = "#30363D";
    public string BorderSubtle { get; set; } = "#21262D";
    public string GridLine { get; set; } = "#1A202C";

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
    public double BaseFontSize { get; set; } = 12.0;
    public double CornerRadius { get; set; } = 6.0;
    public double NodeShadowBlur { get; set; } = 24.0;
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
            NodeShadowBlur = this.NodeShadowBlur,
            NodeShadowOpacity = this.NodeShadowOpacity
        };
    }
}
