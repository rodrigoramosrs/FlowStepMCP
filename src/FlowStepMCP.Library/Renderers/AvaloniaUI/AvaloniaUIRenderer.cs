using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FlowStep.Contracts;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Header;
using FlowStep.Renderers.AvaloniaUI.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlowStep.Renderers.AvaloniaUI
{
    public class AvaloniaUIRenderer : IInteractionRenderer, IDisposable
    {
        private readonly object _lock = new();
        private MainWindow? _mainWindow;
        private bool _disposed = false;

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

                var tcs = new TaskCompletionSource<InteractionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

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

        private Window CreateDialog(InteractionRequest request, TaskCompletionSource<InteractionResponse> tcs)
        {
            var theme = ThemeColors.CreateDefaultDark();

            const double MAX_WIDTH = 600;
            const double MIN_WIDTH = 360;
            const double MAX_HEIGHT = 700;
            const double MIN_HEIGHT = 250;

            // Controles que precisam ser acessados para obter valores
            ComboBox? comboBox = null;
            List<CheckBox>? checkBoxes = null;
            TextBox? textBox = null;
            TextBox? customTextBox = null;

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
                SizeToContent = SizeToContent.Manual,
                Width = 480,
                Height = Math.Min(560, Math.Max(MIN_HEIGHT, EstimateWindowHeight(request)))
            };

            dialog.Closed += (_, _) =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(new InteractionResponse { Cancelled = true });
            };

            // Layout principal: Grid com 3 linhas (Header, Content, Footer)
            var mainGrid = new Grid
            {
                RowDefinitions = new RowDefinitions
                {
                    new RowDefinition { Height = GridLength.Auto },    // Header
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // Content
                    new RowDefinition { Height = GridLength.Auto }     // Footer
                }
            };

            // === HEADER ===
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 16, 20, 12),
                Child = HeaderContentFactory.CreateHeaderContent(request, theme, MAX_WIDTH)
            };

            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // === CONTENT E FOOTER ===
            if (request.Type == InteractionType.Notification)
            {
                var emptyContent = new Border
                {
                    Background = new SolidColorBrush(theme.Background),
                    MinHeight = 40
                };
                Grid.SetRow(emptyContent, 1);
                mainGrid.Children.Add(emptyContent);

                var notificationFooter = CreateNotificationFooter(tcs, dialog, theme);
                Grid.SetRow(notificationFooter, 2);
                mainGrid.Children.Add(notificationFooter);
            }
            else if (request.Type == InteractionType.Confirmation && request.Options?.Count > 0)
            {
                var confirmationPanel = CreateConfirmationButtonsPanel(request, dialog, tcs, theme);

                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Padding = new Thickness(20, 16, 20, 16),
                    Background = new SolidColorBrush(theme.Background),
                    Content = confirmationPanel,
                    AllowAutoHide = false
                };

                Grid.SetRow(scrollViewer, 1);
                mainGrid.Children.Add(scrollViewer);

                var emptyFooter = new Border { Height = 0 };
                Grid.SetRow(emptyFooter, 2);
                mainGrid.Children.Add(emptyFooter);
            }
            else
            {
                // Demais tipos: inputs em ScrollViewer + footer fixo
                var contentPanel = new StackPanel
                {
                    Spacing = 12,
                };

                // Criar inputs específicos
                switch (request.Type)
                {
                    case InteractionType.Confirmation:
                        contentPanel.Children.Add(new Border { Height = 0 });
                        break;

                    case InteractionType.SingleChoice:
                        comboBox = CreateSingleChoiceComboBox(request, theme);
                        contentPanel.Children.Add(CreateWrappingLabel("Selecione uma opção:", theme));
                        contentPanel.Children.Add(comboBox);
                        break;

                    case InteractionType.MultiChoice:
                        checkBoxes = CreateMultiChoiceCheckBoxes(request, theme);
                        contentPanel.Children.Add(CreateWrappingLabel("Selecione uma ou mais opções:", theme));
                        foreach (var cb in checkBoxes)
                        {
                            contentPanel.Children.Add(cb);
                        }
                        break;

                    case InteractionType.TextInput:
                        // CORREÇÃO: Usar TextBlock com TextWrapping para a mensagem/instrução
                        // e TextBox multiline para entrada do usuário
                        textBox = CreateMultilineTextInputBox(request, theme);

                        // Adicionar a mensagem como label quebrando linha
                        if (!string.IsNullOrWhiteSpace(request.Message))
                        {
                            contentPanel.Children.Add(CreateWrappingLabel(request.Message, theme));
                        }

                        contentPanel.Children.Add(textBox);
                        break;

                    case InteractionType.ChoiceWithText:
                        var (combo, txtBox) = CreateChoiceWithTextControls(request, theme);
                        comboBox = combo;
                        customTextBox = txtBox;
                        contentPanel.Children.Add(CreateWrappingLabel("Selecione uma opção:", theme));
                        contentPanel.Children.Add(comboBox);
                        contentPanel.Children.Add(CreateWrappingLabel("Ou digite um valor personalizado:", theme));
                        contentPanel.Children.Add(customTextBox);
                        break;
                }

                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Padding = new Thickness(20, 16, 20, 16),
                    Background = new SolidColorBrush(theme.Background),
                    Content = contentPanel,
                    AllowAutoHide = false
                };

                Grid.SetRow(scrollViewer, 1);
                mainGrid.Children.Add(scrollViewer);

                var standardFooter = CreateStandardFooter(tcs, dialog, theme, request.Type, comboBox, checkBoxes, textBox, customTextBox);
                Grid.SetRow(standardFooter, 2);
                mainGrid.Children.Add(standardFooter);
            }

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
                    },
                    new[]{ new BoxShadow
                    {
                        OffsetX = 0,
                        OffsetY = 8,
                        Blur = 32,
                        Spread = 0,
                        Color = new Color(0x60, 0x00, 0x00, 0x00)
                    }}
                ),
                Child = mainGrid
            };

            dialog.Content = rootContainer;
            Styles.DarkThemeStyles.ApplyDarkStyles(dialog, theme);

            return dialog;
        }

        // NOVO: Label com quebra de linha automática
        private TextBlock CreateWrappingLabel(string text, ThemeColors theme)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(theme.TextSecondary),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap,        // QUEBRA DE LINHA AUTOMÁTICA
                MaxWidth = 440,                          // Largura máxima para garantir wrapping
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private TextBox CreateMultilineTextInputBox(InteractionRequest request, ThemeColors theme)
        {
            var textBox = new TextBox
            {
                Watermark = request.CustomInputPlaceholder ?? "Digite aqui...",
                Text = string.Empty,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 80,
                MaxHeight = 200,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalContentAlignment = VerticalAlignment.Top,
                Padding = new Thickness(12, 8)
            };

            // Definir attached properties usando SetValue
            textBox.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            textBox.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);

            return textBox;
        }

        private double EstimateWindowHeight(InteractionRequest request)
        {
            var baseHeight = 200;
            var contentHeight = request.Type switch
            {
                InteractionType.Notification => 80,
                InteractionType.Confirmation => 100 + (request.Options?.Count ?? 0) * 50,
                InteractionType.SingleChoice => 140,
                InteractionType.MultiChoice => 140 + (request.Options?.Count ?? 0) * 40,
                InteractionType.TextInput => 220,  // Aumentado para acomodar TextBox multiline
                InteractionType.ChoiceWithText => 280,
                _ => 200
            };
            return Math.Min(700, baseHeight + contentHeight);
        }

        private Border CreateNotificationFooter(TaskCompletionSource<InteractionResponse> tcs, Window dialog, ThemeColors theme)
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
                Classes = { "primary" },
                MinWidth = 100,
                MinHeight = 36
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
                Padding = new Thickness(20, 12, 20, 16),
                Child = panel
            };
        }

        private Border CreateStandardFooter(
            TaskCompletionSource<InteractionResponse> tcs,
            Window dialog,
            ThemeColors theme,
            InteractionType type,
            ComboBox? comboBox,
            List<CheckBox>? checkBoxes,
            TextBox? textBox,
            TextBox? customTextBox)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 12
            };

            var cancelBtn = new Button
            {
                Content = "Cancelar",
                MinWidth = 90,
                MinHeight = 36
            };

            var confirmBtn = new Button
            {
                Content = "Confirmar",
                Classes = { "primary" },
                MinWidth = 100,
                MinHeight = 36,
                IsDefault = true
            };

            cancelBtn.Click += (_, _) =>
            {
                tcs.TrySetResult(new InteractionResponse { Cancelled = true });
                dialog.Close();
            };

            confirmBtn.Click += (_, _) =>
            {
                var response = BuildResponse(type, comboBox, checkBoxes, textBox, customTextBox);
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
                Padding = new Thickness(20, 12, 20, 16),
                Child = panel
            };
        }

        private InteractionResponse BuildResponse(
            InteractionType type,
            ComboBox? comboBox,
            List<CheckBox>? checkBoxes,
            TextBox? textBox,
            TextBox? customTextBox)
        {
            var response = new InteractionResponse { Success = true };

            try
            {
                switch (type)
                {
                    case InteractionType.SingleChoice:
                        var selectedValue = GetComboBoxSelectedValue(comboBox);
                        response.SelectedValues = new List<string> { selectedValue ?? string.Empty };
                        break;

                    case InteractionType.MultiChoice:
                        response.SelectedValues = checkBoxes?
                            .Where(c => c.IsChecked == true)
                            .Select(c => c.Tag?.ToString() ?? c.Content?.ToString() ?? string.Empty)
                            .ToList() ?? new List<string>();
                        break;

                    case InteractionType.TextInput:
                        // CORREÇÃO: Pegar Text que pode conter múltiplas linhas
                        response.TextValue = textBox?.Text ?? string.Empty;
                        response.SelectedValues = new List<string> { response.TextValue };
                        break;

                    case InteractionType.ChoiceWithText:
                        var values = new List<string>();
                        var comboValue = GetComboBoxSelectedValue(comboBox);

                        if (!string.IsNullOrEmpty(comboValue))
                            values.Add(comboValue);

                        if (!string.IsNullOrEmpty(customTextBox?.Text))
                        {
                            values.Add(customTextBox.Text);
                            response.CustomInput = customTextBox.Text;
                        }

                        response.SelectedValues = values;
                        break;

                    case InteractionType.Confirmation:
                        response.Success = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao construir resposta: {ex.Message}");
                response.Success = false;
            }

            return response;
        }

        private string? GetComboBoxSelectedValue(ComboBox? comboBox)
        {
            if (comboBox?.SelectedItem == null) return null;

            if (comboBox.SelectedItem is ComboBoxItem item)
                return item.Tag?.ToString() ?? item.Content?.ToString();

            return comboBox.SelectedItem.ToString();
        }

        private StackPanel CreateConfirmationButtonsPanel(InteractionRequest request, Window dialog, TaskCompletionSource<InteractionResponse> tcs, ThemeColors theme)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 10,
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
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        MinHeight = 44,
                        Margin = new Thickness(0, 2, 0, 2)
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

        private ComboBox CreateSingleChoiceComboBox(InteractionRequest request, ThemeColors theme)
        {
            var combo = new ComboBox
            {
                PlaceholderText = "Clique para selecionar...",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 44
            };

            if (request.Options != null)
            {
                foreach (var opt in request.Options)
                {
                    var item = new ComboBoxItem
                    {
                        Content = opt.Label,
                        Tag = opt.Value,
                        MinHeight = 36
                    };
                    combo.Items.Add(item);
                }

                var defaultIndex = request.Options.FindIndex(o => o.IsDefault);
                if (defaultIndex >= 0 && defaultIndex < combo.Items.Count)
                    combo.SelectedIndex = defaultIndex;
            }

            return combo;
        }

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
                        HorizontalAlignment = HorizontalAlignment.Left,
                        MinHeight = 36,
                        Margin = new Thickness(0, 4, 0, 4)
                    };
                    checkBoxes.Add(cb);
                }
            }

            return checkBoxes;
        }

        private (ComboBox combo, TextBox textBox) CreateChoiceWithTextControls(InteractionRequest request, ThemeColors theme)
        {
            var combo = new ComboBox
            {
                PlaceholderText = "Clique para selecionar...",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 44
            };

            if (request.Options != null)
            {
                foreach (var opt in request.Options)
                {
                    combo.Items.Add(new ComboBoxItem
                    {
                        Content = opt.Label,
                        Tag = opt.Value,
                        MinHeight = 36
                    });
                }
            }

            var textBox = new TextBox
            {
                Watermark = request.CustomInputPlaceholder ?? "Digite um valor personalizado...",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 44,
                Margin = new Thickness(0, 4, 0, 0),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap
            };

            return (combo, textBox);
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
                _mainWindow = null;
                _disposed = true;
            }
        }
    }
}