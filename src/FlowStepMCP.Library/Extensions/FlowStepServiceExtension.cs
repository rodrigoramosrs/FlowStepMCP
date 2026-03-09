using FlowStep.Contracts;
using FlowStep.MCP.Library.Renderers;
using FlowStep.Renderers;
using FlowStep.Renderers.AvaloniaUI;
using FlowStep.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowStep.Extensions
{
    public enum McpMode { Cli, Gui, Telegram }

    public static class FlowStepServiceExtension
    {
        public static IServiceCollection AddFlowStep(this IServiceCollection services, McpMode mode,
            string? telegramBotToken = null, long? telegramChatId = null)
        {
            services.AddLogging(b => b.AddConsole());

            switch (mode)
            {
                case McpMode.Cli:
                    services.AddSingleton<IInteractionRenderer, CliInteractionRenderer>();
                    break;

                case McpMode.Gui:
                    services.AddSingleton<AvaloniaUIRenderer>();
                    services.AddSingleton<IInteractionRenderer>(sp => sp.GetRequiredService<AvaloniaUIRenderer>());
                    break;

                case McpMode.Telegram:
                    if (string.IsNullOrEmpty(telegramBotToken) || !telegramChatId.HasValue)
                        throw new ArgumentException("Telegram require botToken e chatId");

                    services.AddSingleton<IInteractionRenderer>(sp =>
                        new TelegramRenderer(telegramBotToken, telegramChatId.Value));
                    break;
            }

            services.AddSingleton<IFlowStepService, FlowStepService>();
            return services;
        }
    }
}