using Avalonia.Controls;
using Avalonia.Layout;
using FlowStep.Models;
using FlowStep.Renderers.AvaloniaUI.Themes;

namespace FlowStep.Renderers.AvaloniaUI.Inputs
{
    public static class TextInputFactory
    {
        public static TextBox CreateTextInputBox(
            InteractionRequest request,
            ThemeColors theme)
        {
            return new TextBox
            {
                Watermark = request.CustomInputPlaceholder ?? "Digite aqui...",
                Text = string.Empty, // CORREÇÃO: Iniciar vazio, não usar request.Message
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }
    }
}