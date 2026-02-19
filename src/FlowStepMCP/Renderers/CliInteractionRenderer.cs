using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowStep.Contracts;
using FlowStep.Models;

namespace FlowStep.Renderers
{
    public class CliInteractionRenderer : IInteractionRenderer
    {
        private readonly object _consoleLock = new();

        public async Task<InteractionResponse> RenderAsync(InteractionRequest request, CancellationToken ct)
        {
            var response = new InteractionResponse { Success = false };

            // Notificação Simples
            if (request.Type == InteractionType.Notification)
            {
                WriteColor($"[{request.Title}] {request.Message}", ConsoleColor.Cyan);
                response.Success = true;
                return response;
            }

            // Loop de validação de entrada
            bool validInput = false;
            while (!validInput && !ct.IsCancellationRequested)
            {
                WriteColor($"\n[{request.Title}] {request.Message}", ConsoleColor.White);

                // Renderizar Opções
                if (request.Options != null && request.Options.Any())
                {
                    for (int i = 0; i < request.Options.Count; i++)
                    {
                        var opt = request.Options[i];
                        var prefix = request.Type == InteractionType.MultiChoice ? "[ ]" : "( )";
                        if (opt.IsDefault) prefix = request.Type == InteractionType.MultiChoice ? "[X]" : "(*)";

                        Console.WriteLine($"  {i + 1}. {prefix} {opt.Label}");
                    }

                    if (request.AllowCustomInput)
                    {
                        Console.WriteLine($"  O. [Expandir] {request.CustomInputPlaceholder}");
                    }
                }

                if (request.IsCancellable)
                    Console.WriteLine("  C. Cancelar");

                Console.Write("> ");

                // Leitura com suporte a Cancelamento
                string? input = await ReadLineAsync(ct);

                if (string.IsNullOrWhiteSpace(input) && request.Options?.Any(o => o.IsDefault) == true)
                {
                    // Aceitar default se vazio
                    var def = request.Options.First(o => o.IsDefault);
                    response.SelectedValues = new List<string> { def.Value };
                    validInput = true;
                }
                else if (request.AllowCustomInput && (input?.ToLower() == "o" || input?.ToLower() == "outros"))
                {
                    // Lógica de Expandir no CLI
                    Console.Write("Digite sua resposta personalizada: ");
                    var custom = await ReadLineAsync(ct);
                    response.CustomInput = custom;
                    response.TextValue = custom; // Fallback
                    response.Success = !string.IsNullOrWhiteSpace(custom);
                    validInput = response.Success;
                }
                else if (request.IsCancellable && (input?.ToLower() == "c"))
                {
                    response.Cancelled = true;
                    validInput = true;
                }
                else
                {
                    // Processar Seleção Numérica
                    if (int.TryParse(input, out int index) && request.Options != null)
                    {
                        index -= 1; // 0-based
                        if (index >= 0 && index < request.Options.Count)
                        {
                            response.SelectedValues = new List<string> { request.Options[index].Value };
                            response.TextValue = request.Options[index].Label;
                            validInput = true;
                        }
                    }

                    if (!validInput)
                        WriteColor("Opção inválida.", ConsoleColor.Red);
                }
            }

            response.Success = !response.Cancelled && !response.TimedOut;
            return response;
        }

        public void ReportProgress(string operationId, int current, int total, string status)
        {
            lock (_consoleLock)
            {
                int percent = (int)((double)current / total * 100);
                int barWidth = 30;
                int filled = (int)(barWidth * percent / 100);
                string bar = new string('█', filled) + new string('░', barWidth - filled);

                // Sobrescrever linha atual para efeito de progresso
                Console.Write($"\r{status}: [{bar}] {percent}% ({current}/{total})");
                if (current >= total) Console.WriteLine();
            }
        }

        public void EndProgress(string operationId)
        {
            Console.WriteLine();
        }

        private void WriteColor(string msg, ConsoleColor color)
        {
            lock (_consoleLock)
            {
                var prev = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(msg);
                Console.ForegroundColor = prev;
            }
        }

        private Task<string?> ReadLineAsync(CancellationToken ct)
        {
            return Task.Run(() =>
            {
                // Simples implementação bloqueante que verifica CT
                // Em produção, usar Console.KeyAvailable para ser verdadeiramente não-bloqueante
                while (!ct.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        return Console.ReadLine();
                    }
                    Thread.Sleep(100);
                }
                throw new OperationCanceledException();
            }, ct);
        }
    }
}