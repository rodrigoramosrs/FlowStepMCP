using System;
using System.Threading;
using System.Threading.Tasks;
using FlowStep.Contracts;
using FlowStep.Models;
using Microsoft.Extensions.Logging;

namespace FlowStep.Services
{
    public class FlowStepService : IFlowStepService
    {
        private readonly IInteractionRenderer _renderer;
        private readonly ILogger<FlowStepService> _logger;

        public FlowStepService(IInteractionRenderer renderer, ILogger<FlowStepService> logger)
        {
            _renderer = renderer;
            _logger = logger;
        }

        public async Task<InteractionResponse> InteractAsync(InteractionRequest request, CancellationToken ct = default)
        {
            _logger.LogInformation("Interaction Request: {Type} - {Message}", request.Type, request.Message);

            // Cria um token vinculado para gerenciar o Timeout da requisição específica
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (request.Timeout.HasValue)
            {
                timeoutCts.CancelAfter(request.Timeout.Value);
            }

            try
            {
                var response = await _renderer.RenderAsync(request, timeoutCts.Token);

                // Pós-processamento e Validação Básica
                if (response.TimedOut)
                {
                    _logger.LogWarning("Interaction timed out.");
                }

                return response;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Interaction cancelled by timeout or user.");
                return new InteractionResponse { Cancelled = true, TimedOut = timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested };
            }
        }

        public IProgress<(int Current, int Total, string Status)> CreateProgress(string operationName, int total)
        {
            var operationId = Guid.NewGuid().ToString();
            return new Progress<(int, int, string)>(val =>
            {
                _renderer.ReportProgress(operationId, val.Item1, val.Item2, val.Item3);
            });
        }
    }
}