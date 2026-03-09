using Avalonia.Controls;

namespace FlowStep.Renderers.AvaloniaUI.Factories
{
    public static class SimpleConfirmationContentFactory
    {
        public static Control CreateSimpleConfirmationContent()
        {
            // Para confirmation simples, apenas mostra a mensagem e usa botões padrão no footer
            return new Border { Height = 0 };
        }
    }
}