using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyNext.Services;

/// <summary>
/// Registers the library discovery script with JavaScript Injector when installed.
/// </summary>
public class WebDiscoveryRegistration : IHostedService
{
    private const string InjectorAssembly = "Jellyfin.Plugin.JavaScriptInjector";
    private readonly ILogger<WebDiscoveryRegistration> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebDiscoveryRegistration"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public WebDiscoveryRegistration(ILogger<WebDiscoveryRegistration> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var register = AssemblyLoadContext.All.SelectMany(context => context.Assemblies)
                .FirstOrDefault(assembly => assembly.GetName().Name == InjectorAssembly)
                ?.GetType(InjectorAssembly + ".PluginInterface")
                ?.GetMethod("RegisterScript");

            if (register is null)
            {
                _logger.LogInformation("Install JavaScript Injector to display JellyNext discovery in Jellyfin Web libraries");
                return Task.CompletedTask;
            }

            var plugin = Plugin.Instance ?? throw new InvalidOperationException("JellyNext is not initialized");
            var assembly = typeof(WebDiscoveryRegistration).Assembly;
            using var stream = assembly.GetManifestResourceStream("Jellyfin.Plugin.JellyNext.Resources.discovery.js")
                ?? throw new InvalidOperationException("JellyNext discovery script is missing");
            using var reader = new StreamReader(stream);
            var json = JsonSerializer.Serialize(new
            {
                id = "jellynext-library-discovery",
                name = "JellyNext Library Discovery",
                script = reader.ReadToEnd(),
                enabled = true,
                requiresAuthentication = true,
                pluginId = plugin.Id.ToString(),
                pluginName = plugin.Name,
                pluginVersion = assembly.GetName().Version?.ToString()
            });
            var parse = register.GetParameters().Single().ParameterType.GetMethod("Parse", new[] { typeof(string) })
                ?? throw new InvalidOperationException("JavaScript Injector does not support the script registration payload");
            var payload = parse.Invoke(null, new object[] { json });

            if (register.Invoke(null, new[] { payload }) is true)
            {
                _logger.LogInformation("Registered JellyNext library discovery with JavaScript Injector");
            }
            else
            {
                _logger.LogWarning("JavaScript Injector did not register JellyNext library discovery");
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not register JellyNext library discovery with JavaScript Injector");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
