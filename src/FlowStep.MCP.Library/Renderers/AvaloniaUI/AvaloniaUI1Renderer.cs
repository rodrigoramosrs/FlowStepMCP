using Avalonia;
using Avalonia.Animation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlowStep.Contracts;
using FlowStep.Models;
using FlowStep.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlowStep.MCP.Library.Renderers.AvaloniaUI
{
    /// <summary>
    /// Renderizador Avalonia que exibe interações dinâmicas (notificações, prompts, progresso).
    /// Compatível com versões do Avalonia onde Close(result) pode não estar disponível:
    /// usa TaskCompletionSource para garantir que o ShowDialog retorne um InteractionResponse.
    /// </summary>
    public class AvaloniaUIRenderer1 : IInteractionRenderer, IDisposable
    {
        private readonly object _lock = new();
        private MainWindow? _mainWindow;
        private bool _disposed = false;

        // Referências aos controles de input para acessar valores no footer
        private ComboBox? _currentComboBox;
        private List<CheckBox>? _currentCheckBoxes;
        private TextBox? _currentTextBox;
        private TextBox? _currentCustomTextBox;
        private InteractionRequest? _currentRequest;

        public event Func<InteractionRequest, Task<InteractionResponse>>? OnInteractionRequested;
        public event Action<string, int, int, string>? OnProgressUpdate;
        public event Action<string>? OnProgressEnd;

        public async Task InitializeAsync()
        {
            if (_disposed) return;
            if (_mainWindow != null) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_mainWindow == null)
                {
                    _mainWindow = new MainWindow();
                    _mainWindow.ShowInTaskbar = false;
                    _mainWindow.WindowState = WindowState.Minimized;
                    _mainWindow.Show();
                }
            });
        }

        /// <summary>
        /// Renderiza a interação e retorna um InteractionResponse garantido (nunca null).
        /// Usa TaskCompletionSource para receber o resultado do diálogo.
        /// </summary>
        public async Task<InteractionResponse> RenderAsync(InteractionRequest request, CancellationToken ct)
        {
            if (_disposed) return new InteractionResponse { Cancelled = true };

            try
            {
                if (OnInteractionRequested != null)
                {
                    _ = OnInteractionRequested.Invoke(request);
                }

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (_mainWindow == null)
                        await InitializeAsync();
                });

                var tcs = new TaskCompletionSource<InteractionResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var dialog = CreateDialog(request, tcs);
                    if (_mainWindow != null)
                        dialog.ShowDialog(_mainWindow);
                    else
                        dialog.Show();
                });

                using (ct.Register(() =>
                {
                    tcs.TrySetResult(new InteractionResponse { Cancelled = true, TimedOut = true });
                }))
                {
                    var result = await tcs.Task;
                    return result ?? new InteractionResponse { Cancelled = true };
                }
            }
            catch (OperationCanceledException)
            {
                return new InteractionResponse { Cancelled = true, TimedOut = true };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao renderizar interação: {ex.Message}");
                return new InteractionResponse { Cancelled = true };
            }
        }

        public void ReportProgress(string operationId, int current, int total, string status)
        {
            if (_disposed) return;

            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    OnProgressUpdate?.Invoke(operationId, current, total, status);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao reportar progresso: {ex.Message}");
            }
        }

        public void EndProgress(string operationId)
        {
            if (_disposed) return;

            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    OnProgressEnd?.Invoke(operationId);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao finalizar progresso: {ex.Message}");
            }
        }

        /// <summary>
        /// Cria um Window (diálogo) com tema Dark moderno, responsivo e alta qualidade visual.
        /// Botões ficam sempre fixos no footer, fora do ScrollViewer, para garantir acessibilidade.
        /// </summary>
        private Window CreateDialog(InteractionRequest request, TaskCompletionSource<InteractionResponse?> tcs)
        {
            // Limpar referências anteriores
            _currentComboBox = null;
            _currentCheckBoxes = null;
            _currentTextBox = null;
            _currentCustomTextBox = null;
            _currentRequest = request;

            var theme = new ThemeColors
            {
                Background = Color.Parse("#0F0F12"),
                Surface = Color.Parse("#1A1A1F"),
                SurfaceHover = Color.Parse("#232329"),
                SurfacePressed = Color.Parse("#2A2A32"),
                Primary = Color.Parse("#6366F1"),
                PrimaryHover = Color.Parse("#818CF8"),
                PrimaryPressed = Color.Parse("#4F46E5"),
                PrimaryDisabled = Color.Parse("#374151"),
                TextPrimary = Color.Parse("#F9FAFB"),
                TextSecondary = Color.Parse("#9CA3AF"),
                TextMuted = Color.Parse("#6B7280"),
                TextDisabled = Color.Parse("#4B5563"),
                Border = Color.Parse("#27272A"),
                BorderHover = Color.Parse("#3F3F46"),
                Error = Color.Parse("#EF4444"),
                Success = Color.Parse("#22C55E"),
                Warning = Color.Parse("#F59E0B")
            };

            const double MAX_WIDTH = 768;
            const double MIN_WIDTH = 600;
            const double MAX_HEIGHT = 1024;
            const double MIN_HEIGHT = 800;

            var dialog = new Window
            {
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Interação" : request.Title,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = true,
                MinWidth = MIN_WIDTH,
                MinHeight = MIN_HEIGHT,
                MaxWidth = MAX_WIDTH,
                MaxHeight = MAX_HEIGHT,
                Background = new SolidColorBrush(theme.Background),
                TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur },
                CornerRadius = new CornerRadius(12),
                FontFamily = new FontFamily("Inter, -apple-system, BlinkMacSystemFont, Segoe UI, Roboto, sans-serif"),
                SizeToContent = SizeToContent.Height,
                Width = Math.Min(MAX_WIDTH, Math.Max(MIN_WIDTH, 420))
            };

            dialog.Closed += (_, _) =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(new InteractionResponse { Cancelled = true });

                // Limpar referências ao fechar
                _currentComboBox = null;
                _currentCheckBoxes = null;
                _currentTextBox = null;
                _currentCustomTextBox = null;
                _currentRequest = null;
            };

            var mainGrid = new Grid
            {
                RowDefinitions = new RowDefinitions
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = GridLength.Auto }
                }
            };

            // === HEADER ===
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 20, 24, 16),
                Child = CreateHeaderContent(request, theme, MAX_WIDTH)
            };

            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // === CONTENT E FOOTER ===
            if (request.Type == InteractionType.Notification)
            {
                // Notification: sem conteúdo scrollável, apenas footer com botão
                var emptyContent = new Border
                {
                    Background = new SolidColorBrush(theme.Background),
                    MinHeight = 20
                };
                Grid.SetRow(emptyContent, 1);
                mainGrid.Children.Add(emptyContent);

                var notificationFooter = CreateNotificationFooter(tcs, dialog, theme);
                Grid.SetRow(notificationFooter, 2);
                mainGrid.Children.Add(notificationFooter);
            }
            else if (request.Type == InteractionType.Confirmation && request.Options?.Count > 0)
            {
                // Confirmation com opções customizadas: botões são o próprio conteúdo
                var confirmationPanel = CreateConfirmationButtonsPanel(request, dialog, tcs, theme);

                // Envolver em ScrollViewer caso haja muitos botões
                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Padding = new Thickness(24, 20, 24, 24),
                    Background = new SolidColorBrush(theme.Background),
                    Content = confirmationPanel
                };

                Grid.SetRow(scrollViewer, 1);
                mainGrid.Children.Add(scrollViewer);

                // Sem footer para Confirmation com opções customizadas
                var emptyFooter = new Border { Height = 0 };
                Grid.SetRow(emptyFooter, 2);
                mainGrid.Children.Add(emptyFooter);
            }
            else
            {
                // Demais tipos: inputs no ScrollViewer + botões fixos no footer
                var contentPanel = new StackPanel { Spacing = 16 };

                // Criar inputs específicos e guardar referências
                switch (request.Type)
                {
                    case InteractionType.Confirmation:
                        // Confirmation sem opções: usar radio buttons ou simples
                        var confirmationContent = CreateSimpleConfirmationContent(request, theme);
                        contentPanel.Children.Add(confirmationContent);
                        break;

                    case InteractionType.SingleChoice:
                        _currentComboBox = CreateSingleChoiceComboBox(request, theme);
                        contentPanel.Children.Add(_currentComboBox);
                        break;

                    case InteractionType.MultiChoice:
                        _currentCheckBoxes = CreateMultiChoiceCheckBoxes(request, theme);
                        foreach (var cb in _currentCheckBoxes)
                        {
                            contentPanel.Children.Add(cb);
                        }
                        break;

                    case InteractionType.TextInput:
                        _currentTextBox = CreateTextInputBox(request, theme);
                        contentPanel.Children.Add(_currentTextBox);
                        break;

                    case InteractionType.ChoiceWithText:
                        var (combo, textBox) = CreateChoiceWithTextControls(request, theme);
                        _currentComboBox = combo;
                        _currentCustomTextBox = textBox;
                        contentPanel.Children.Add(_currentComboBox);
                        contentPanel.Children.Add(_currentCustomTextBox);
                        break;
                }

                // ScrollViewer contendo APENAS os inputs (sem botões)
                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Padding = new Thickness(24, 20, 24, 20),
                    Background = new SolidColorBrush(theme.Background),
                    Content = contentPanel
                };

                Grid.SetRow(scrollViewer, 1);
                mainGrid.Children.Add(scrollViewer);

                // Footer fixo com botões Cancelar/Confirmar
                var standardFooter = CreateStandardFooter(tcs, dialog, theme, request.Type);
                Grid.SetRow(standardFooter, 2);
                mainGrid.Children.Add(standardFooter);
            }

            // Container final com sombra
            var rootContainer = new Border
            {
                Background = new SolidColorBrush(theme.Background),
                CornerRadius = new CornerRadius(12),
                BoxShadow = new BoxShadows(
                    new BoxShadow
                    {
                        OffsetX = 0,
                        OffsetY = 0,
                        Blur = 0,
                        Spread = 1,
                        Color = new Color(0x20, 0xFF, 0xFF, 0xFF)
                    }, new[] {
                    new BoxShadow
                    {
                        OffsetX = 0,
                        OffsetY = 8,
                        Blur = 32,
                        Spread = 0,
                        Color = new Color(0x60, 0x00, 0x00, 0x00)
                    } }
                ),
                Child = mainGrid
            };

            dialog.Content = rootContainer;

            ApplyDarkStyles(dialog, theme);

            return dialog;
        }

        /// <summary>
        /// Cria o conteúdo do header (título e mensagem)
        /// </summary>
        private StackPanel CreateHeaderContent(InteractionRequest request, ThemeColors theme, double maxWidth)
        {
            var panel = new StackPanel { Spacing = 6 };

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = request.Title,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(theme.TextPrimary),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    LineHeight = 28
                });
            }

            if (!string.IsNullOrWhiteSpace(request.Message))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = request.Message,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(theme.TextSecondary),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 22,
                    MaxWidth = maxWidth - 28
                });
            }

            return panel;
        }

        /// <summary>
        /// Cria footer para Notification (botão único)
        /// </summary>
        private Border CreateNotificationFooter(TaskCompletionSource<InteractionResponse?> tcs, Window dialog, ThemeColors theme)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 12
            };

            var btn = new Button
            {
                Content = "Entendi",
                Classes = { "primary" }
            };

            btn.Click += (_, _) =>
            {
                tcs.TrySetResult(new InteractionResponse { Success = true });
                dialog.Close();
            };

            panel.Children.Add(btn);

            return new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 16, 24, 20),
                Child = panel
            };
        }

        /// <summary>
        /// Cria footer padrão com Cancelar/Confirmar para tipos que usam inputs
        /// </summary>
        private Border CreateStandardFooter(TaskCompletionSource<InteractionResponse?> tcs, Window dialog, ThemeColors theme, InteractionType type)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 12
            };

            var cancelBtn = new Button { Content = "Cancelar" };
            var confirmBtn = new Button { Content = "Confirmar", Classes = { "primary" } };

            cancelBtn.Click += (_, _) =>
            {
                tcs.TrySetResult(new InteractionResponse { Cancelled = true });
                dialog.Close();
            };

            confirmBtn.Click += (_, _) =>
            {
                var response = BuildResponseFromCurrentState(type);
                tcs.TrySetResult(response);
                dialog.Close();
            };

            panel.Children.Add(cancelBtn);
            panel.Children.Add(confirmBtn);

            return new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 16, 24, 20),
                Child = panel
            };
        }

        /// <summary>
        /// Constrói a resposta baseada no estado atual dos controles
        /// </summary>
        private InteractionResponse BuildResponseFromCurrentState(InteractionType type)
        {
            var response = new InteractionResponse { Success = true };

            switch (type)
            {
                case InteractionType.SingleChoice:
                    var selectedValue = (_currentComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                        ?? _currentComboBox?.SelectedItem?.ToString()
                        ?? string.Empty;
                    response.SelectedValues = new List<string> { selectedValue };
                    break;

                case InteractionType.MultiChoice:
                    response.SelectedValues = _currentCheckBoxes?
                        .Where(c => c.IsChecked == true)
                        .Select(c => c.Tag?.ToString() ?? c.Content?.ToString() ?? string.Empty)
                        .ToList() ?? new List<string>();
                    break;

                case InteractionType.TextInput:
                    response.SelectedValues = new List<string> { _currentTextBox?.Text ?? string.Empty };
                    break;

                case InteractionType.ChoiceWithText:
                    var values = new List<string>();

                    var comboValue = (_currentComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                        ?? _currentComboBox?.SelectedItem?.ToString();
                    if (!string.IsNullOrEmpty(comboValue))
                        values.Add(comboValue);

                    if (!string.IsNullOrEmpty(_currentCustomTextBox?.Text))
                        values.Add(_currentCustomTextBox.Text);

                    response.SelectedValues = values;
                    break;

                case InteractionType.Confirmation:
                    // Para confirmation simples, sempre retorna sucesso se chegou aqui
                    response.Success = true;
                    break;
            }

            return response;
        }

        /// <summary>
        /// Cria painel de botões para Confirmation com opções customizadas
        /// </summary>
        private StackPanel CreateConfirmationButtonsPanel(InteractionRequest request, Window dialog, TaskCompletionSource<InteractionResponse?> tcs, ThemeColors theme)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (request.Options?.Count > 0)
            {
                foreach (var option in request.Options)
                {
                    var btn = new Button
                    {
                        Content = option.Label ?? "OK",
                        Tag = option.Value,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Center
                    };

                    if (option.IsDefault)
                    {
                        btn.Classes.Add("primary");
                    }

                    btn.Click += (_, _) =>
                    {
                        var value = option.Value ?? string.Empty;
                        var resp = new InteractionResponse
                        {
                            Success = !value.Equals("no", StringComparison.OrdinalIgnoreCase)
                                   && !value.Equals("cancel", StringComparison.OrdinalIgnoreCase)
                                   && !value.Equals("false", StringComparison.OrdinalIgnoreCase),
                            SelectedValues = new List<string> { value }
                        };
                        tcs.TrySetResult(resp);
                        dialog.Close();
                    };

                    panel.Children.Add(btn);
                }
            }

            return panel;
        }

        /// <summary>
        /// Cria conteúdo simples para Confirmation sem opções customizadas
        /// </summary>
        private Control CreateSimpleConfirmationContent(InteractionRequest request, ThemeColors theme)
        {
            // Para confirmation simples, apenas mostra a mensagem e usa botões padrão no footer
            return new Border { Height = 0 };
        }

        /// <summary>
        /// Cria ComboBox para SingleChoice
        /// </summary>
        private ComboBox CreateSingleChoiceComboBox(InteractionRequest request, ThemeColors theme)
        {
            var combo = new ComboBox
            {
                PlaceholderText = "Selecione uma opção...",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (request.Options != null)
            {
                foreach (var opt in request.Options)
                {
                    var item = new ComboBoxItem
                    {
                        Content = opt.Label,
                        Tag = opt.Value
                    };
                    combo.Items.Add(item);
                }

                var defaultIndex = request.Options.FindIndex(o => o.IsDefault);
                if (defaultIndex >= 0 && defaultIndex < combo.Items.Count)
                    combo.SelectedIndex = defaultIndex;
            }

            return combo;
        }

        /// <summary>
        /// Cria CheckBoxes para MultiChoice
        /// </summary>
        private List<CheckBox> CreateMultiChoiceCheckBoxes(InteractionRequest request, ThemeColors theme)
        {
            var checkBoxes = new List<CheckBox>();

            if (request.Options != null)
            {
                foreach (var option in request.Options)
                {
                    var cb = new CheckBox
                    {
                        Content = option.Label,
                        Tag = option.Value,
                        IsChecked = option.IsDefault,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    checkBoxes.Add(cb);
                }
            }

            return checkBoxes;
        }

        /// <summary>
        /// Cria TextBox para TextInput
        /// </summary>
        private TextBox CreateTextInputBox(InteractionRequest request, ThemeColors theme)
        {
            return new TextBox
            {
                Watermark = request.CustomInputPlaceholder ?? "Digite aqui...",
                Text = request.Message ?? string.Empty,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        /// <summary>
        /// Cria controles para ChoiceWithText
        /// </summary>
        private (ComboBox combo, TextBox textBox) CreateChoiceWithTextControls(InteractionRequest request, ThemeColors theme)
        {
            var combo = new ComboBox
            {
                PlaceholderText = "Selecione uma opção...",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (request.Options != null)
            {
                foreach (var opt in request.Options)
                {
                    combo.Items.Add(new ComboBoxItem { Content = opt.Label, Tag = opt.Value });
                }
            }

            var textBox = new TextBox
            {
                Watermark = request.CustomInputPlaceholder ?? "Ou digite um valor customizado...",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 0)
            };

            return (combo, textBox);
        }

        /// <summary>
        /// Aplica estilos Dark completos e refinados ao diálogo
        /// </summary>
        private void ApplyDarkStyles(Window dialog, ThemeColors theme)
        {
            var styles = new Styles();

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
                        new BrushTransition { Property = Button.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(120) },
                        new BrushTransition { Property = Button.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(120) },
                        new BrushTransition { Property = Button.ForegroundProperty, Duration = TimeSpan.FromMilliseconds(80) }
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
                    new Setter(Button.ForegroundProperty, new SolidColorBrush(Colors.White)),
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
                    new Setter(TextBox.SelectionForegroundBrushProperty, new SolidColorBrush(Colors.White)),
                    new Setter(TextBox.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = TextBox.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = TextBox.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(150) }
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
                        new BrushTransition { Property = TemplatedControl.ForegroundProperty, Duration = TimeSpan.FromMilliseconds(100) }
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
                        new BrushTransition { Property = ComboBox.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(150) },
                        new BrushTransition { Property = ComboBox.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(150) }
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
                    new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Colors.Transparent)),
                    new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(theme.TextPrimary)),
                    new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 8)),
                    new Setter(ListBoxItem.CornerRadiusProperty, new CornerRadius(6)),
                    new Setter(ListBoxItem.TransitionsProperty, new Transitions
                    {
                        new BrushTransition { Property = ListBoxItem.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(80) }
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

        /// <summary>
        /// Classe auxiliar para cores do tema
        /// </summary>
        private class ThemeColors
        {
            public Color Background { get; set; }
            public Color Surface { get; set; }
            public Color SurfaceHover { get; set; }
            public Color SurfacePressed { get; set; }
            public Color Primary { get; set; }
            public Color PrimaryHover { get; set; }
            public Color PrimaryPressed { get; set; }
            public Color PrimaryDisabled { get; set; }
            public Color TextPrimary { get; set; }
            public Color TextSecondary { get; set; }
            public Color TextMuted { get; set; }
            public Color TextDisabled { get; set; }
            public Color Border { get; set; }
            public Color BorderHover { get; set; }
            public Color Error { get; set; }
            public Color Success { get; set; }
            public Color Warning { get; set; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_mainWindow != null)
                {
                    _mainWindow = null;
                }
                _disposed = true;
            }
        }
    }
}