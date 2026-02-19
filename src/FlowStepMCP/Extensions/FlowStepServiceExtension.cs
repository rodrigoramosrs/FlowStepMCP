using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FlowStep.Contracts;
using FlowStep.Services;
using FlowStep.Renderers;

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
                services.AddSingleton<AvaloniaGuiRenderer>();
                services.AddSingleton<IInteractionRenderer>(sp => sp.GetRequiredService<AvaloniaGuiRenderer>());
            }

            services.AddSingleton<IFlowStepService, FlowStepService>();
            return services;
        }
    }
}