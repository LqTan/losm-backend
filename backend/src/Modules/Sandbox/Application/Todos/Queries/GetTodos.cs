using Sandbox.Application.Abstractions;
using Sandbox.Domain;

namespace Sandbox.Application.Todos.Queries;

public class GetTodos
{
    private readonly ITodoRepository _repository;

    public GetTodos(ITodoRepository repository)
    {
        _repository = repository;
    }
    public Task<List<Todo>> ExecuteAsync()
    {
        return _repository.GetAllAsync();
    }
}