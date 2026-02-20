using Avalonia.Controls;
using Avalonia.Layout;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Themes;

namespace FlowStep.Renderers.AvaloniaUI.Inputs
{
    public static class TextInputFactory
    {
        private static TextBox? _currentTextBox = null;

        public static TextBox CreateTextInputBox(
            InteractionRequest request,
            ThemeColors theme)
        {
            var textBox = new TextBox
            {
                Watermark = request.CustomInputPlaceholder ?? "Digite aqui...",
                Text = request.Message ?? string.Empty,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            _currentTextBox = textBox;

            return textBox;
        }

        public static TextBox? GetCurrentTextBox() => _currentTextBox;
    }
}