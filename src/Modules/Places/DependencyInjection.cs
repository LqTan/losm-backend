using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddHttpClient<IPlaceProvider, HerePlaceProvider>(client =>
        {
            client.BaseAddress = new Uri("https://discover.search.hereapi.com/");
        });

        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);

        return services;
    }
}
