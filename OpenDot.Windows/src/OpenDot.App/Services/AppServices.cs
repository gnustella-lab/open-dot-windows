using Microsoft.Extensions.DependencyInjection;
using OpenDot.Automation.Browser;
using OpenDot.Automation.Computer;
using OpenDot.Core.Agents;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Scheduling;
using OpenDot.Infrastructure.Persistence;
using OpenDot.Infrastructure.Security;
using OpenDot.Infrastructure.Windows;
using OpenDot.Integrations.Composio;
using OpenDot.Integrations.OpenAI;
using OpenDot.Integrations.OpenRouter;
using OpenDot.App.ViewModels;

namespace OpenDot.App.Services;

public static class AppServices
{
    private static IServiceProvider? _serviceProvider;

    public static IServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException("AppServices has not been initialized.");

    public static void Initialize()
    {
        var services = new ServiceCollection();

        // 1. Persistence & Security
        services.AddSingleton<IDotRepository, SqliteDotRepository>();
        services.AddSingleton<ISecureVault, DpapiVault>();
        services.AddSingleton<INotificationService, WindowsNotificationService>();

        // 2. Automation & Workspaces
        services.AddSingleton<PlaywrightBrowserManager>();
        services.AddSingleton<IComputerManager, ComputerManager>();

        // 3. AI Clients & Integrations
        services.AddSingleton(new HttpClient());
        services.AddSingleton(sp =>
        {
            var vault = sp.GetRequiredService<ISecureVault>();
            var repo = sp.GetRequiredService<IDotRepository>();
            var http = sp.GetRequiredService<HttpClient>();
            return new OpenAiClient(http, () =>
            {
                var env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrWhiteSpace(env)) return env;
                var sealedKey = repo.GetSetting("openai_key");
                return string.IsNullOrWhiteSpace(sealedKey) ? null : vault.Unseal(sealedKey);
            });
        });

        services.AddSingleton(sp =>
        {
            var vault = sp.GetRequiredService<ISecureVault>();
            var repo = sp.GetRequiredService<IDotRepository>();
            var http = sp.GetRequiredService<HttpClient>();
            return new OpenRouterClient(http, () =>
            {
                var env = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
                if (!string.IsNullOrWhiteSpace(env)) return env;
                var sealedKey = repo.GetSetting("openrouter_key");
                return string.IsNullOrWhiteSpace(sealedKey) ? null : vault.Unseal(sealedKey);
            });
        });

        services.AddSingleton<ComposioClient>(sp =>
        {
            var vault = sp.GetRequiredService<ISecureVault>();
            var repo = sp.GetRequiredService<IDotRepository>();
            var http = sp.GetRequiredService<HttpClient>();
            return new ComposioClient(http, () =>
            {
                var env = Environment.GetEnvironmentVariable("COMPOSIO_API_KEY");
                if (!string.IsNullOrWhiteSpace(env)) return env;
                var sealedKey = repo.GetSetting("composio_api_key");
                return string.IsNullOrWhiteSpace(sealedKey) ? null : vault.Unseal(sealedKey);
            });
        });

        services.AddSingleton<ITriggerService>(sp => sp.GetRequiredService<ComposioClient>());

        // 4. Core Runtime & Scheduler
        services.AddSingleton<IAgentRuntime>(sp =>
        {
            var repo = sp.GetRequiredService<IDotRepository>();
            var computer = sp.GetRequiredService<IComputerManager>();
            var vault = sp.GetRequiredService<ISecureVault>();
            var openai = sp.GetRequiredService<OpenAiClient>();
            var openrouter = sp.GetRequiredService<OpenRouterClient>();

            return new AgentRuntime(repo, computer, vault, dot =>
            {
                if (dot.Model != null && dot.Model.StartsWith("openrouter:"))
                    return openrouter;
                return openai;
            });
        });

        services.AddSingleton<ISchedulerService>(sp =>
        {
            var repo = sp.GetRequiredService<IDotRepository>();
            return new RoutineScheduler(repo, () => sp.GetRequiredService<IAgentRuntime>());
        });

        // 5. ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<DotViewModel>();
        services.AddTransient<ChannelsViewModel>();
        services.AddTransient<AppsViewModel>();
        services.AddTransient<SettingsViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // Start background scheduler
        _serviceProvider.GetRequiredService<ISchedulerService>().Start();
    }
}
