using Sandbox.Domain;

namespace Sandbox.Application.Abstractions;

public interface ITodoRepository
{
    Task AddAsync(Todo todo);
    Task<Todo?> GetByIdAsync(int id);
    Task<List<Todo>> GetAllAsync();
}