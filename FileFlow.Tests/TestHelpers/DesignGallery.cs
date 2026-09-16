using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Composición de muestra con la que se congelan visualmente los tokens del tema y la capa de estilos.
///
/// Está construida **con las clases de la capa de diseño** (<c>card</c>, <c>panel</c>, <c>badge</c>,
/// <c>led</c>, <c>primary</c>…), no con colores literales: si una clase deja de resolver un token o cambia de
/// forma, la captura lo refleja. Las escalas (radios, espaciado y elevación) leen los tokens publicados por
/// el tema activo, así que la misma galería sirve para comparar temas.
/// </summary>
public static class DesignGallery
{
    public const int Width = 620;
    public const int Height = 640;

    /// <summary>
    /// Construye la galería. Exige el hilo de UI de la sesión headless: sin esa comprobación, construirla
    /// desde el hilo del runner «funciona» hasta que un token no se resuelve y el fallo aparece como «el
    /// token no existe» —cuando el problema real es el hilo—.
    /// </summary>
    public static Control Build()
    {
        AvaloniaTestHelper.RequireUIThread($"{nameof(DesignGallery)}.{nameof(Build)}");

        return new Border
        {
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    Section("Botones", new WrapPanel
                    {
                        ItemSpacing = 8,
                        LineSpacing = 8,
                        Children =
                        {
                            Button("Primario", "primary"),
                            Button("Éxito", "success"),
                            Button("Advertencia", "warning"),
                            Button("Peligro", "danger"),
                            Button("Ghost", "ghost"),
                            Button("Pill", "pill"),
                            IconButton()
                        }
                    }),

                    Section("Campos", new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBox { Text = "Texto de interfaz", Width = 260, HorizontalAlignment = HorizontalAlignment.Left },
                            new TextBox { Text = "ruta\\al\\fichero.txt", Classes = { "mono" }, Width = 260, HorizontalAlignment = HorizontalAlignment.Left },
                            new ComboBox
                            {
                                ItemsSource = new[] { "Opción A", "Opción B", "Opción C" },
                                SelectedIndex = 0,
                                Width = 260,
                                HorizontalAlignment = HorizontalAlignment.Left
                            },
                            new NumericUpDown { Value = 12, Minimum = 0, Maximum = 40, Width = 160, HorizontalAlignment = HorizontalAlignment.Left }
                        }
                    }),

                    Section("Superficies e indicadores", new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Border
                            {
                                Classes = { "card" },
                                Child = new StackPanel
                                {
                                    Spacing = 6,
                                    Children =
                                    {
                                        new TextBlock { Text = "Tarjeta (RadiusMd + Elev1)", Classes = { "semiBold", "primaryText" } },
                                        new TextBlock { Text = "Superficie de contenido con borde sutil.", Classes = { "caption", "secondary" } },
                                        new WrapPanel
                                        {
                                            ItemSpacing = 6,
                                            Children =
                                            {
                                                new Border { Classes = { "badge" }, Child = new TextBlock { Text = "badge", Classes = { "micro", "muted" } } },
                                                new Border { Classes = { "badgeAccent" }, Child = new TextBlock { Text = "accent", Classes = { "micro", "onAccent" } } },
                                                new Border { Classes = { "led", "ledLarge", "onSuccess" } },
                                                new Border { Classes = { "led", "ledLarge", "onInfo" } },
                                                new Border { Classes = { "led", "ledLarge", "onError" } }
                                            }
                                        }
                                    }
                                }
                            },
                            new Border
                            {
                                Classes = { "panel" },
                                Child = new Grid
                                {
                                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                                    Children =
                                    {
                                        new TextBlock { Text = "Panel base", Classes = { "bodySm", "primaryText" }, VerticalAlignment = VerticalAlignment.Center },
                                        new Border { Classes = { "statusPill" }, [Grid.ColumnProperty] = 1, Child = new TextBlock { Text = "statusPill", Classes = { "micro", "secondary" } } }
                                    }
                                }
                            }
                        }
                    }),

                    Section("Escala de radios", RadiusScale()),

                    Section("Escala de espaciado", SpacingScale()),

                    Section("Escala de elevación", ElevationScale())
                }
            }
        };
    }

    private static Control Section(string title, Control content) => new StackPanel
    {
        Spacing = 6,
        Children =
        {
            new TextBlock { Text = title, Classes = { "sectionLabel" } },
            content
        }
    };

    private static Button Button(string text, string classes)
    {
        var button = new Button { Content = text };
        button.Classes.Add(classes);
        return button;
    }

    private static Button IconButton()
    {
        var button = new Button { Content = "✕" };
        button.Classes.Add("icon");
        return button;
    }

    /// <summary>Escala completa de radios del tema, del más pequeño a la pastilla.</summary>
    private static Control RadiusScale()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        foreach (string token in new[] { "RadiusXs", "RadiusSm", "RadiusMd", "RadiusLg", "RadiusXl", "RadiusXxl", "RadiusPill" })
        {
            panel.Children.Add(new Border
            {
                Width = 46,
                Height = 34,
                Background = Token<IBrush>("BgHoverBrush"),
                BorderBrush = Token<IBrush>("BorderDarkBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = Token<CornerRadius>(token)
            });
        }

        return panel;
    }

    /// <summary>Barras con la escala de espaciado: la anchura real de cada una es el token.</summary>
    private static Control SpacingScale()
    {
        var panel = new StackPanel { Spacing = 4 };

        for (int i = 1; i <= 9; i++)
        {
            panel.Children.Add(new Border
            {
                Height = 8,
                Width = Token<double>($"Space{i}"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Token<IBrush>("AccentCyanBrush"),
                CornerRadius = Token<CornerRadius>("RadiusPill")
            });
        }

        return panel;
    }

    /// <summary>Escala de profundidad: cada tarjeta proyecta el nivel de elevación del tema.</summary>
    private static Control ElevationScale()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };

        foreach (string token in new[] { "Elev1", "Elev2", "Elev3", "Elev4", "ElevGlowAccent" })
        {
            panel.Children.Add(new Border
            {
                Width = 62,
                Height = 40,
                Background = Token<IBrush>("BgSurfaceBrush"),
                CornerRadius = Token<CornerRadius>("RadiusSm"),
                BoxShadow = Token<BoxShadows>(token)
            });
        }

        return panel;
    }

    /// <summary>Lee un token publicado por el tema activo; si falta, el token es un recurso muerto.</summary>
    private static T Token<T>(string key) => VisualSnapshot.ResolveToken<T>(key);
}
