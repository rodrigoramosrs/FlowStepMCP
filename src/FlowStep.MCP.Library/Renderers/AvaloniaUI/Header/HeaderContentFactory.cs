using Avalonia.Controls;
using Avalonia.Media;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Themes;

namespace FlowStep.Renderers.AvaloniaUI.Header
{
    public static class HeaderContentFactory
    {
        public static StackPanel CreateHeaderContent(
            InteractionRequest request,
            ThemeColors theme,
            double maxWidth)
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
    }
}