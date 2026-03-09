using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Themes;

namespace FlowStep.Renderers.AvaloniaUI.Factories
{
    public static class ConfirmationButtonsFactory
    {
        public static StackPanel CreateConfirmationButtonsPanel(
            InteractionRequest request,
            Window dialog,
            TaskCompletionSource<InteractionResponse?> tcs,
            ThemeColors theme)
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
    }
}