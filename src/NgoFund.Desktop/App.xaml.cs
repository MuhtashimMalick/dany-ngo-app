using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NgoFund.Desktop.Logging;
using NgoFund.Desktop.Services;

namespace NgoFund.Desktop;

public partial class App : Application
{
    // Default matches docker-compose.yml's well-known port for a same-host dev setup (see
    // ). Overridable per-deployment via an "ApiBaseUrl" key in an optional
    // appsettings.json placed next to the .exe — see docs/deployment.md — so pointing a shipped
    // build at a different server never requires a recompile.
    private const string DefaultApiBaseUrl = "http://localhost:8080/";

    public App()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddProvider(new FileLoggerProvider()));

        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        services.AddSingleton<AuthState>();
        services.AddSingleton<ToastService>();
        services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(ResolveApiBaseUrl()) });
        services.AddScoped<ApiClient>();

        var provider = services.BuildServiceProvider();
        Resources.Add("services", provider);
        provider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup").LogInformation("App constructed, service provider built.");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
    }

    private static string ResolveApiBaseUrl()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
        {
            return DefaultApiBaseUrl;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            return document.RootElement.TryGetProperty("ApiBaseUrl", out var value) && value.GetString() is { Length: > 0 } url
                ? url
                : DefaultApiBaseUrl;
        }
        catch (JsonException)
        {
            return DefaultApiBaseUrl;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ((IServiceProvider)Resources["services"]).GetRequiredService<ILoggerFactory>()
            .CreateLogger("UnhandledException").LogError(e.Exception, "Unhandled dispatcher exception");
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        ((IServiceProvider)Resources["services"]).GetRequiredService<ILoggerFactory>()
            .CreateLogger("UnhandledException").LogError(e.ExceptionObject as Exception, "Unhandled AppDomain exception");
    }
}
