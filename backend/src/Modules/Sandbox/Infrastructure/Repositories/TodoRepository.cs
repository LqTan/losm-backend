using Microsoft.EntityFrameworkCore;
using Sandbox.Application.Abstractions;
using Sandbox.Domain;
using Sandbox.Infrastructure.Persistence;

namespace Sandbox.Infrastructure.Repositories;

public class TodoRepository : ITodoRepository
{
    private readonly SandboxDbContext _dbContext;
    public TodoRepository(SandboxDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task AddAsync(Todo todo)
    {
        _dbContext.Todos.Add(todo);
        await _dbContext.SaveChangesAsync();
    }
    public Task<Todo?> GetByIdAsync(int id)
    {
        return _dbContext.Todos.FirstOrDefaultAsync(x => x.Id == id);
    }
    public Task<List<Todo>> GetAllAsync()
    {
        return _dbContext.Todos.ToListAsync();
    }
}