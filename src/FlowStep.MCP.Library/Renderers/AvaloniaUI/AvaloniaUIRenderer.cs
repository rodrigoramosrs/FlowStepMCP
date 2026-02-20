using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    /// <summary>
    /// Renderizador Avalonia que exibe interações dinâmicas (notificações, prompts, progresso).
    /// Compatível com versões do Avalonia onde Close(result) pode não estar disponível:
    /// usa TaskCompletionSource para garantir que o ShowDialog retorne um InteractionResponse.
    /// </summary>
    public class AvaloniaUIRenderer : IInteractionRenderer, IDisposable
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

            var theme = ThemeColors.CreateDefaultDark();

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
                Child = HeaderContentFactory.CreateHeaderContent(request, theme, MAX_WIDTH)
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

                var notificationFooter = Footer.NotificationFooterFactory.CreateNotificationFooter(tcs, dialog, theme);
                Grid.SetRow(notificationFooter, 2);
                mainGrid.Children.Add(notificationFooter);
            }
            else if (request.Type == InteractionType.Confirmation && request.Options?.Count > 0)
            {
                // Confirmation com opções customizadas: botões são o próprio conteúdo
                var confirmationPanel = Factories.ConfirmationButtonsFactory.CreateConfirmationButtonsPanel(request, dialog, tcs, theme);

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
                        var confirmationContent = Factories.SimpleConfirmationContentFactory.CreateSimpleConfirmationContent();
                        contentPanel.Children.Add(confirmationContent);
                        break;

                    case InteractionType.SingleChoice:
                        _currentComboBox = Inputs.SingleChoiceInputFactory.CreateSingleChoiceComboBox(request, theme);
                        contentPanel.Children.Add(_currentComboBox);
                        break;

                    case InteractionType.MultiChoice:
                        _currentCheckBoxes = Inputs.MultiChoiceInputFactory.CreateMultiChoiceCheckBoxes(request, theme);
                        foreach (var cb in _currentCheckBoxes)
                        {
                            contentPanel.Children.Add(cb);
                        }
                        break;

                    case InteractionType.TextInput:
                        _currentTextBox = Inputs.TextInputFactory.CreateTextInputBox(request, theme);
                        contentPanel.Children.Add(_currentTextBox);
                        break;

                    case InteractionType.ChoiceWithText:
                        var (combo, textBox) = Inputs.ChoiceWithTextInputFactory.CreateChoiceWithTextControls(request, theme);
                        _currentComboBox = combo;
                        _currentCustomTextBox = textBox;
                        contentPanel.Children.Add(_currentComboBox);
                        contentPanel.Children.Add(_currentCustomTextBox);
                        break;
                }

                // Atualizar ResponseBuilder com controles atuais
                Factories.ResponseBuilder.SetCurrentControls(
                    _currentComboBox,
                    _currentCheckBoxes,
                    _currentTextBox,
                    _currentCustomTextBox);

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
                var standardFooter = Footer.StandardFooterFactory.CreateStandardFooter(tcs, dialog, theme, request.Type);
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

            Styles.DarkThemeStyles.ApplyDarkStyles(dialog, theme);

            return dialog;
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