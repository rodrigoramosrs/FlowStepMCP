using FlowStep.Contracts;
using FlowStep.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Tmds.DBus.Protocol;

namespace FlowStep.MCP.Library.Renderers
{
    /// <summary>
    /// Renderizador para interações via Telegram Bot.
    /// Adapta os tipos de interação do FlowStep para recursos do Telegram.
    /// </summary>
    public class TelegramRenderer : IInteractionRenderer, IDisposable
    {
        private readonly ITelegramBotClient _botClient;
        private readonly long _chatId;
        private readonly Dictionary<string, TaskCompletionSource<InteractionResponse>> _pendingResponses;
        private readonly Dictionary<string, InteractionRequest> _pendingRequests;
        private readonly object _lock = new();
        private bool _disposed = false;

        // Evento para atualizações de progresso (enviadas como mensagens de edição)
        private readonly Dictionary<string, int> _progressMessageIds;

        public TelegramRenderer(string botToken, long chatId)
        {
            _botClient = new TelegramBotClient(botToken);
            _chatId = chatId;
            _pendingResponses = new Dictionary<string, TaskCompletionSource<InteractionResponse>>();
            _pendingRequests = new Dictionary<string, InteractionRequest>();
            _progressMessageIds = new Dictionary<string, int>();
        }

        /// <summary>
        /// Inicializa o bot e configura o webhook/polling para receber atualizações.
        /// </summary>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            var me = await _botClient.GetMe(ct);
            Console.WriteLine($"Telegram Bot iniciado: @{me.Username}");

            // Iniciar polling em background para receber callbacks
            _ = StartPollingAsync(ct);
        }

        private async Task StartPollingAsync(CancellationToken ct)
        {
            int offset = 0;
            while (!ct.IsCancellationRequested && !_disposed)
            {
                try
                {
                    var updates = await _botClient.GetUpdates(
                        offset: offset,
                        limit: 100,
                        timeout: 30,
                        cancellationToken: ct);

                    foreach (var update in updates)
                    {
                        offset = update.Id + 1;
                        await ProcessUpdateAsync(update);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro no polling do Telegram: {ex.Message}");
                    await Task.Delay(1000, ct);
                }
            }
        }

        private async Task ProcessUpdateAsync(Update update)
        {
            // Processar callback queries (botões inline)
            if (update.CallbackQuery != null)
            {
                await ProcessCallbackQueryAsync(update.CallbackQuery);
                return;
            }

            // Processar mensagens de texto (respostas livres)
            if (update.Message?.Text != null)
            {
                await ProcessTextMessageAsync(update.Message);
            }
        }

        private async Task ProcessCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            var data = callbackQuery.Data;
            if (string.IsNullOrEmpty(data)) return;

            // Formato do callback: "requestId:action:value"
            var parts = data.Split(':', 3);
            if (parts.Length < 2) return;

            var requestId = parts[0];
            var action = parts[1];
            var value = parts.Length > 2 ? parts[2] : null;

            lock (_lock)
            {
                if (!_pendingResponses.TryGetValue(requestId, out var tcs))
                    return;

                var request = _pendingRequests.GetValueOrDefault(requestId);

                switch (action)
                {
                    case "select":
                        tcs.TrySetResult(new InteractionResponse
                        {
                            Success = true,
                            SelectedValues = new List<string> { value ?? string.Empty }
                        });
                        break;

                    case "confirm":
                        tcs.TrySetResult(new InteractionResponse
                        {
                            Success = value == "yes",
                            SelectedValues = new List<string> { value ?? string.Empty }
                        });
                        break;

                    case "multi":
                        // Para seleção múltipla, acumular seleções
                        HandleMultiSelection(requestId, value, request, tcs);
                        break;

                    case "cancel":
                        tcs.TrySetResult(new InteractionResponse { Cancelled = true });
                        break;

                    case "custom":
                        // Usuário escolheu digitar valor personalizado
                        // Enviar mensagem solicitando texto
                        _ = RequestCustomTextAsync(requestId, callbackQuery.Message.MessageId);
                        return;
                }

                // Limpar pending
                _pendingResponses.Remove(requestId);
                _pendingRequests.Remove(requestId);
            }

            // Responder ao callback para remover o "carregando"
            await _botClient.AnswerCallbackQuery(callbackQuery.Id);
        }

        private void HandleMultiSelection(string requestId, string value, InteractionRequest request,
            TaskCompletionSource<InteractionResponse> tcs)
        {
            // Implementar lógica de seleção múltipla com estado
            // Por simplicidade, nesta versão tratamos como seleção única
            tcs.TrySetResult(new InteractionResponse
            {
                Success = true,
                SelectedValues = new List<string> { value ?? string.Empty }
            });
        }

        private async Task RequestCustomTextAsync(string requestId, int messageId)
        {
            await _botClient.EditMessageText(
                chatId: _chatId,
                messageId: messageId,
                text: "✏️ Digite seu valor personalizado:",
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("❌ Cancelar", $"{requestId}:cancel") }
                }));
        }

        private async Task ProcessTextMessageAsync(Telegram.Bot.Types.Message message)
        {
            var text = message.Text;
            if (string.IsNullOrEmpty(text)) return;

            lock (_lock)
            {
                // Procurar por request pendente que espera texto
                var pendingEntry = _pendingResponses.FirstOrDefault(kvp =>
                    _pendingRequests.TryGetValue(kvp.Key, out var req) &&
                    (req.Type == InteractionType.TextInput || req.AllowCustomInput));

                if (pendingEntry.Key == null) return;

                var requestId = pendingEntry.Key;
                var tcs = pendingEntry.Value;

                tcs.TrySetResult(new InteractionResponse
                {
                    Success = true,
                    TextValue = text,
                    SelectedValues = new List<string> { text }
                });

                _pendingResponses.Remove(requestId);
                _pendingRequests.Remove(requestId);
            }
        }

        public async Task<InteractionResponse> RenderAsync(InteractionRequest request, CancellationToken ct)
        {
            if (_disposed) return new InteractionResponse { Cancelled = true };

            var requestId = Guid.NewGuid().ToString("N")[..8];
            var tcs = new TaskCompletionSource<InteractionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_lock)
            {
                _pendingResponses[requestId] = tcs;
                _pendingRequests[requestId] = request;
            }

            using (ct.Register(() =>
            {
                tcs.TrySetResult(new InteractionResponse { Cancelled = true, TimedOut = true });
                _ = CleanupAsync(requestId);
            }))
            {
                try
                {
                    await SendInteractionAsync(request, requestId, ct);
                    var result = await tcs.Task;
                    return result ?? new InteractionResponse { Cancelled = true };
                }
                catch (OperationCanceledException)
                {
                    return new InteractionResponse { Cancelled = true, TimedOut = true };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro no render Telegram: {ex.Message}");
                    return new InteractionResponse { Cancelled = true };
                }
            }
        }

        private async Task SendInteractionAsync(InteractionRequest request, string requestId, CancellationToken ct)
        {
            var messageText = FormatMessage(request);
            var replyMarkup = CreateReplyMarkup(request, requestId);

            await _botClient.SendMessage(
                chatId: _chatId,
                text: messageText,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup,
                cancellationToken: ct);
        }

        private string FormatMessage(InteractionRequest request)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(request.Title) && request.Title != "Sistema")
            {
                sb.AppendLine($"<b>📝 {EscapeHtml(request.Title)}</b>");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(request.Message))
            {
                sb.AppendLine(EscapeHtml(request.Message));
            }

            // Adicionar informações de constraint se houver
            if (request.MinSelections > 0 || request.MaxSelections > 1)
            {
                sb.AppendLine();
                sb.AppendLine($"<i>Selecione de {request.MinSelections} a {request.MaxSelections} opção(ões)</i>");
            }

            if (request.IsCancellable)
            {
                sb.AppendLine();
                sb.AppendLine("<i>Envie /cancelar para cancelar</i>");
            }

            return sb.ToString();
        }

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private ReplyMarkup CreateReplyMarkup(InteractionRequest request, string requestId)
        {
            switch (request.Type)
            {
                case InteractionType.Notification:
                    return new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("✓ OK", $"{requestId}:confirm:ok") }
                    });

                case InteractionType.Confirmation:
                    if (request.Options?.Count > 0)
                    {
                        // Botões customizados
                        var buttons = request.Options.Select(opt =>
                            InlineKeyboardButton.WithCallbackData(
                                opt.Label,
                                $"{requestId}:confirm:{opt.Value}"));
                        return new InlineKeyboardMarkup(buttons.Select(b => new[] { b }));
                    }
                    else
                    {
                        // Sim/Não padrão
                        return new InlineKeyboardMarkup(new[]
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("✓ Sim", $"{requestId}:confirm:yes"),
                                InlineKeyboardButton.WithCallbackData("✗ Não", $"{requestId}:confirm:no")
                            }
                        });
                    }

                case InteractionType.SingleChoice:
                    return CreateChoiceKeyboard(request, requestId, false);

                case InteractionType.MultiChoice:
                    return CreateChoiceKeyboard(request, requestId, true);

                case InteractionType.TextInput:
                    // Para texto livre, não enviar teclado inline
                    // O usuário responde com mensagem de texto
                    return new ReplyKeyboardRemove();

                case InteractionType.ChoiceWithText:
                    return CreateChoiceWithTextKeyboard(request, requestId);

                default:
                    return new ReplyKeyboardRemove();
            }
        }

        private InlineKeyboardMarkup CreateChoiceKeyboard(InteractionRequest request, string requestId, bool isMulti)
        {
            if (request.Options == null || request.Options.Count == 0)
                return new InlineKeyboardMarkup(Array.Empty<InlineKeyboardButton[]>());

            var buttons = new List<InlineKeyboardButton[]>();

            foreach (var opt in request.Options)
            {
                var prefix = opt.IsDefault ? "● " : "○ ";
                var callbackData = isMulti
                    ? $"{requestId}:multi:{opt.Value}"
                    : $"{requestId}:select:{opt.Value}";

                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"{prefix}{opt.Label}", callbackData)
                });
            }

            if (request.IsCancellable)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("❌ Cancelar", $"{requestId}:cancel")
                });
            }

            return new InlineKeyboardMarkup(buttons);
        }

        private InlineKeyboardMarkup CreateChoiceWithTextKeyboard(InteractionRequest request, string requestId)
        {
            var buttons = new List<InlineKeyboardButton[]>();

            if (request.Options != null)
            {
                foreach (var opt in request.Options)
                {
                    buttons.Add(new[]
                    {
                        InlineKeyboardButton.WithCallbackData(opt.Label, $"{requestId}:select:{opt.Value}")
                    });
                }
            }

            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("✏️ Digitar personalizado", $"{requestId}:custom")
            });

            if (request.IsCancellable)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("❌ Cancelar", $"{requestId}:cancel")
                });
            }

            return new InlineKeyboardMarkup(buttons);
        }

        public async void ReportProgress(string operationId, int current, int total, string status)
        {
            if (_disposed) return;

            try
            {
                var percentage = total > 0 ? (current * 100 / total) : 0;
                var progressBar = CreateProgressBar(percentage);
                var messageText = $"<b>⏳ {EscapeHtml(status)}</b>\n\n{progressBar} {percentage}%";

                if (_progressMessageIds.TryGetValue(operationId, out var messageId))
                {
                    // Editar mensagem existente
                    await _botClient.EditMessageText(
                        chatId: _chatId,
                        messageId: messageId,
                        text: messageText,
                        parseMode: ParseMode.Html);
                }
                else
                {
                    // Enviar nova mensagem
                    var message = await _botClient.SendMessage(
                        chatId: _chatId,
                        text: messageText,
                        parseMode: ParseMode.Html);

                    _progressMessageIds[operationId] = message.MessageId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao reportar progresso no Telegram: {ex.Message}");
            }
        }

        private static string CreateProgressBar(int percentage)
        {
            const int totalBlocks = 10;
            var filledBlocks = percentage * totalBlocks / 100;
            var emptyBlocks = totalBlocks - filledBlocks;

            var sb = new StringBuilder();
            sb.Append('█', filledBlocks);
            sb.Append('░', emptyBlocks);
            return sb.ToString();
        }

        public async void EndProgress(string operationId)
        {
            if (_disposed) return;

            try
            {
                if (_progressMessageIds.TryGetValue(operationId, out var messageId))
                {
                    await _botClient.EditMessageText(
                        chatId: _chatId,
                        messageId: messageId,
                        text: "✅ <b>Operação concluída!</b>",
                        parseMode: ParseMode.Html);

                    _progressMessageIds.Remove(operationId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao finalizar progresso no Telegram: {ex.Message}");
            }
        }

        private async Task CleanupAsync(string requestId)
        {
            lock (_lock)
            {
                _pendingResponses.Remove(requestId);
                _pendingRequests.Remove(requestId);
            }
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _disposed = true;
            }
        }
    }
}