using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Themes;


namespace FlowStep.Renderers.AvaloniaUI.Inputs
{
    public static class ChoiceWithTextInputFactory
    {
        private static ComboBox? _currentComboBox = null;
        private static TextBox? _currentCustomTextBox = null;

        public static (ComboBox combo, TextBox textBox) CreateChoiceWithTextControls(
            InteractionRequest request,
            ThemeColors theme)
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

            _currentComboBox = combo;
            _currentCustomTextBox = textBox;

            return (combo, textBox);
        }

        public static ComboBox? GetCurrentComboBox() => _currentComboBox;
        public static TextBox? GetCurrentCustomTextBox() => _currentCustomTextBox;
    }
}