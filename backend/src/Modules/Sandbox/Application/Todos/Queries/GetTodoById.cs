using Sandbox.Application.Abstractions;
using Sandbox.Domain;

namespace Sandbox.Application.Todos.Queries;

public class GetTodoById
{
    private readonly ITodoRepository _repository;
    public GetTodoById(ITodoRepository repository)
    {
        _repository = repository;
    }
    public Task<Todo?> ExecuteAsync(int id)
    {
        return _repository.GetByIdAsync(id);
    }
}