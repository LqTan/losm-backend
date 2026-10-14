using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Search.Application.Abstractions;
using Search.Application.Search.Queries.SearchMeetingPlaces;
using Search.Application.Search.Queries.SearchPlaces;
using Search.Infrastructure.Services;

namespace Search;

public static class DependencyInjection
{
    public static IServiceCollection AddSearch(
        this IServiceCollection services,
        IConfiguration? configuration = null
    )
    {
        services.AddSingleton<IOnnxRankingModelService, OnnxRankingModelService>();
        services.AddScoped<IPlaceSearchService, PlaceSearchService>();
        services.AddScoped<IRelevanceScorer, MockRelevanceScorer>();
        services.AddScoped<IRankingService, RankingService>();

        if (configuration is not null)
        {
            services.Configure<OsrmOptions>(configuration.GetSection(OsrmOptions.SectionName));
        }

        services.AddHttpClient<IOsrmTableService, OsrmTableService>();
        services.AddHttpClient<IOsrmRouteService, OsrmRouteService>();

        services.AddScoped<SearchPlacesHandler>();
        services.AddScoped<SearchMeetingPlacesHandler>();
        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);
        return services;
    }
}
