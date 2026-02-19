using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlowStep.Contracts;
using FlowStep.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlowStep.Renderers
{
    /// <summary>
    /// Renderizador Avalonia que exibe interações dinâmicas (notificações, prompts, progresso).
    /// Compatível com versões do Avalonia onde Close(result) pode não estar disponível:
    /// usa TaskCompletionSource para garantir que o ShowDialog retorne um InteractionResponse.
    /// </summary>
    public class AvaloniaGuiRenderer : IInteractionRenderer, IDisposable
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

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_mainWindow == null)
                {
                    _mainWindow = new MainWindow();
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
                // Permite que assinantes reajam antes de abrir o diálogo (opcional)
                if (OnInteractionRequested != null)
                {
                    // Não aguardamos o resultado do handler aqui; apenas notificamos
                    _ = OnInteractionRequested.Invoke(request);
                }

                // Garante que a UI esteja inicializada
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (_mainWindow == null)
                        await InitializeAsync();
                });

                // Cria um TCS para receber o resultado do diálogo
                var tcs = new TaskCompletionSource<InteractionResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);

                // Mostra o diálogo no thread da UI e aguarda o TCS
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var dialog = CreateDialog(request, tcs);
                    if (_mainWindow != null)
                        dialog.ShowDialog(_mainWindow); // sem atribuição
                    else
                        dialog.Show();
                });


                using (ct.Register(() =>
                {
                    // Se o token for cancelado, tenta completar o TCS com Cancelled
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
        /// Cria um Window (diálogo) e conecta handlers que completam o TaskCompletionSource com InteractionResponse.
        /// </summary>
        private Window CreateDialog(InteractionRequest request, TaskCompletionSource<InteractionResponse?> tcs)
        {
            var dialog = new Window
            {
                Width = 520,
                Height = 260,
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Interação" : request.Title,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            // Garante que, se o usuário fechar a janela pela borda, o TCS seja completado
            dialog.Closed += (_, _) =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(new InteractionResponse { Cancelled = true });
            };

            var content = new StackPanel { Spacing = 10, Margin = new Thickness(12) };

            var messageTextBlock = new TextBlock
            {
                Text = request.Message ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            content.Children.Add(messageTextBlock);

            Control? inputControl = null;

            switch (request.Type)
            {
                case InteractionType.Notification:
                    var okButton = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
                    okButton.Click += (_, _) =>
                    {
                        var resp = new InteractionResponse { Success = true };
                        tcs.TrySetResult(resp);
                        dialog.Close();
                    };
                    content.Children.Add(okButton);
                    break;

                case InteractionType.Confirmation:
                    inputControl = CreateConfirmationControls(request, dialog, tcs);
                    break;

                case InteractionType.SingleChoice:
                    inputControl = CreateSingleChoiceControl(request, dialog, tcs);
                    break;

                case InteractionType.MultiChoice:
                    inputControl = CreateMultiChoiceControl(request, dialog, tcs);
                    break;

                case InteractionType.TextInput:
                    inputControl = CreateTextInputControl(request, dialog, tcs);
                    break;

                case InteractionType.ChoiceWithText:
                    inputControl = CreateChoiceWithTextControl(request, dialog, tcs);
                    break;

                default:
                    var fallback = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
                    fallback.Click += (_, _) =>
                    {
                        var resp = new InteractionResponse { Success = true };
                        tcs.TrySetResult(resp);
                        dialog.Close();
                    };
                    content.Children.Add(fallback);
                    break;
            }

            if (inputControl != null)
                content.Children.Add(inputControl);

            dialog.Content = content;
            return dialog;
        }

        private Control CreateConfirmationControls(InteractionRequest request, Window dialog, TaskCompletionSource<InteractionResponse?> tcs)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

            if (request.Options?.Count > 0)
            {
                foreach (var option in request.Options)
                {
                    var btn = new Button { Content = option.Label ?? "OK", Tag = option.Value };
                    btn.Click += (_, _) =>
                    {
                        var value = option.Value ?? string.Empty;
                        var resp = new InteractionResponse
                        {
                            Success = !value.Equals("no", StringComparison.OrdinalIgnoreCase),
                            SelectedValues = new List<string> { value }
                        };
                        tcs.TrySetResult(resp);
                        dialog.Close();
                    };
                    panel.Children.Add(btn);
                }
            }
            else
            {
                var yes = new Button { Content = "Yes" };
                var no = new Button { Content = "No" };
                yes.Click += (_, _) =>
                {
                    tcs.TrySetResult(new InteractionResponse { Success = true });
                    dialog.Close();
                };
                no.Click += (_, _) =>
                {
                    tcs.TrySetResult(new InteractionResponse { Cancelled = true });
                    dialog.Close();
                };
                panel.Children.Add(yes);
                panel.Children.Add(no);
            }

            return panel;
        }

        private Control CreateSingleChoiceControl(InteractionRequest request, Window dialog, TaskCompletionSource<InteractionResponse?> tcs)
        {
            var panel = new StackPanel { Spacing = 8 };

            var combo = new ComboBox { Margin = new Thickness(0, 5, 0, 0) };

            if (request.Options != null && request.Options.Count > 0)
            {
                var items = new AvaloniaList<object>();
                foreach (var opt in request.Options)
                {
                    var item = new ComboBoxItem { Content = opt.Label, Tag = opt.Value };
                    items.Add(item);
                }

                combo.Items.Add(items);
                var defaultIndex = request.Options.FindIndex(o => o.IsDefault);
                if (defaultIndex >= 0 && defaultIndex < items.Count)
                    combo.SelectedIndex = defaultIndex;
            }

            panel.Children.Add(combo);

            var confirm = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
            confirm.Click += (_, _) =>
            {
                var selected = (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
                var resp = new InteractionResponse { Success = true, SelectedValues = new List<string> { selected } };
                tcs.TrySetResult(resp);
                dialog.Close();
            };
            panel.Children.Add(confirm);

            return panel;
        }

        private Control CreateMultiChoiceControl(InteractionRequest request, Window dialog, TaskCompletionSource<InteractionResponse?> tcs)
        {
            var panel = new StackPanel { Spacing = 6 };

            if (request.Options != null)
            {
                foreach (var option in request.Options)
                {
                    var cb = new CheckBox { Content = option.Label, Tag = option.Value, IsChecked = option.IsDefault };
                    panel.Children.Add(cb);
                }
            }

            var confirm = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
            confirm.Click += (_, _) =>
            {
                var selected = panel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => c.Tag?.ToString() ?? string.Empty).ToList();
                var resp = new InteractionResponse { Success = true, SelectedValues = selected };
                tcs.TrySetResult(resp);
                dialog.Close();
            };
            panel.Children.Add(confirm);

            return panel;
        }

        private Control CreateTextInputControl(InteractionRequest request, Window dialog, TaskCompletionSource<InteractionResponse?> tcs)
        {
            var panel = new StackPanel { Spacing = 6 };
            var tb = new TextBox
            {
                Text = request.Message ?? string.Empty,
                Watermark = request.CustomInputPlaceholder ?? string.Empty
            };
            panel.Children.Add(tb);

            var confirm = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
            confirm.Click += (_, _) =>
            {
                // Como nem sempre há propriedade Text/ResponseText no modelo do usuário,
                // retornamos o texto dentro de SelectedValues para compatibilidade.
                var resp = new InteractionResponse
                {
                    Success = true,
                    SelectedValues = new List<string> { tb.Text ?? string.Empty }
                };
                tcs.TrySetResult(resp);
                dialog.Close();
            };
            panel.Children.Add(confirm);

            return panel;
        }

        private Control CreateChoiceWithTextControl(InteractionRequest request, Window dialog, TaskCompletionSource<InteractionResponse?> tcs)
        {
            var panel = new StackPanel { Spacing = 6 };
            var combo = new ComboBox { Margin = new Thickness(0, 5, 0, 0) };

            if (request.Options != null)
            {
                var items = new AvaloniaList<object>();
                foreach (var opt in request.Options)
                {
                    items.Add(new ComboBoxItem { Content = opt.Label, Tag = opt.Value });
                }
                combo.Items.Add(items);
            }

            panel.Children.Add(combo);

            if (request.AllowCustomInput)
            {
                var tb = new TextBox { Watermark = request.CustomInputPlaceholder ?? string.Empty, Margin = new Thickness(0, 5, 0, 0) };
                panel.Children.Add(tb);

                var confirm = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
                confirm.Click += (_, _) =>
                {
                    var selected = (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                    var text = tb.Text ?? string.Empty;
                    var values = new List<string>();
                    if (!string.IsNullOrEmpty(selected)) values.Add(selected);
                    if (!string.IsNullOrEmpty(text)) values.Add(text);

                    var resp = new InteractionResponse
                    {
                        Success = true,
                        SelectedValues = values
                    };
                    tcs.TrySetResult(resp);
                    dialog.Close();
                };
                panel.Children.Add(confirm);
            }
            else
            {
                var confirm = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
                confirm.Click += (_, _) =>
                {
                    var selected = (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
                    var resp = new InteractionResponse { Success = true, SelectedValues = new List<string> { selected } };
                    tcs.TrySetResult(resp);
                    dialog.Close();
                };
                panel.Children.Add(confirm);
            }

            return panel;
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
                    Dispatcher.UIThread.Post(() => _mainWindow.Close());
                    _mainWindow = null;
                }
                _disposed = true;
            }
        }
    }
}
