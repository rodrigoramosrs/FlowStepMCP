using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Themes;
using System.Collections.Generic;
using System.Linq;

namespace FlowStep.Renderers.AvaloniaUI.Footer
{
    public static class StandardFooterFactory
    {
        public static Border CreateStandardFooter(
            TaskCompletionSource<InteractionResponse> tcs,
            Window dialog,
            ThemeColors theme,
            InteractionType type,
            ComboBox? comboBox = null,
            List<CheckBox>? checkBoxes = null,
            TextBox? textBox = null,
            TextBox? customTextBox = null)
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
                Padding = new Thickness(24, 16, 24, 20),
                Child = panel
            };
        }

        private static InteractionResponse BuildResponse(
            InteractionType type,
            ComboBox? comboBox,
            List<CheckBox>? checkBoxes,
            TextBox? textBox,
            TextBox? customTextBox)
        {
            var response = new InteractionResponse { Success = true };

            switch (type)
            {
                case InteractionType.SingleChoice:
                    var selectedValue = GetComboBoxValue(comboBox);
                    response.SelectedValues = new List<string> { selectedValue ?? string.Empty };
                    break;

                case InteractionType.MultiChoice:
                    response.SelectedValues = checkBoxes?
                        .Where(c => c.IsChecked == true)
                        .Select(c => c.Tag?.ToString() ?? c.Content?.ToString() ?? string.Empty)
                        .ToList() ?? new List<string>();
                    break;

                case InteractionType.TextInput:
                    response.TextValue = textBox?.Text ?? string.Empty;
                    response.SelectedValues = new List<string> { response.TextValue };
                    break;

                case InteractionType.ChoiceWithText:
                    var values = new List<string>();
                    var comboValue = GetComboBoxValue(comboBox);

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

            return response;
        }

        private static string? GetComboBoxValue(ComboBox? comboBox)
        {
            if (comboBox?.SelectedItem == null) return null;

            if (comboBox.SelectedItem is ComboBoxItem item)
                return item.Tag?.ToString() ?? item.Content?.ToString();

            return comboBox.SelectedItem.ToString();
        }
    }
}