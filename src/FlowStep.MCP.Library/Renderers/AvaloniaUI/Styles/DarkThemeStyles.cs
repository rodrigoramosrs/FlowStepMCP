using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using FlowStep.Renderers.AvaloniaUI.Themes;

namespace FlowStep.Renderers.AvaloniaUI.Styles
{
    public static class DarkThemeStyles
    {
        public static void ApplyDarkStyles(Window dialog, ThemeColors theme)
        {
            var styles = new Style();

            // === BUTTON BASE ===
            styles.Add(new Style(x => x.OfType<Button>())
            {
                Setters =
                {
                    new Setter(Button.MinWidthProperty, 100.0),
                    new Setter(Button.MinHeightProperty, 40.0),
                    new Setter(Button.PaddingProperty, new Thickness(16, 10)),
                    new Setter(Button.CornerRadiusProperty, new CornerRadius(8)),
                    new Setter(Button.FontSizeProperty, 14.0),
                    new Setter(Button.FontWeightProperty, FontWeight.Medium),
                    new Setter(Button.BorderThicknessProperty, new Thickness(1)),
                    new Setter(Button.CursorProperty, new Cursor(StandardCursorType.Hand)),
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(theme.Surface)),
                    new Setter(Button.ForegroundProperty, new SolidColorBrush(theme.TextPrimary)),
                    new Setter(Button.BorderBrushProperty, new SolidColorBrush(theme.Border)),
                    new Setter(Button.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = Button.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(120) },
                        new BrushTransition { Property = Button.BorderBrushProperty, Duration = System.TimeSpan.FromMilliseconds(120) },
                        new BrushTransition { Property = Button.ForegroundProperty, Duration = System.TimeSpan.FromMilliseconds(80) }
                    })
                }
            });

            styles.Add(new Style(x => x.OfType<Button>().Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(theme.SurfaceHover)),
                    new Setter(Button.BorderBrushProperty, new SolidColorBrush(theme.BorderHover)),
                    new Setter(Button.ForegroundProperty, new SolidColorBrush(theme.TextPrimary))
                }
            });

            styles.Add(new Style(x => x.OfType<Button>().Class(":pressed"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(theme.SurfacePressed)),
                    new Setter(Button.BorderBrushProperty, new SolidColorBrush(theme.BorderHover)),
                    new Setter(Button.ForegroundProperty, new SolidColorBrush(theme.TextSecondary))
                }
            });

            styles.Add(new Style(x => x.OfType<Button>().Class(":disabled"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(theme.Surface)),
                    new Setter(Button.BorderBrushProperty, new SolidColorBrush(theme.Border)),
                    new Setter(Button.ForegroundProperty, new SolidColorBrush(theme.TextDisabled)),
                    new Setter(Button.OpacityProperty, 0.6)
                }
            });

            // === PRIMARY BUTTON ===
            styles.Add(new Style(x => x.OfType<Button>().Class("primary"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(theme.Primary)),
                    new Setter(Button.ForegroundProperty, new SolidColorBrush(Avalonia.Media.Colors.White)),
                    new Setter(Button.BorderBrushProperty, new SolidColorBrush(theme.Primary))
                }
            });

            styles.Add(new Style(x => x.OfType<Button>().Class("primary").Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(theme.PrimaryHover)),
                    new Setter(Button.BorderBrushProperty, new SolidColorBrush(theme.PrimaryHover))
                }
            });

            styles.Add(new Style(x => x.OfType<Button>().Class("primary").Class(":pressed"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(theme.PrimaryPressed)),
                    new Setter(Button.BorderBrushProperty, new SolidColorBrush(theme.PrimaryPressed))
                }
            });

            styles.Add(new Style(x => x.OfType<Button>().Class("primary").Class(":disabled"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, new SolidColorBrush(theme.PrimaryDisabled)),
                    new Setter(Button.BorderBrushProperty, new SolidColorBrush(theme.PrimaryDisabled)),
                    new Setter(Button.ForegroundProperty, new SolidColorBrush(theme.TextDisabled))
                }
            });

            // === TEXTBOX ===
            styles.Add(new Style(x => x.OfType<TextBox>())
            {
                Setters =
                {
                    new Setter(TextBox.MinHeightProperty, 44.0),
                    new Setter(TextBox.PaddingProperty, new Thickness(12, 12)),
                    new Setter(TextBox.CornerRadiusProperty, new CornerRadius(8)),
                    new Setter(TextBox.FontSizeProperty, 14.0),
                    new Setter(TextBox.BackgroundProperty, new SolidColorBrush(theme.Surface)),
                    new Setter(TextBox.ForegroundProperty, new SolidColorBrush(theme.TextPrimary)),
                    new Setter(TextBox.BorderBrushProperty, new SolidColorBrush(theme.Border)),
                    new Setter(TextBox.BorderThicknessProperty, new Thickness(1)),
                    new Setter(TextBox.CaretBrushProperty, new SolidColorBrush(theme.Primary)),
                    new Setter(TextBox.SelectionBrushProperty, new SolidColorBrush(theme.Primary, 0.4)),
                    new Setter(TextBox.SelectionForegroundBrushProperty, new SolidColorBrush(Avalonia.Media.Colors.White)),
                    new Setter(TextBox.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = TextBox.BorderBrushProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = TextBox.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) }
                    })
                }
            });

            styles.Add(new Style(x => x.OfType<TextBox>().Class(":focus"))
            {
                Setters =
                {
                    new Setter(TextBox.BorderBrushProperty, new SolidColorBrush(theme.Primary)),
                    new Setter(TextBox.BackgroundProperty, new SolidColorBrush(theme.SurfaceHover))
                }
            });

            styles.Add(new Style(x => x.OfType<TextBox>().Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(TextBox.BorderBrushProperty, new SolidColorBrush(theme.BorderHover)),
                    new Setter(TextBox.BackgroundProperty, new SolidColorBrush(theme.SurfaceHover))
                }
            });

            styles.Add(new Style(x => x.OfType<TextBox>().Class(":disabled"))
            {
                Setters =
                {
                    new Setter(TextBox.BackgroundProperty, new SolidColorBrush(theme.Background)),
                    new Setter(TextBox.BorderBrushProperty, new SolidColorBrush(theme.Border)),
                    new Setter(TextBox.ForegroundProperty, new SolidColorBrush(theme.TextDisabled)),
                    new Setter(TextBox.OpacityProperty, 0.7)
                }
            });

            // === RADIOBUTTON & CHECKBOX ===
            styles.Add(new Style(x => Selectors.Or(x.OfType<RadioButton>(), x.OfType<CheckBox>()))
            {
                Setters =
                {
                    new Setter(TemplatedControl.FontSizeProperty, 14.0),
                    new Setter(TemplatedControl.ForegroundProperty, new SolidColorBrush(theme.TextPrimary)),
                    new Setter(TemplatedControl.VerticalAlignmentProperty, VerticalAlignment.Center),
                    new Setter(TemplatedControl.MinHeightProperty, 36.0),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(8, 4)),
                    new Setter(TemplatedControl.CursorProperty, new Cursor(StandardCursorType.Hand)),
                    new Setter(TemplatedControl.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = TemplatedControl.ForegroundProperty, Duration = System.TimeSpan.FromMilliseconds(100) }
                    })
                }
            });

            styles.Add(new Style(x => Selectors.Or(
                x.OfType<RadioButton>().Class(":pointerover"),
                x.OfType<CheckBox>().Class(":pointerover")))
            {
                Setters =
                {
                    new Setter(TemplatedControl.ForegroundProperty, new SolidColorBrush(theme.TextSecondary))
                }
            });

            // === COMBOBOX ===
            styles.Add(new Style(x => x.OfType<ComboBox>())
            {
                Setters =
                {
                    new Setter(ComboBox.MinHeightProperty, 44.0),
                    new Setter(ComboBox.PaddingProperty, new Thickness(12, 0, 8, 0)),
                    new Setter(ComboBox.CornerRadiusProperty, new CornerRadius(8)),
                    new Setter(ComboBox.FontSizeProperty, 14.0),
                    new Setter(ComboBox.BackgroundProperty, new SolidColorBrush(theme.Surface)),
                    new Setter(ComboBox.ForegroundProperty, new SolidColorBrush(theme.TextPrimary)),
                    new Setter(ComboBox.BorderBrushProperty, new SolidColorBrush(theme.Border)),
                    new Setter(ComboBox.BorderThicknessProperty, new Thickness(1)),
                    new Setter(ComboBox.PlaceholderForegroundProperty, new SolidColorBrush(theme.TextMuted)),
                    new Setter(ComboBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                    new Setter(ComboBox.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                    new Setter(ComboBox.CursorProperty, new Cursor(StandardCursorType.Hand)),
                    new Setter(ComboBox.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = ComboBox.BorderBrushProperty, Duration = System.TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = ComboBox.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(150) }
                    })
                }
            });

            styles.Add(new Style(x => x.OfType<ComboBox>().Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(ComboBox.BorderBrushProperty, new SolidColorBrush(theme.BorderHover)),
                    new Setter(ComboBox.BackgroundProperty, new SolidColorBrush(theme.SurfaceHover))
                }
            });

            styles.Add(new Style(x => x.OfType<ComboBox>().Class(":focus"))
            {
                Setters =
                {
                    new Setter(ComboBox.BorderBrushProperty, new SolidColorBrush(theme.Primary)),
                    new Setter(ComboBox.BackgroundProperty, new SolidColorBrush(theme.SurfaceHover))
                }
            });

            styles.Add(new Style(x => x.OfType<ComboBox>().Class(":disabled"))
            {
                Setters =
                {
                    new Setter(ComboBox.BackgroundProperty, new SolidColorBrush(theme.Background)),
                    new Setter(ComboBox.BorderBrushProperty, new SolidColorBrush(theme.Border)),
                    new Setter(ComboBox.ForegroundProperty, new SolidColorBrush(theme.TextDisabled)),
                    new Setter(ComboBox.OpacityProperty, 0.7)
                }
            });

            // === SCROLLVIEWER ===
            styles.Add(new Style(x => x.OfType<ScrollViewer>())
            {
                Setters =
                {
                    new Setter(ScrollViewer.BackgroundProperty, new SolidColorBrush(theme.Background))
                }
            });

            // === LISTBOX ===
            styles.Add(new Style(x => x.OfType<ListBox>())
            {
                Setters =
                {
                    new Setter(ListBox.BackgroundProperty, new SolidColorBrush(theme.Surface)),
                    new Setter(ListBox.BorderBrushProperty, new SolidColorBrush(theme.Border)),
                    new Setter(ListBox.BorderThicknessProperty, new Thickness(1)),
                    new Setter(ListBox.CornerRadiusProperty, new CornerRadius(8)),
                    new Setter(ListBox.PaddingProperty, new Thickness(4))
                }
            });

            // === LISTBOXITEM ===
            styles.Add(new Style(x => x.OfType<ListBoxItem>())
            {
                Setters =
                {
                    new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Avalonia.Media.Colors.Transparent)),
                    new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(theme.TextPrimary)),
                    new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 8)),
                    new Setter(ListBoxItem.CornerRadiusProperty, new CornerRadius(6)),
                    new Setter(ListBoxItem.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = ListBoxItem.BackgroundProperty, Duration = System.TimeSpan.FromMilliseconds(80) }
                    })
                }
            });

            styles.Add(new Style(x => x.OfType<ListBoxItem>().Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(theme.SurfaceHover))
                }
            });

            styles.Add(new Style(x => x.OfType<ListBoxItem>().Class(":selected"))
            {
                Setters =
                {
                    new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(theme.Primary, 0.2)),
                    new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(theme.Primary))
                }
            });

            dialog.Styles.Add(styles);
        }
    }
}