using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Factories;
using FlowStep.Renderers.AvaloniaUI.Themes;

namespace FlowStep.Renderers.AvaloniaUI.Footer
{
    public static class StandardFooterFactory
    {
        public static Border CreateStandardFooter(
            TaskCompletionSource<InteractionResponse?> tcs,
            Window dialog,
            ThemeColors theme,
            InteractionType type)
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
                var response = ResponseBuilder.BuildResponseFromCurrentState(type);
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
    }
}