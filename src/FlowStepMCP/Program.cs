using Avalonia;
using FlowStep.Contracts;
using FlowStep.Extensions;
using FlowStep.MCP.Library.Renderers;
using FlowStep.McpServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;

namespace FlowStep
{
    public class McpServerProgram
    {
        [STAThread]
        public static async Task Main(string[] args)
        {
            // Parse argumentos de linha de comando primeiro para determinar modo
            var parsedOptions = ParseCommandLineArgs(args);

            // Build configuration hierárquica: args > env > appsettings
            var configuration = BuildConfiguration(args, parsedOptions);

            // Determinar modo de operação
            var mode = DetermineMcpMode(configuration);

            Console.WriteLine($"🚀 Iniciando FlowStep MCP Server em modo: {mode}");

            var builder = WebApplication.CreateBuilder(args);

            // Adicionar nossa configuration ao WebApplication
            builder.Configuration.AddConfiguration(configuration);

            builder.Services.AddControllers();

            // Configurar FlowStep baseado no modo
            ConfigureFlowStep(builder.Services, mode, configuration);

            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithHttpTransport()
                .WithToolsFromAssembly(typeof(FlowStepMcpService).Assembly);

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // Logging
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors();
            app.MapMcp();
            app.MapControllers();

            var webHostCts = new CancellationTokenSource();
            var webHostTask = Task.Run(() => app.RunAsync(webHostCts.Token), webHostCts.Token);

            // Inicializar renderizador específico do modo
            if (mode == McpMode.Gui)
            {
                await InitializeAvaloniaAsync(args, webHostCts);
            }
            else if (mode == McpMode.Telegram)
            {
                await InitializeTelegramAsync(app.Services, webHostCts);
            }
            else
            {
                // CLI mode - apenas aguardar
                Console.WriteLine("Modo CLI ativo. Pressione Ctrl+C para encerrar.");
                await WaitForCancellationAsync(webHostCts);
            }

            // Shutdown
            webHostCts.Cancel();
            try
            {
                await webHostTask;
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Parse argumentos de linha de comando usando System.CommandLine
        /// </summary>
        private static McpServerOptions ParseCommandLineArgs(string[] args)
        {
            var modeOption = new Option<string>(
                name: "--mode",
                description: "Modo de operação: cli, gui, telegram",
                getDefaultValue: () => "gui");

            var telegramTokenOption = new Option<string?>(
                name: "--telegram-token",
                description: "Token do bot Telegram (modo telegram)");

            var telegramChatIdOption = new Option<long?>(
                name: "--telegram-chat-id",
                description: "Chat ID do Telegram (modo telegram)");

            var configOption = new Option<string?>(
                name: "--config",
                description: "Caminho para arquivo de configuração personalizado");

            var rootCommand = new RootCommand("FlowStep MCP Server")
    {
        modeOption,
        telegramTokenOption,
        telegramChatIdOption,
        configOption
    };

            McpServerOptions result = new();

            // API moderna do System.CommandLine
            rootCommand.SetHandler((mode, telegramToken, telegramChatId, config) =>
            {
                result.Mode = mode;
                result.TelegramToken = telegramToken;
                result.TelegramChatId = telegramChatId;
                result.ConfigPath = config;
            }, modeOption, telegramTokenOption, telegramChatIdOption, configOption);

            rootCommand.Invoke(args);
            return result;
        }

        /// <summary>
        /// Build configuration hierárquica: CommandLine > Environment > Appsettings
        /// </summary>
        private static IConfiguration BuildConfiguration(string[] args, McpServerOptions parsedOptions)
        {
            var configBuilder = new ConfigurationBuilder();

            // 1. Appsettings.json (base)
            configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            // 2. Appsettings.{Environment}.json
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            configBuilder.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);

            // 3. Environment Variables (prefixo FLOWSTEP_)
            configBuilder.AddEnvironmentVariables(prefix: "FLOWSTEP_");

            // 4. Command Line Args
            configBuilder.AddCommandLine(args);

            // 5. Config personalizado se especificado
            if (!string.IsNullOrEmpty(parsedOptions.ConfigPath))
            {
                configBuilder.AddJsonFile(parsedOptions.ConfigPath, optional: false);
            }

            var configuration = configBuilder.Build();

            // Merge parsed options (maior prioridade)
            if (!string.IsNullOrEmpty(parsedOptions.Mode))
            {
                configuration["Mode"] = parsedOptions.Mode;
            }
            if (!string.IsNullOrEmpty(parsedOptions.TelegramToken))
            {
                configuration["Telegram:BotToken"] = parsedOptions.TelegramToken;
            }
            if (parsedOptions.TelegramChatId.HasValue)
            {
                configuration["Telegram:ChatId"] = parsedOptions.TelegramChatId.Value.ToString();
            }

            return configuration;
        }

        /// <summary>
        /// Determina o modo MCP baseado na configuração
        /// </summary>
        private static McpMode DetermineMcpMode(IConfiguration configuration)
        {
            var modeStr = configuration["Mode"]?.ToLowerInvariant();

            return modeStr switch
            {
                "cli" or "console" => McpMode.Cli,
                "telegram" or "tg" => McpMode.Telegram,
                "gui" or "avalonia" or "desktop" or null => McpMode.Gui,
                _ => throw new ArgumentException($"Modo desconhecido: {modeStr}. Use: cli, gui, telegram")
            };
        }

        /// <summary>
        /// Configura os serviços do FlowStep baseado no modo
        /// </summary>
        private static void ConfigureFlowStep(IServiceCollection services, McpMode mode, IConfiguration configuration)
        {
            switch (mode)
            {
                case McpMode.Cli:
                    services.AddFlowStep(McpMode.Cli);
                    break;

                case McpMode.Gui:
                    services.AddFlowStep(McpMode.Gui);
                    break;

                case McpMode.Telegram:
                    var token = configuration["Telegram:BotToken"]
                        ?? throw new InvalidOperationException("Telegram:BotToken não configurado. Use appsettings, env var FLOWSTEP_TELEGRAM__BOTTOKEN, ou argumento --telegram-token");

                    var chatIdStr = configuration["Telegram:ChatId"]
                        ?? throw new InvalidOperationException("Telegram:ChatId não configurado. Use appsettings, env var FLOWSTEP_TELEGRAM__CHATID, ou argumento --telegram-chat-id");

                    if (!long.TryParse(chatIdStr, out var chatId))
                        throw new InvalidOperationException($"Telegram:ChatId inválido: {chatIdStr}");

                    services.AddFlowStep(McpMode.Telegram, token, chatId);
                    break;
            }
        }

        /// <summary>
        /// Inicializa Avalonia (modo GUI)
        /// </summary>
        private static async Task InitializeAvaloniaAsync(string[] args, CancellationTokenSource cts)
        {
            var appBuilder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();

            // Iniciar em thread separada para não bloquear
            var avaloniaTask = Task.Run(() =>
            {
                try
                {
                    appBuilder.StartWithClassicDesktopLifetime(args);
                }
                finally
                {
                    cts.Cancel();
                }
            });

            // Aguardar sinal de cancelamento
            await WaitForCancellationAsync(cts);
        }

        /// <summary>
        /// Inicializa Telegram (modo headless)
        /// </summary>
        private static async Task InitializeTelegramAsync(IServiceProvider services, CancellationTokenSource cts)
        {
            var renderer = services.GetRequiredService<IInteractionRenderer>() as TelegramRenderer;

            if (renderer != null)
            {
                await renderer.InitializeAsync(cts.Token);
                Console.WriteLine("🤖 Bot Telegram inicializado e aguardando mensagens...");
            }

            await WaitForCancellationAsync(cts);
        }

        /// <summary>
        /// Aguarda sinal de cancelamento
        /// </summary>
        private static async Task WaitForCancellationAsync(CancellationTokenSource cts)
        {
            var tcs = new TaskCompletionSource<object>();
            using (cts.Token.Register(() => tcs.TrySetResult(null!)))
            {
                await tcs.Task;
            }
        }

        private static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }

    /// <summary>
    /// Opções parseadas da linha de comando
    /// </summary>
    public class McpServerOptions
    {
        public string Mode { get; set; } = "gui";
        public string? TelegramToken { get; set; }
        public long? TelegramChatId { get; set; }
        public string? ConfigPath { get; set; }
    }
}