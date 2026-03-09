using Avalonia.Controls;
using Avalonia.Layout;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Themes;
using System.Collections.Generic;

namespace FlowStep.Renderers.AvaloniaUI.Inputs
{
    public static class MultiChoiceInputFactory
    {
        private static List<CheckBox>? _currentCheckBoxes = null;

        public static List<CheckBox> CreateMultiChoiceCheckBoxes(
            InteractionRequest request,
            ThemeColors theme)
        {
            var checkBoxes = new List<CheckBox>();

            if (request.Options != null)
            {
                foreach (var option in request.Options)
                {
                    var cb = new CheckBox
                    {
                        Content = option.Label,
                        Tag = option.Value,
                        IsChecked = option.IsDefault,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    checkBoxes.Add(cb);
                }
            }

            _currentCheckBoxes = checkBoxes;

            return checkBoxes;
        }

        public static List<CheckBox>? GetCurrentCheckBoxes() => _currentCheckBoxes;
    }
}