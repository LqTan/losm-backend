using Configuration.Application.Abstractions;
using Configuration.Application.Admin.Commands.DeleteConfiguration;
using Configuration.Application.Admin.Commands.UpsertConfiguration;
using Configuration.Application.Admin.Queries.GetConfiguration;
using Configuration.Application.Admin.Queries.ListConfigurations;
using Configuration.Infrastructure;
using Configuration.Infrastructure.Caching;
using Configuration.Infrastructure.Elasticsearch;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddConfigurationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ElasticsearchOptions>(
            configuration.GetSection("Elasticsearch"));

        services.AddHttpClient<EsConfigurationStore>();
        services.AddSingleton<Application.Abstractions.IConfigurationStore>(
            sp => sp.GetRequiredService<EsConfigurationStore>());

        services.AddHttpClient<ConfigurationIndexBootstrapper>();
        services.AddScoped<CachedTuningProvider>();
        services.AddScoped<ITuningProvider>(
            sp => sp.GetRequiredService<CachedTuningProvider>());

        services.AddScoped<UpsertConfigurationHandler>();
        services.AddScoped<DeleteConfigurationHandler>();
        services.AddScoped<GetConfigurationHandler>();
        services.AddScoped<ListConfigurationsHandler>();

        services.AddHostedService<ConfigurationBootstrapHostedService>();
        services.AddHostedService<ConfigurationChangeWatcher>();

        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);

        return services;
    }
}

internal sealed class ConfigurationBootstrapHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConfigurationBootstrapHostedService> _logger;

    public ConfigurationBootstrapHostedService(
        IServiceProvider serviceProvider,
        ILogger<ConfigurationBootstrapHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var bootstrapper = scope.ServiceProvider.GetRequiredService<ConfigurationIndexBootstrapper>();
            await bootstrapper.EnsureIndicesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration index bootstrap failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
