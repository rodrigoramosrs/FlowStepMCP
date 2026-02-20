using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FlowStep.Contracts;
using FlowStep.Services;
using FlowStep.Renderers;
using FlowStep.Renderers.AvaloniaUI;

namespace FlowStep.Extensions
{
    public enum McpMode { Cli, Gui }

    public static class FlowStepServiceExtension
    {
        public static IServiceCollection AddFlowStep(this IServiceCollection services, McpMode mode)
        {
            services.AddLogging(b => b.AddConsole());

            if (mode == McpMode.Cli)
            {
                services.AddSingleton<IInteractionRenderer, CliInteractionRenderer>();
            }
            else
            {
                // No modo GUI, registramos o novo renderizador Avalonia.
                services.AddSingleton<AvaloniaUIRenderer>();
                services.AddSingleton<IInteractionRenderer>(sp => sp.GetRequiredService<AvaloniaUIRenderer>());
            }

            services.AddSingleton<IFlowStepService, FlowStepService>();
            return services;
        }
    }
}