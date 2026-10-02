using Microsoft.EntityFrameworkCore;
using Sandbox.Domain;

namespace Sandbox.Infrastructure.Persistence;

public class SandboxDbContext : DbContext
{
    public SandboxDbContext(DbContextOptions<SandboxDbContext> options)
        : base(options)
    {}
    public DbSet<Todo> Todos => Set<Todo>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SandboxDbContext).Assembly
        );        
    }
}