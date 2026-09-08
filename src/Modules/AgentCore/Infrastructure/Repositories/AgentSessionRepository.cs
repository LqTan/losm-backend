using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using AgentCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentCore.Infrastructure.Repositories;

public class AgentSessionRepository : IAgentSessionRepository
{
    private readonly AgentCoreDbContext _dbContext;
    public AgentSessionRepository(
        AgentCoreDbContext dbContext
    )
    {
        _dbContext = dbContext;
    }

    public async Task<AgentSession?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _dbContext.AgentSessions
            .Include(x => x.Messages)
            .Include(x => x.ToolCalls)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken
            );
    }

    public async Task AddAsync(
        AgentSession session,
        CancellationToken cancellationToken = default
    )
    {
        await _dbContext.AgentSessions.AddAsync(
            session,
            cancellationToken
        );
        await _dbContext.SaveChangesAsync(
            cancellationToken
        );
    }

    public async Task UpdateAsync(
        AgentSession session,
        CancellationToken cancellationToken = default
    )
    {        
        await _dbContext.SaveChangesAsync(
            cancellationToken
        );
    }
}
