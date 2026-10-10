using Areas.Application.Abstractions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Overture.Application.Abstractions;
using Overture.Application.Jobs;
using Overture.Application.Settings.Commands.UpdateSetting;
using Overture.Application.Settings.Queries.GetSettings;
using Overture.Infrastructure;
using Overture.Infrastructure.Persistence;
using Overture.Infrastructure.Repositories;

namespace Overture;

public static class DependencyInjection
{
    public static IServiceCollection AddOverture(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<OvertureDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        services.Configure<OvertureOptions>(configuration.GetSection("Overture"));

        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<GetSettingsHandler>();
        services.AddScoped<UpdateSettingHandler>();

        services.AddScoped<IAreaLookup, AreaLookupAdapter>();
        services.AddScoped<IOvertureCliRunner, OvertureCliRunner>();
        services.AddSingleton<IOvertureItemStreamer, OvertureItemStreamer>();
        services.AddScoped<ImportOverturePlacesJob>();
        services.AddScoped<OvertureRecurringJobBootstrap>();

        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);

        return services;
    }
}