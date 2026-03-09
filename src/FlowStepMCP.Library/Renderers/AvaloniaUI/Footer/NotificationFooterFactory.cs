using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Themes;

namespace FlowStep.Renderers.AvaloniaUI.Footer
{
    public static class NotificationFooterFactory
    {
        public static Border CreateNotificationFooter(
            TaskCompletionSource<InteractionResponse?> tcs,
            Window dialog,
            ThemeColors theme)
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
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 16, 24, 20),
                Child = panel
            };
        }
    }
}