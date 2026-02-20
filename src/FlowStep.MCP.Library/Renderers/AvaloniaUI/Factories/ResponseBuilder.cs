using Avalonia.Controls;
using FlowStep.Models;
using System.Collections.Generic;
using System.Linq;

namespace FlowStep.Renderers.AvaloniaUI.Factories
{
    public static class ResponseBuilder
    {
        // Referências estáticas para acessar os controles criados
        private static ComboBox? _currentComboBox;
        private static List<CheckBox>? _currentCheckBoxes;
        private static TextBox? _currentTextBox;
        private static TextBox? _currentCustomTextBox;

        public static void SetCurrentControls(
            ComboBox? comboBox,
            List<CheckBox>? checkBoxes,
            TextBox? textBox,
            TextBox? customTextBox)
        {
            _currentComboBox = comboBox;
            _currentCheckBoxes = checkBoxes;
            _currentTextBox = textBox;
            _currentCustomTextBox = customTextBox;
        }

        public static InteractionResponse BuildResponseFromCurrentState(InteractionType type)
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
    }
}