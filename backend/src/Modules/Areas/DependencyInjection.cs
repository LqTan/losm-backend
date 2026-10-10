using Areas.Application.Areas.Commands.CreateArea;
using Areas.Application.Areas.Commands.DeleteArea;
using Areas.Application.Areas.Commands.ImportAreas;
using Areas.Application.Areas.Commands.UpdateArea;
using Areas.Application.Areas.Queries.GetAreas;
using Areas.Infrastructure.Persistence;
using Areas.Infrastructure.Repositories;
using Areas.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Areas;

public static class DependencyInjection
{
    public static IServiceCollection AddAreas(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AreasDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<Areas.Application.Abstractions.IAreaRepository, AreaRepository>();
        services.AddScoped<AreasSeeder>();

        services.AddScoped<GetAreasHandler>();
        services.AddScoped<CreateAreaHandler>();
        services.AddScoped<UpdateAreaHandler>();
        services.AddScoped<DeleteAreaHandler>();
        services.AddScoped<ImportAreasHandler>();

        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);

        return services;
    }
}