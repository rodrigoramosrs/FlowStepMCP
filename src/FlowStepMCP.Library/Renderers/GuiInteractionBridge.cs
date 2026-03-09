using System;
using System.Threading;
using System.Threading.Tasks;
using FlowStep.Contracts;
using FlowStep.Models;

namespace FlowStep.Renderers
{
    // Eventos que a UI (WPF/Blazor) deve escutar
    public class GuiInteractionBridge : IInteractionRenderer
    {
        // Evento disparado quando o sistema precisa de algo do usuário
        public event Func<InteractionRequest, Task<InteractionResponse>>? OnInteractionRequested;

        // Evento para atualizar barras de progresso na UI
        public event Action<string, int, int, string>? OnProgressUpdate;
        public event Action<string>? OnProgressEnd;

        public async Task<InteractionResponse> RenderAsync(InteractionRequest request, CancellationToken ct)
        {
            if (OnInteractionRequested == null)
                throw new InvalidOperationException("Nenhuma UI conectada ao GuiInteractionBridge.");

            // Invoca a UI (deve ser marshalled para a UI Thread pela implementação do evento)
            return await OnInteractionRequested.Invoke(request);
        }

        public void ReportProgress(string operationId, int current, int total, string status)
        {
            OnProgressUpdate?.Invoke(operationId, current, total, status);
        }

        public void EndProgress(string operationId)
        {
            OnProgressEnd?.Invoke(operationId);
        }
    }
}