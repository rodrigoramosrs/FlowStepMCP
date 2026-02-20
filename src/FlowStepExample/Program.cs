using Avalonia;
using FlowStep;
using FlowStep.Contracts;
using FlowStep.Extensions;
using FlowStep.McpServices;
using FlowStep.Models;
using FlowStep.Renderers; // necessário para AvaloniaGuiRenderer
using FlowStep.Renderers.AvaloniaUI;
using FlowStep.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlowStepExample
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //var appBuilder = BuildAvaloniaApp();
            //appBuilder.StartWithClassicDesktopLifetime(args);

            var mainThreadInitialized = new ManualResetEventSlim(false);
            var mainThreadException = default(Exception);

            Thread mainThread = new(async () =>
            {
                try
                {
                    Thread.Sleep(500);
                    bool flowControl = await RunDemo();
                    if (!flowControl)
                    {
                        return;
                    }

                }
                catch (Exception ex)
                {
                    mainThreadException = ex;
                }
                finally
                {
                    mainThreadInitialized.Set(); // Sinaliza que a thread UI terminou (ou falhou)
                }
            })
            {
                IsBackground = true,
               // ApartmentState = ApartmentState.MTA // ⚠️ Obrigatório para Avalonia no Windows
            };
            mainThread.Start();

           // uiThread.Start();
            //Task.Run(() => ); // Inicia a thread da UI de forma assíncrona  
            //uiThread.Start();

            // Espera até que a thread da UI esteja iniciada (ou falhe)
            //avaloniaInitialized.Wait();
            //if (uiThreadException != null) throw uiThreadException;


            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(args); // Bloqueia aqui, mas na thread UI
        }

        private static async Task<bool> RunDemo()
        {
            Console.WriteLine("=== Demonstração de Notificações e Mensagens (MCP) ===");
            Console.WriteLine("Escolha o modo:");
            Console.WriteLine("1. CLI (Console)");
            Console.WriteLine("2. GUI (Avalonia - janela separada)");
            Console.Write("> ");
            var choice = "2";// Console.ReadLine()?.Trim();

            McpMode mode;
            switch (choice)
            {
                case "1": mode = McpMode.Cli; break;
                case "2": mode = McpMode.Gui; break;
                default:
                    Console.WriteLine("Modo inválido. Usando CLI como padrão.");
                    mode = McpMode.Cli;
                    break;
            }

            // Criar ServiceCollection e registrar todos os serviços necessários
            var services = new ServiceCollection();

            // Logging (obrigatório para FlowStepMcpService)
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

            // Registrar renderizador (CLI ou GUI)
            if (mode == McpMode.Cli)
            {
                services.AddSingleton<IInteractionRenderer, CliInteractionRenderer>();
            }
            else
            {
                // GUI: AvaloniaGuiRenderer precisa ser registrado como singleton *e* IInteractionRenderer aponta para ele
                services.AddSingleton<AvaloniaUIRenderer>();
                services.AddSingleton<IInteractionRenderer>(sp => sp.GetRequiredService<AvaloniaUIRenderer>());
            }

            // Registrar FlowStepService (depende de renderer)
            services.AddTransient<IFlowStepService, FlowStepService>();

            // ✅ REGISTRO CRÍTICO: FlowStepMcpService precisa ser registrado explicitamente
            services.AddSingleton<FlowStepMcpService>();

            var serviceProvider = services.BuildServiceProvider();

            // Opcional: forçar inicialização da UI (AvaloniaGuiRenderer)
            if (mode == McpMode.Gui)
            {
                try
                {
                    var guiRenderer = serviceProvider.GetRequiredService<AvaloniaUIRenderer>();
                    await guiRenderer.InitializeAsync(); // Garante que MainWindow seja criado
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao inicializar GUI: {ex.Message}");
                    return false;
                }
            }

            // Obtém os serviços
            var mcpService = serviceProvider.GetRequiredService<FlowStepMcpService>();
            var flowStepService = serviceProvider.GetRequiredService<IFlowStepService>();

            Console.WriteLine("\n=== Testando interações ===\n");

            await RunInteractiveDemoAsync(mcpService, flowStepService);
            return true;
        }
        private static async Task RunInteractiveDemoAsync(FlowStepMcpService mcpService, IFlowStepService flowStepService)
        {
            int choice = 0;
            while (true)
            {
                Console.WriteLine("\n--- Menu de Interações ---");
                Console.WriteLine("1. Notificação simples (NotifyUserAsync)");
                Console.WriteLine("2. Confirmação Sim/Não (ConfirmAsync)");
                Console.WriteLine("3. Escolha única (ChooseOptionAsync)");
                Console.WriteLine("4. Escolha múltipla (ChooseMultipleOptionsAsync)");
                Console.WriteLine("5. Entrada de texto (AskUserForTextAsync)");
                Console.WriteLine("6. Escolha com texto personalizado (ChooseWithCustomTextAsync)");
                Console.WriteLine("7. Demo progresso (ShowProgressAsync)");
                Console.WriteLine("0. Sair");
                Console.Write("> ");
                var input = Console.ReadLine()?.Trim();
                if (!int.TryParse(input, out int option))
                {
                    Console.WriteLine("Opção inválida.");
                    continue;
                }

                try
                {
                    switch (option)
                    {
                        case 1:
                            await mcpService.NotifyUserAsync(
                                message: "Esta é uma notificação de teste. O sistema está rodando corretamente.",
                                title: "Sistema");
                            break;

                        case 2:
                            var confirm = await mcpService.ConfirmAsync(
                                message: "Você deseja continuar?",
                                title: "Confirmação",
                                isCancellable: true);
                            Console.WriteLine($"Resposta da confirmação: {confirm}");
                            break;

                        case 3:
                            var options = new List<InteractionOption>
                            {
                                new("Opção A", "a"),
                                new("Opção B", "b", isDefault: true),
                                new("Opção C", "c")
                            };
                            var selected = await mcpService.ChooseOptionAsync(
                                message: "Escolha uma opção:",
                                options: options,
                                title: "Seleção Simples");
                            Console.WriteLine($"Você escolheu: {selected}");
                            break;

                        case 4:
                            var multiOptions = new List<InteractionOption>
                            {
                                new("Item 1", "item1"),
                                new("Item 2", "item2", isDefault: true),
                                new("Item 3", "item3")
                            };
                            var selectedMulti = await mcpService.ChooseMultipleOptionsAsync(
                                message: "Selecione itens (máx. 2):",
                                options: multiOptions,
                                minSelections: 1,
                                maxSelections: 2,
                                title: "Seleção Múltipla");
                            Console.WriteLine($"Itens selecionados: {string.Join(", ", selectedMulti)}");
                            break;

                        case 5:
                            var text = await mcpService.AskUserForTextAsync(
                                message: "Digite seu nome:",
                                title: "Entrada de Texto",
                                placeholder: "Ex: João Silva");
                            Console.WriteLine($"Você digitou: {text}");
                            break;

                        case 6:
                            var choiceOptions = new List<InteractionOption>
                            {
                                new("Verde", "green"),
                                new("Azul", "blue")
                            };
                            var choiceWithText = await mcpService.ChooseWithCustomTextAsync(
                                message: "Escolha uma cor ou digite outra:",
                                options: choiceOptions,
                                title: "Escolha com texto personalizado", "");
                            Console.WriteLine($"Resultado: {choiceWithText}");
                            break;

                        case 7:
                            var progress = flowStepService.CreateProgress("Processando tarefa", total: 10);
                            for (int i = 1; i <= 10; i++)
                            {
                                progress.Report((i, 10, $"Passo {i}/10"));
                                await Task.Delay(500);
                            }
                            Console.WriteLine("\nProgresso concluído.");
                            break;

                        case 0:
                            Console.WriteLine("Saindo...");
                            return;

                        default:
                            Console.WriteLine("Opção inválida.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro: {ex.Message}");
                }
            }
        }
    }
}
