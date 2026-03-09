using System;
using System.Threading;
using System.Threading.Tasks;
using FlowStep.Models;

namespace FlowStep.Contracts
{
    // Interface Principal para o Desenvolvedor usar
    public interface IFlowStepService
    {
        Task<InteractionResponse> InteractAsync(InteractionRequest request, CancellationToken ct = default);
        IProgress<(int Current, int Total, string Status)> CreateProgress(string operationName, int total);
    }
}