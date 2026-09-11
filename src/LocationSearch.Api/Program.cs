using System.Text;
using AgentCore;
using AgentCore.Infrastructure.Persistence;
using LocationSearch.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Places.Infrastructure.Persistence;
using Reviews;
using Reviews.Infrastructure.Persistence;
using Sandbox;
using Sandbox.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Search;
using Users;
using Users.Application.Users.Commands.LoginUser;
using Users.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Location Search API",
            Version = "v1",
            Description = "Backend API for the Location Search platform."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] =
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Bearer token. Example: \"Bearer {token}\""
            };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        var requirement = new OpenApiSecurityRequirement();
        requirement.Add(
            new OpenApiSecuritySchemeReference(
                JwtBearerDefaults.AuthenticationScheme,
                document),
            new List<string>());
        document.Security.Add(requirement);

        return Task.CompletedTask;
    });
});
builder.Services.AddSandbox(
    builder.Configuration.GetConnectionString("DefaultConnection")!);
builder.Services.AddPlaces(builder.Configuration);
builder.Services.AddSearch();
builder.Services.AddReviews(builder.Configuration);
builder.Services.AddUsers(builder.Configuration);
builder.Services.AddAgentCore(builder.Configuration);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:SecretKey"]!
                )
            )
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Cookies.TryGetValue(
                    "scalar_token", out var cookieToken))
                {
                    ctx.Token = cookieToken;
                }
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured");

var dbName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
{
    var masterCs = new SqlConnectionStringBuilder(connectionString)
    {
        InitialCatalog = "master"
    };
    await using var conn = new SqlConnection(masterCs.ConnectionString);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand(
        $"IF DB_ID(@db) IS NULL CREATE DATABASE [{dbName}]", conn);
    cmd.Parameters.AddWithValue("@db", dbName);
    await cmd.ExecuteNonQueryAsync();
}

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    await sp.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
    await sp.GetRequiredService<ReviewsDbContext>().Database.MigrateAsync();
    await sp.GetRequiredService<PlacesDbContext>().Database.MigrateAsync();
    await sp.GetRequiredService<SandboxDbContext>().Database.MigrateAsync();
    await sp.GetRequiredService<AgentCoreDbContext>().Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}
else
{
    app.MapOpenApi().RequireAuthorization();
    app.MapScalarApiReference().RequireAuthorization();
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();

app.MapPost("/scalar/login", async (
    LoginUserCommand cmd,
    LoginUserHandler handler,
    HttpContext http,
    IConfiguration config) =>
{
    var result = await handler.HandleAsync(cmd);
    var requireSecure = config.GetValue<bool>(
        "ScalarAuth:SecureCookie", false);
    http.Response.Cookies.Append("scalar_token", result.Token,
        new CookieOptions
        {
            HttpOnly = true,
            Secure = requireSecure,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });
    return Results.Ok(
        new { result.Id, result.Username, result.Email });
}).AllowAnonymous();

app.MapPost("/scalar/logout", (HttpContext http) =>
{
    http.Response.Cookies.Delete("scalar_token");
    return Results.Ok();
}).AllowAnonymous();

app.MapControllers();

app.Run();
