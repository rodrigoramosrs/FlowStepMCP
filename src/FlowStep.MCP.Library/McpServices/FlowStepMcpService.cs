using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowStep.Models;
using FlowStep.Services;
using FlowStep.Contracts;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FlowStep.McpServices
{
    /// <summary>
    /// Serviço MCP para interações com usuário via FlowStep
    /// </summary>
    [McpServerToolType]
    public class FlowStepMcpService
    {
        private readonly IFlowStepService _flowStepService;
        private readonly ILogger<FlowStepMcpService> _logger;

        public FlowStepMcpService(IFlowStepService flowStepService, ILogger<FlowStepMcpService> logger)
        {
            _flowStepService = flowStepService;
            _logger = logger;
        }

        /// <summary>
        /// Exibe uma notificação simples para o usuário, podendo aguardar confirmação.
        /// </summary>
        /// <param name="message">Mensagem a ser exibida</param>
        /// <param name="title">Título da notificação (opcional, padrão: "Sistema")</param>
        /// <param name="waitConfirmation">
        /// Se true, aguarda confirmação do usuário. Útil para ações críticas.
        /// Se false (padrão), não bloqueia o progresso da task — ideal para notificações informativas.
        /// </param>
        /// <returns>Status da operação</returns>
        [McpServerTool]
        [Description("Exibe uma notificação simples para o usuário com um título e mensagem. Pode aguardar confirmação do usuário ou ser não bloqueante (padrão).")]
        public async Task<string> NotifyUserAsync(
            [Description("Mensagem a ser exibida ao usuário")]
    string message,
            [Description("Título da notificação (opcional, padrão: 'Sistema')")]
    string title,
            [Description("Se true, aguarda confirmação do usuário. Padrão: false (notificação não bloqueante).")]
    bool waitConfirmation = false)
        {
            _logger.LogInformation(
                "Notificação solicitada: {Title} - {Message} | Aguardar Confirmação: {Wait}",
                title ?? "Sistema",
                message,
                waitConfirmation);

            var request = new InteractionRequest
            {
                Title = title ?? "Sistema",
                Message = message,
                Type = waitConfirmation ? InteractionType.Confirmation : InteractionType.Notification
            };

            if (!waitConfirmation)
            {
                Task.Run(() => _flowStepService.InteractAsync(request));
                return $"Notificação exibida. Usuário não confirmou.";
            }
            else
            {
                var response = await _flowStepService.InteractAsync(request);

                if (response.Success)
                {
                    return $"Notificação {(waitConfirmation ? "confirmada" : "exibida")} com sucesso: {message}";
                }
            }


            return $"Falha ao exibir notificação.";
        }


        /// <summary>
        /// Solicita confirmação do usuário (Sim/Não)
        /// </summary>
        /// <param name="message">Mensagem de confirmação</param>
        /// <param name="title">Título da confirmação (opcional)</param>
        /// <param name="isCancellable">Se pode ser cancelado (opcional, padrão: true)</param>
        /// <returns>Resposta do usuário: "yes" ou "no"</returns>
        [McpServerTool]
        [Description("Solicita confirmação do usuário com uma mensagem. Retorna 'yes' se confirmado ou 'no' se rejeitado. Útil para ações críticas ou decisões importantes.")]
        public async Task<string> ConfirmAsync(
            [Description("Mensagem de confirmação para o usuário")]
            string message,
            [Description("Título da confirmação (opcional)")]
            string title,
            [Description("Indica se a operação pode ser cancelada pelo usuário (opcional, padrão: true)")]
            bool isCancellable = true)
        {
            _logger.LogInformation("Confirmação solicitada: {Title} - {Message}", title ?? "Sistema", message);

            var request = new InteractionRequest
            {
                Title = title ?? "Sistema",
                Message = message,
                Type = InteractionType.Confirmation,
                IsCancellable = isCancellable
            };

            var response = await _flowStepService.InteractAsync(request);

            if (response.Cancelled)
            {
                return "cancelled";
            }

            if (response.SelectedValues != null && response.SelectedValues.Contains("yes"))
            {
                return "yes";
            }

            return "no";
        }

        /// <summary>
        /// Permite que o usuário escolha uma opção entre várias
        /// </summary>
        /// <param name="message">Mensagem de escolha</param>
        /// <param name="options">Lista de opções disponíveis</param>
        /// <param name="title">Título da escolha (opcional)</param>
        /// <param name="allowCustomInput">Permite opção personalizada (opcional, padrão: false)</param>
        /// <returns>Valor da opção selecionada</returns>
        [McpServerTool]
        [Description("Permite que o usuário escolha uma opção entre várias disponíveis. Retorna o valor da opção selecionada. Útil para seleções simples de usuário.")]
        public async Task<string> ChooseOptionAsync(
            [Description("Mensagem descrevendo as opções disponíveis")]
            string message,
            [Description("Lista de opções disponíveis para seleção. Cada opção tem Label, Value e pode ter IsDefault")]
            List<InteractionOption> options,
            [Description("Título da escolha (opcional)")]
            string title,
            [Description("Permite que o usuário digite uma opção personalizada (opcional, padrão: false)")]
            bool allowCustomInput = false)
        {
            _logger.LogInformation("Escolha solicitada: {Title} - {Message}", title ?? "Sistema", message);

            var request = new InteractionRequest
            {
                Title = title ?? "Sistema",
                Message = message,
                Type = allowCustomInput ? InteractionType.ChoiceWithText : InteractionType.SingleChoice,
                Options = options,
                AllowCustomInput = allowCustomInput
            };

            var response = await _flowStepService.InteractAsync(request);

            if (response.Cancelled)
            {
                return "cancelled";
            }

            if (response.SelectedValues != null && response.SelectedValues.Count > 0)
            {
                return response.SelectedValues[0];
            }

            if (response.CustomInput != null)
            {
                return $"custom:{response.CustomInput}";
            }

            return "cancelled";
        }

        /// <summary>
        /// Permite que o usuário selecione múltiplas opções
        /// </summary>
        /// <param name="message">Mensagem de seleção múltipla</param>
        /// <param name="options">Lista de opções disponíveis</param>
        /// <param name="minSelections">Mínimo de seleções obrigatórias (opcional, padrão: 0)</param>
        /// <param name="maxSelections">Máximo de seleções permitidas (opcional, padrão: 1)</param>
        /// <param name="title">Título da seleção (opcional)</param>
        /// <returns>Lista de valores das opções selecionadas</returns>
        [McpServerTool]
        [Description("Permite que o usuário selecione múltiplas opções entre várias disponíveis. Retorna uma lista com os valores das opções selecionadas. Útil para seleções múltiplas como filtros ou múltiplos itens.")]
        public async Task<List<string>> ChooseMultipleOptionsAsync(
            [Description("Título da seleção (opcional)")]
            string title,
            [Description("Mensagem descrevendo as opções disponíveis")]
            string message,
            [Description("Lista de opções disponíveis para seleção. Cada opção tem Label, Value e pode ter IsDefault")]
            List<InteractionOption> options,
            [Description("Número mínimo de seleções obrigatórias (opcional, padrão: 0)")]
            int minSelections = 0,
            [Description("Número máximo de seleções permitidas (opcional, padrão: 1)")]
            int maxSelections = 1)
        {
            _logger.LogInformation("Seleção múltipla solicitada: {Title} - {Message}", title ?? "Sistema", message);

            var request = new InteractionRequest
            {
                Title = title ?? "Sistema",
                Message = message,
                Type = InteractionType.MultiChoice,
                Options = options,
                MinSelections = minSelections,
                MaxSelections = maxSelections
            };

            var response = await _flowStepService.InteractAsync(request);

            if (response.Cancelled)
            {
                return new List<string>();
            }

            if (response.SelectedValues != null && response.SelectedValues.Count > 0)
            {
                return response.SelectedValues;
            }

            if (response.CustomInput != null)
            {
                return new List<string> { $"custom:{response.CustomInput}" };
            }

            return new List<string>();
        }

        /// <summary>
        /// Solicita entrada de texto livre do usuário
        /// </summary>
        /// <param name="message">Instrução para o usuário</param>
        /// <param name="title">Título do input (opcional)</param>
        /// <param name="placeholder">Texto de placeholder (opcional, padrão: "Digite aqui...")</param>
        /// <returns>O texto digitado pelo usuário</returns>
        [McpServerTool]
        [Description("Solicita que o usuário digite um texto livre. Retorna o texto digitado pelo usuário. Útil para inputs de dados como nome, descrição, comentários, etc.")]
        public async Task<string> AskUserForTextAsync(
            [Description("Instrução ou mensagem para o usuário")]
            string message,
            [Description("Título do campo de texto (opcional)")]
            string? title,
            [Description("Texto que aparecerá no campo de entrada (opcional, padrão: 'Digite aqui...')")]
            string placeholder)
        {
            _logger.LogInformation("Input de texto solicitado: {Title} - {Message}", title ?? "Sistema", message);

            var request = new InteractionRequest
            {
                Title = title ?? "Sistema",
                Message = message,
                Type = InteractionType.TextInput,
                CustomInputPlaceholder = placeholder ?? "Digite aqui..."
            };

            var response = await _flowStepService.InteractAsync(request);

            if (response.Success && response.TextValue != null)
            {
                return response.TextValue;
            }

            return "";
        }

        /// <summary>
        /// Solicita que o usuário escolha uma opção e digite um texto personalizado
        /// </summary>
        /// <param name="message">Mensagem de instrução</param>
        /// <param name="options">Lista de opções disponíveis</param>
        /// <param name="title">Título da interação (opcional)</param>
        /// <param name="placeholder">Texto de placeholder para o campo personalizado (opcional)</param>
        /// <returns>Valor da opção ou texto personalizado</returns>
        [McpServerTool]
        [Description("Permite que o usuário escolha uma opção e digite um texto personalizado. Retorna a opção selecionada ou o texto personalizado. Útil para cenários onde o usuário pode escolher entre opções predefinidas ou fornecer uma resposta personalizada.")]
        public async Task<string> ChooseWithCustomTextAsync(
            [Description("Mensagem de instrução para o usuário")]
            string message,
            [Description("Lista de opções disponíveis para seleção. Cada opção tem Label, Value e pode ter IsDefault")]
            List<InteractionOption> options,
            [Description("Título da interação (opcional)")]
            string title,
            [Description("Texto de placeholder para o campo de texto personalizado (opcional)")]
            string placeholder)
        {
            _logger.LogInformation("Escolha com texto personalizado solicitada: {Title} - {Message}", title ?? "Sistema", message);

            var request = new InteractionRequest
            {
                Title = title ?? "Sistema",
                Message = message,
                Type = InteractionType.ChoiceWithText,
                Options = options,
                AllowCustomInput = true,
                CustomInputPlaceholder = placeholder ?? "Digite aqui..."
            };

            var response = await _flowStepService.InteractAsync(request);

            if (response.Cancelled)
            {
                return "cancelled";
            }

            if (response.CustomInput != null)
            {
                return $"custom:{response.CustomInput}";
            }

            if (response.SelectedValues != null && response.SelectedValues.Count > 0)
            {
                return response.SelectedValues[0];
            }

            return "cancelled";
        }

        /// <summary>
        /// Exibe uma notificação com barra de progresso
        /// </summary>
        /// <param name="operationName">Nome da operação</param>
        /// <param name="total">Total de itens a processar</param>
        /// <param name="status">Status atual da operação</param>
        /// <returns>Status da operação</returns>
        [McpServerTool]
        [Description("Exibe uma notificação indicando o progresso de uma operação. Útil para indicar o progresso de tarefas longas ou processamento em lote.")]
        public async Task<string> ShowProgressAsync(
            [Description("Nome descritivo da operação em andamento")]
            string operationName,
            [Description("Número total de itens a processar")]
            int total,
            [Description("Status atual ou mensagem de progresso")]
            string status)
        {
            _logger.LogInformation("Progresso solicitado: {Operation} - {Status}", operationName, status);

            //TODO: IMPLEMENT HERE. 
            return $"Operação '{operationName}' concluída com sucesso.";
        }
    }
}