using Avalonia.Controls;
using Avalonia.Layout;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Themes;
using System.Collections.Generic;

namespace FlowStep.Renderers.AvaloniaUI.Inputs
{
    public static class SingleChoiceInputFactory
    {
        private static ComboBox? _currentComboBox = null;

        public static ComboBox CreateSingleChoiceComboBox(
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
                    var item = new ComboBoxItem
                    {
                        Content = opt.Label,
                        Tag = opt.Value
                    };
                    combo.Items.Add(item);
                }

                var defaultIndex = request.Options.FindIndex(o => o.IsDefault);
                if (defaultIndex >= 0 && defaultIndex < combo.Items.Count)
                    combo.SelectedIndex = defaultIndex;
            }

            _currentComboBox = combo;

            return combo;
        }

        public static ComboBox? GetCurrentComboBox() => _currentComboBox;
    }
}