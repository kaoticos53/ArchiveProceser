using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FileFlow.Sdk.Themes;

namespace FileFlow.App.Services;

/// <summary>
/// Servicio para la gestión, serialización, persistencia y generación dinámica de diccionarios
/// de recursos para temas visuales en WPF.
/// </summary>
public class CustomThemeService
{
    private static readonly Lazy<CustomThemeService> _instance = new(() => new CustomThemeService());
    public static CustomThemeService Instance => _instance.Value;

    private readonly string _storagePath;
    private readonly System.Threading.Lock _lock = new();
    private readonly List<ThemeDefinition> _builtInThemes;
    private List<ThemeDefinition> _customThemes = [];

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public CustomThemeService() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileFlow", "custom_themes.json"))
    {
    }

    public CustomThemeService(string storagePath)
    {
        _storagePath = storagePath;
        _builtInThemes = CreateBuiltInThemes();
        LoadCustomThemes();
    }

    public IReadOnlyList<ThemeDefinition> GetAllThemes()
    {
        lock (_lock)
        {
            var list = new List<ThemeDefinition>(_builtInThemes.Count + _customThemes.Count);
            list.AddRange(_builtInThemes.Select(t => t.Clone()));
            list.AddRange(_customThemes.Select(t => t.Clone()));
            return list;
        }
    }

    public ThemeDefinition? GetThemeById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        lock (_lock)
        {
            var found = _builtInThemes.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                     ?? _customThemes.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            return found?.Clone();
        }
    }

    public void SaveCustomTheme(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        lock (_lock)
        {
            theme.IsBuiltIn = false;
            int existingIndex = _customThemes.FindIndex(t => t.Id.Equals(theme.Id, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                _customThemes[existingIndex] = theme.Clone();
            }
            else
            {
                _customThemes.Add(theme.Clone());
            }

            PersistCustomThemes();
        }
    }

    public bool DeleteCustomTheme(string id)
    {
        lock (_lock)
        {
            int removed = _customThemes.RemoveAll(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                PersistCustomThemes();
                return true;
            }
            return false;
        }
    }

    public ThemeDefinition DuplicateTheme(ThemeDefinition original, string newName)
    {
        ArgumentNullException.ThrowIfNull(original);

        var copy = original.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = string.IsNullOrWhiteSpace(newName) ? $"{original.Name} (Copia)" : newName;
        copy.IsBuiltIn = false;

        SaveCustomTheme(copy);
        return copy;
    }

    public string ExportThemeToJson(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return JsonSerializer.Serialize(theme, _jsonOptions);
    }

    public ThemeDefinition ImportThemeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("El contenido JSON del tema no puede estar vacío.", nameof(json));
        }

        var imported = JsonSerializer.Deserialize<ThemeDefinition>(json, _jsonOptions)
            ?? throw new InvalidOperationException("No se pudo deserializar la definición del tema.");

        imported.Id = Guid.NewGuid().ToString("N");
        imported.IsBuiltIn = false;
        if (string.IsNullOrWhiteSpace(imported.Name))
        {
            imported.Name = "Tema Importado";
        }

        SaveCustomTheme(imported);
        return imported;
    }

    public static ResourceDictionary BuildResourceDictionary(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var dict = new ResourceDictionary();

        void AddSolidBrush(string key, string hex, string fallbackHex = "#FFFFFF")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) hex = fallbackHex;
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                dict[key] = brush;
            }
            catch
            {
                var fallbackColor = (Color)ColorConverter.ConvertFromString(fallbackHex);
                var fallbackBrush = new SolidColorBrush(fallbackColor);
                fallbackBrush.Freeze();
                dict[key] = fallbackBrush;
            }
        }

        AddSolidBrush("AppBackgroundBrush", theme.AppBackground, "#0D1117");
        AddSolidBrush("BgDarkBrush", theme.BgDark, "#0D1117");
        AddSolidBrush("BgEditorBrush", theme.BgEditor, "#10131B");
        AddSolidBrush("BgCardBrush", theme.BgCard, "#161B22");
        AddSolidBrush("BgSurfaceBrush", theme.BgSurface, "#131720");
        AddSolidBrush("BgHeaderBrush", theme.BgHeader, "#1A1F29");
        AddSolidBrush("BgHoverBrush", theme.BgHover, "#21262D");

        AddSolidBrush("AccentPrimaryBrush", theme.AccentPrimary, "#6366F1");
        AddSolidBrush("AccentHoverBrush", theme.AccentHover, "#4F46E5");
        AddSolidBrush("AccentGlowBrush", theme.AccentGlow, "#818CF8");
        AddSolidBrush("AccentSuccessBrush", theme.AccentSuccess, "#10B981");
        AddSolidBrush("AccentWarningBrush", theme.AccentWarning, "#F59E0B");
        AddSolidBrush("AccentErrorBrush", theme.AccentError, "#EF4444");
        AddSolidBrush("AccentCyanBrush", theme.AccentCyan, "#06B6D4");
        AddSolidBrush("AccentPurpleBrush", theme.AccentPurple, "#A855F7");

        AddSolidBrush("TextPrimaryBrush", theme.TextPrimary, "#F0F6FC");
        AddSolidBrush("TextSecondaryBrush", theme.TextSecondary, "#8B949E");
        AddSolidBrush("BorderDarkBrush", theme.BorderDark, "#30363D");
        AddSolidBrush("BorderSubtleBrush", theme.BorderSubtle, "#21262D");
        AddSolidBrush("GridLineBrush", theme.GridLine, "#1A202C");

        AddSolidBrush("ScrollbarThumbBrush", theme.ScrollbarThumb, "#384152");
        AddSolidBrush("ScrollbarThumbHoverBrush", theme.ScrollbarThumbHover, "#4F5B73");

        // Gradient connection wire brush
        try
        {
            var colStart = (Color)ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(theme.WireColorStart) ? "#818CF8" : theme.WireColorStart);
            var colMid = (Color)ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(theme.WireColorMid) ? "#6366F1" : theme.WireColorMid);
            var colEnd = (Color)ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(theme.WireColorEnd) ? "#C084FC" : theme.WireColorEnd);

            var gradBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new GradientStop(colStart, 0.0),
                    new GradientStop(colMid, 0.5),
                    new GradientStop(colEnd, 1.0)
                }
            };
            gradBrush.Freeze();
            dict["ConnectionWireBrush"] = gradBrush;
        }
        catch
        {
        }

        // DropShadowEffect
        try
        {
            var shadow = new DropShadowEffect
            {
                BlurRadius = Math.Max(0, theme.NodeShadowBlur),
                ShadowDepth = 4,
                Direction = 270,
                Color = Colors.Black,
                Opacity = Math.Clamp(theme.NodeShadowOpacity, 0.0, 1.0)
            };
            shadow.Freeze();
            dict["NodeShadowEffect"] = shadow;
        }
        catch
        {
        }

        // Typography & Scale Tokens
        try
        {
            dict["AppFontFamily"] = new FontFamily(string.IsNullOrWhiteSpace(theme.FontFamily) ? "Segoe UI" : theme.FontFamily);
            dict["CodeFontFamily"] = new FontFamily(string.IsNullOrWhiteSpace(theme.CodeFontFamily) ? "Cascadia Code, Consolas" : theme.CodeFontFamily);
            dict["AppFontSize"] = theme.BaseFontSize > 0 ? theme.BaseFontSize : 12.0;
            dict["AppCornerRadius"] = new CornerRadius(Math.Max(0, theme.CornerRadius));
        }
        catch
        {
        }

        return dict;
    }

    private void LoadCustomThemes()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    string json = File.ReadAllText(_storagePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var loaded = JsonSerializer.Deserialize<List<ThemeDefinition>>(json, _jsonOptions);
                        if (loaded != null)
                        {
                            _customThemes = loaded;
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Fallback a lista vacía si hay error de I/O o JSON inválido
            }
            _customThemes = [];
        }
    }

    private void PersistCustomThemes()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(_customThemes, _jsonOptions);
            File.WriteAllText(_storagePath, json);
        }
        catch
        {
            // Manejar silenciosamente excepciones de permisos de disco
        }
    }

    private static List<ThemeDefinition> CreateBuiltInThemes()
    {
        return
        [
            new ThemeDefinition
            {
                Id = "dark_fluent",
                Name = "🌙 Oscuro Fluent (Predeterminado)",
                Description = "Paleta moderna oscura de alta legibilidad estilo GitHub & Windows 11 Fluent.",
                IsBuiltIn = true,
                IsDark = true,
                AppBackground = "#0D1117",
                BgDark = "#0D1117",
                BgEditor = "#10131B",
                BgCard = "#161B22",
                BgSurface = "#131720",
                BgHeader = "#1A1F29",
                BgHover = "#21262D",
                AccentPrimary = "#6366F1",
                AccentHover = "#4F46E5",
                AccentGlow = "#818CF8",
                AccentSuccess = "#10B981",
                AccentWarning = "#F59E0B",
                AccentError = "#EF4444",
                AccentCyan = "#06B6D4",
                AccentPurple = "#A855F7",
                TextPrimary = "#F0F6FC",
                TextSecondary = "#8B949E",
                BorderDark = "#30363D",
                BorderSubtle = "#21262D",
                GridLine = "#1A202C",
                ScrollbarThumb = "#384152",
                ScrollbarThumbHover = "#4F5B73",
                WireColorStart = "#818CF8",
                WireColorMid = "#6366F1",
                WireColorEnd = "#C084FC",
                FontFamily = "Segoe UI Variable Text, Segoe UI, sans-serif",
                CodeFontFamily = "Cascadia Code, Consolas, monospace",
                BaseFontSize = 12.0,
                CornerRadius = 6.0,
                NodeShadowBlur = 24.0,
                NodeShadowOpacity = 0.55
            },
            new ThemeDefinition
            {
                Id = "light_studio",
                Name = "☀️ Claro Minimalista",
                Description = "Entorno claro de alta luminosidad y contraste limpio para trabajo diurno.",
                IsBuiltIn = true,
                IsDark = false,
                AppBackground = "#F8FAFC",
                BgDark = "#F1F5F9",
                BgEditor = "#F8FAFC",
                BgCard = "#FFFFFF",
                BgSurface = "#F1F5F9",
                BgHeader = "#E2E8F0",
                BgHover = "#CBD5E1",
                AccentPrimary = "#4F46E5",
                AccentHover = "#4338CA",
                AccentGlow = "#6366F1",
                AccentSuccess = "#059669",
                AccentWarning = "#D97706",
                AccentError = "#DC2626",
                AccentCyan = "#0891B2",
                AccentPurple = "#9333EA",
                TextPrimary = "#0F172A",
                TextSecondary = "#64748B",
                BorderDark = "#CBD5E1",
                BorderSubtle = "#E2E8F0",
                GridLine = "#E2E8F0",
                ScrollbarThumb = "#CBD5E1",
                ScrollbarThumbHover = "#94A3B8",
                WireColorStart = "#6366F1",
                WireColorMid = "#4F46E5",
                WireColorEnd = "#9333EA",
                FontFamily = "Segoe UI Variable Text, Segoe UI, sans-serif",
                CodeFontFamily = "Cascadia Code, Consolas, monospace",
                BaseFontSize = 12.0,
                CornerRadius = 6.0,
                NodeShadowBlur = 16.0,
                NodeShadowOpacity = 0.12
            },
            new ThemeDefinition
            {
                Id = "cyber_neon",
                Name = "🔮 Cyber Neón",
                Description = "Estética futurista con fondos violetas profundos y acentos cian y magenta brillantes.",
                IsBuiltIn = true,
                IsDark = true,
                AppBackground = "#0B0814",
                BgDark = "#0B0814",
                BgEditor = "#0E0A1A",
                BgCard = "#151026",
                BgSurface = "#1C1533",
                BgHeader = "#1A1330",
                BgHover = "#2A1F4D",
                AccentPrimary = "#D946EF",
                AccentHover = "#C026D3",
                AccentGlow = "#F472B6",
                AccentSuccess = "#10E599",
                AccentWarning = "#FBBF24",
                AccentError = "#F43F5E",
                AccentCyan = "#00F0FF",
                AccentPurple = "#A855F7",
                TextPrimary = "#FDF4FF",
                TextSecondary = "#A78BFA",
                BorderDark = "#3B2667",
                BorderSubtle = "#251842",
                GridLine = "#1F1438",
                ScrollbarThumb = "#4C2889",
                ScrollbarThumbHover = "#6D38C3",
                WireColorStart = "#00F0FF",
                WireColorMid = "#D946EF",
                WireColorEnd = "#F472B6",
                FontFamily = "Segoe UI Variable Text, Segoe UI, sans-serif",
                CodeFontFamily = "Cascadia Code, Consolas, monospace",
                BaseFontSize = 12.0,
                CornerRadius = 8.0,
                NodeShadowBlur = 28.0,
                NodeShadowOpacity = 0.70
            },
            new ThemeDefinition
            {
                Id = "pastel_spring",
                Name = "🌸 Primavera Pastel",
                Description = "Gama de tonos pastel suaves y relajantes con acentos florales y menta.",
                IsBuiltIn = true,
                IsDark = false,
                AppBackground = "#FFF5F7",
                BgDark = "#FFE4E9",
                BgEditor = "#FFF8FA",
                BgCard = "#FFFFFF",
                BgSurface = "#FFF0F3",
                BgHeader = "#FCE7EC",
                BgHover = "#FBCFE8",
                AccentPrimary = "#EC4899",
                AccentHover = "#DB2777",
                AccentGlow = "#F472B6",
                AccentSuccess = "#10B981",
                AccentWarning = "#F59E0B",
                AccentError = "#F43F5E",
                AccentCyan = "#06B6D4",
                AccentPurple = "#C084FC",
                TextPrimary = "#4A044E",
                TextSecondary = "#831843",
                BorderDark = "#FBCFE8",
                BorderSubtle = "#FCE7EC",
                GridLine = "#FCE7EC",
                ScrollbarThumb = "#F472B6",
                ScrollbarThumbHover = "#EC4899",
                WireColorStart = "#F472B6",
                WireColorMid = "#EC4899",
                WireColorEnd = "#C084FC",
                FontFamily = "Segoe UI Variable Text, Segoe UI, sans-serif",
                CodeFontFamily = "Cascadia Code, Consolas, monospace",
                BaseFontSize = 12.0,
                CornerRadius = 10.0,
                NodeShadowBlur = 20.0,
                NodeShadowOpacity = 0.15
            },
            new ThemeDefinition
            {
                Id = "midnight_oled",
                Name = "🌌 Midnight OLED (Negro Puro)",
                Description = "Fondo negro absoluto #000000 para pantallas OLED con máximo contraste visual.",
                IsBuiltIn = true,
                IsDark = true,
                AppBackground = "#000000",
                BgDark = "#000000",
                BgEditor = "#050505",
                BgCard = "#0D0D0D",
                BgSurface = "#121212",
                BgHeader = "#141414",
                BgHover = "#1F1F1F",
                AccentPrimary = "#8B5CF6",
                AccentHover = "#7C3AED",
                AccentGlow = "#A78BFA",
                AccentSuccess = "#00E676",
                AccentWarning = "#FFD600",
                AccentError = "#FF1744",
                AccentCyan = "#00E5FF",
                AccentPurple = "#D500F9",
                TextPrimary = "#FFFFFF",
                TextSecondary = "#A0A0A0",
                BorderDark = "#282828",
                BorderSubtle = "#1A1A1A",
                GridLine = "#141414",
                ScrollbarThumb = "#333333",
                ScrollbarThumbHover = "#555555",
                WireColorStart = "#00E5FF",
                WireColorMid = "#8B5CF6",
                WireColorEnd = "#D500F9",
                FontFamily = "Segoe UI Variable Text, Segoe UI, sans-serif",
                CodeFontFamily = "Cascadia Code, Consolas, monospace",
                BaseFontSize = 12.0,
                CornerRadius = 6.0,
                NodeShadowBlur = 30.0,
                NodeShadowOpacity = 0.85
            },
            new ThemeDefinition
            {
                Id = "nord_slate",
                Name = "❄️ Nord Slate (Ártico)",
                Description = "Gama de colores nórdicos árticos fríos con excelente equilibrio ergonómico.",
                IsBuiltIn = true,
                IsDark = true,
                AppBackground = "#2E3440",
                BgDark = "#2E3440",
                BgEditor = "#242933",
                BgCard = "#3B4252",
                BgSurface = "#343B48",
                BgHeader = "#434C5E",
                BgHover = "#4C566A",
                AccentPrimary = "#88C0D0",
                AccentHover = "#81A1C1",
                AccentGlow = "#8FBCBB",
                AccentSuccess = "#A3BE8C",
                AccentWarning = "#EBCB8B",
                AccentError = "#BF616A",
                AccentCyan = "#88C0D0",
                AccentPurple = "#B48EAD",
                TextPrimary = "#ECEFF4",
                TextSecondary = "#D8DEE9",
                BorderDark = "#4C566A",
                BorderSubtle = "#3B4252",
                GridLine = "#2E3440",
                ScrollbarThumb = "#4C566A",
                ScrollbarThumbHover = "#5E81AC",
                WireColorStart = "#8FBCBB",
                WireColorMid = "#88C0D0",
                WireColorEnd = "#B48EAD",
                FontFamily = "Segoe UI Variable Text, Segoe UI, sans-serif",
                CodeFontFamily = "Cascadia Code, Consolas, monospace",
                BaseFontSize = 12.0,
                CornerRadius = 6.0,
                NodeShadowBlur = 22.0,
                NodeShadowOpacity = 0.50
            },
            new ThemeDefinition
            {
                Id = "dracula_purple",
                Name = "🧛 Dracula Purple",
                Description = "Tema gótico oscuro con tonos morados y resaltados en rosa, cian y verde lima.",
                IsBuiltIn = true,
                IsDark = true,
                AppBackground = "#282A36",
                BgDark = "#282A36",
                BgEditor = "#21222C",
                BgCard = "#343746",
                BgSurface = "#2D303E",
                BgHeader = "#3A3D4D",
                BgHover = "#44475A",
                AccentPrimary = "#BD93F9",
                AccentHover = "#A774F7",
                AccentGlow = "#FF79C6",
                AccentSuccess = "#50FA7B",
                AccentWarning = "#F1FA8C",
                AccentError = "#FF5555",
                AccentCyan = "#8BE9FD",
                AccentPurple = "#BD93F9",
                TextPrimary = "#F8F8F2",
                TextSecondary = "#6272A4",
                BorderDark = "#44475A",
                BorderSubtle = "#343746",
                GridLine = "#2D303E",
                ScrollbarThumb = "#44475A",
                ScrollbarThumbHover = "#6272A4",
                WireColorStart = "#8BE9FD",
                WireColorMid = "#BD93F9",
                WireColorEnd = "#FF79C6",
                FontFamily = "Segoe UI Variable Text, Segoe UI, sans-serif",
                CodeFontFamily = "Cascadia Code, Consolas, monospace",
                BaseFontSize = 12.0,
                CornerRadius = 6.0,
                NodeShadowBlur = 24.0,
                NodeShadowOpacity = 0.60
            },
            new ThemeDefinition
            {
                Id = "emerald_forest",
                Name = "🌲 Emerald Forest",
                Description = "Paleta orgánica en tonos verdes bosque profundos y acentos esmeralda brillantes.",
                IsBuiltIn = true,
                IsDark = true,
                AppBackground = "#061A14",
                BgDark = "#061A14",
                BgEditor = "#04140F",
                BgCard = "#0B2920",
                BgSurface = "#0E3328",
                BgHeader = "#123E31",
                BgHover = "#184D3D",
                AccentPrimary = "#10B981",
                AccentHover = "#059669",
                AccentGlow = "#34D399",
                AccentSuccess = "#34D399",
                AccentWarning = "#FBBF24",
                AccentError = "#F87171",
                AccentCyan = "#2DD4BF",
                AccentPurple = "#A78BFA",
                TextPrimary = "#ECFDF5",
                TextSecondary = "#6EE7B7",
                BorderDark = "#184D3D",
                BorderSubtle = "#0E3328",
                GridLine = "#0B2920",
                ScrollbarThumb = "#184D3D",
                ScrollbarThumbHover = "#10B981",
                WireColorStart = "#2DD4BF",
                WireColorMid = "#10B981",
                WireColorEnd = "#34D399",
                FontFamily = "Segoe UI Variable Text, Segoe UI, sans-serif",
                CodeFontFamily = "Cascadia Code, Consolas, monospace",
                BaseFontSize = 12.0,
                CornerRadius = 6.0,
                NodeShadowBlur = 24.0,
                NodeShadowOpacity = 0.60
            }
        ];
    }
}
