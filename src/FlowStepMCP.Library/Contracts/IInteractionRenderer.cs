using System.Threading;
using System.Threading.Tasks;
using FlowStep.Models;

namespace FlowStep.Contracts
{
    // Interface para a "View" (CLI, WPF, Web)
    public interface IInteractionRenderer
    {
        Task<InteractionResponse> RenderAsync(InteractionRequest request, CancellationToken ct);
        void ReportProgress(string operationId, int current, int total, string status);
        void EndProgress(string operationId);
    }
}