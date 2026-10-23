using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Transit.Application;
using Transit.Infrastructure;
namespace Transit;
public static class DependencyInjection
{
    public static IServiceCollection AddTransit(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<TransitDbContext>(o => o.UseSqlServer(config.GetConnectionString("DefaultConnection"), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "transit")));
        services.AddSingleton<TransitStorage>();
        services.AddScoped<TransitValidator>();
        services.AddScoped<TransitImportJobs>();
        services.AddScoped<TransitImportRecovery>();
        services.AddHostedService<TransitImportRecoverySchedule>();
        services.AddControllers().AddApplicationPart(typeof(DependencyInjection).Assembly);
        return services;
    }
}
