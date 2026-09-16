using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Places.Application.Abstractions;
using Places.Application.Contracts;
using Places.Application.Places.Queries.GetPlaceById;
using Places.Application.Places.Queries.SearchPlaces;
using Places.Application.SavedPlaces.Commands.SavePlace;
using Places.Application.SavedPlaces.Commands.UnsavePlace;
using Places.Application.SavedPlaces.Queries.GetSavedPlacesByUser;
using Places.Infrastructure.Persistence;
using Places.Infrastructure.Providers;
using Places.Infrastructure.Repositories;

public static class DependencyInjection
{
    public static IServiceCollection AddPlaces(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<PlacesDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")
            ));
        services.AddScoped<IPlaceRepository, PlaceRepository>();
        services.AddScoped<ISavedPlaceRepository, SavedPlaceRepository>();
        services.AddScoped<SearchPlacesHandler>();
        services.AddScoped<GetPlaceByIdHandler>();
        services.AddScoped<SavePlaceHandler>();
        services.AddScoped<UnsavePlaceHandler>();
        services.AddScoped<GetSavedPlacesByUserHandler>();
        services.AddScoped<IPlacesSearchContract, PlacesSearchContract>();

        services
            .AddOptions<OverpassProviderOptions>()
            .Bind(configuration.GetSection("Overpass"));

        services.AddHttpClient(OverpassPlaceProvider.NominatimClientName, (sp, client) =>
        {
            var opts = sp
                .GetRequiredService<IOptions<OverpassProviderOptions>>()
                .Value;

            client.BaseAddress = new Uri(opts.NominatimBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
        });

        services.AddHttpClient(OverpassPlaceProvider.OverpassClientName, (sp, client) =>
        {
            var opts = sp
                .GetRequiredService<IOptions<OverpassProviderOptions>>()
                .Value;

            client.BaseAddress = new Uri(opts.OverpassBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds));
        });

        services.AddHttpClient(OverpassPlaceProvider.PhotonClientName, (sp, client) =>
        {
            var opts = sp
                .GetRequiredService<IOptions<OverpassProviderOptions>>()
                .Value;

            client.BaseAddress = new Uri(opts.PhotonBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
        });

        services.AddScoped<OverpassPlaceProvider>();
        services.AddScoped<IPlaceProvider>(sp =>
            sp.GetRequiredService<OverpassPlaceProvider>());
        services.AddScoped<IGeocodingService>(sp =>
            sp.GetRequiredService<OverpassPlaceProvider>());

        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);

        return services;
    }
}
