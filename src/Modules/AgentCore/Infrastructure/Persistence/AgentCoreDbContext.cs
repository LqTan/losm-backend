using AgentCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentCore.Infrastructure.Persistence;

public class AgentCoreDbContext : DbContext
{
    public AgentCoreDbContext(
        DbContextOptions<AgentCoreDbContext> options
    ) : base(options)
    {
        
    }

    public DbSet<AgentSession> AgentSessions
        => Set<AgentSession>();
    public DbSet<AgentMessage> AgentMessages
        => Set<AgentMessage>();
    public DbSet<AgentToolCall> AgentToolCalls
        => Set<AgentToolCall>();
    public DbSet<AgentPlan> AgentPlans
        => Set<AgentPlan>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AgentCoreDbContext).Assembly
        );
    }
}
