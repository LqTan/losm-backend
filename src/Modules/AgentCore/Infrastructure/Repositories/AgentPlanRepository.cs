using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using AgentCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentCore.Infrastructure.Repositories;

public sealed class AgentPlanRepository
    : IAgentPlanRepository
{
    private readonly AgentCoreDbContext _dbContext;

    public AgentPlanRepository(
        AgentCoreDbContext dbContext
    )
    {
        _dbContext = dbContext;
    }

    public async Task<AgentPlan?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _dbContext.AgentPlans
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken
            );
    }

    public async Task AddAsync(
        AgentPlan plan,
        CancellationToken cancellationToken = default
    )
    {
        await _dbContext.AgentPlans.AddAsync(
            plan,
            cancellationToken
        );

        await _dbContext.SaveChangesAsync(
            cancellationToken
        );
    }

    public async Task UpdateAsync(
        AgentPlan plan,
        CancellationToken cancellationToken = default
    )
    {
        await _dbContext.SaveChangesAsync(
            cancellationToken
        );
    }
}
