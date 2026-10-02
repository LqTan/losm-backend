using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Abstractions;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Queries.GetAverageRatingByPlace;
using Reviews.Application.Reviews.Queries.GetReviewsByPlace;
using Reviews.Application.Reviews.Queries.GetReviewsByUser;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Repositories;

namespace Reviews;

public static class DependencyInjection
{
    public static IServiceCollection AddReviews(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<ReviewsDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")
            ));
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<CreateReviewHandler>();
        services.AddScoped<GetReviewsByPlaceHandler>();
        services.AddScoped<GetAverageRatingByPlaceHandler>();
        services.AddScoped<GetReviewsByUserHandler>();
        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);
        return services;
    }
}