using Avalonia;
using FlowStep.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlowStep
{
    public class McpServerProgram
    {
        [STAThread] // Avalonia exige STA para UI
        public static async Task Main(string[] args)
        {
            // Constrói o WebHost (não inicia ainda)
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            builder.Services.AddFlowStep(McpMode.Gui);
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithHttpTransport()
                .WithToolsFromAssembly();

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors();
            app.MapMcp();
            app.MapControllers();

            // Inicia o servidor web em background
            var webHostCts = new CancellationTokenSource();
            var webHostTask = Task.Run(() => app.RunAsync(webHostCts.Token), webHostCts.Token);

            // Inicializa e inicia Avalonia (bloqueante; gerencia o lifetime da UI)
            var appBuilder = BuildAvaloniaApp();
            appBuilder.StartWithClassicDesktopLifetime(args);

            // Quando a UI fechar, solicita shutdown do web host
            webHostCts.Cancel();
            try
            {
                await webHostTask;
            }
            catch (OperationCanceledException) { }
        }

        private static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }
}
