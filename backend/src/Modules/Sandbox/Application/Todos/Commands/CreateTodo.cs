using Sandbox.Application.Abstractions;
using Sandbox.Domain;

namespace Sandbox.Application.Todos.Commands;

public class CreateTodo
{
    private readonly ITodoRepository _repository;
    public CreateTodo(ITodoRepository repository)
    {
        _repository = repository;
    }
    public async Task<int> ExecuteAsync(string title)
    {
        var todo = new Todo(title);
        await _repository.AddAsync(todo);
        return todo.Id;
    }
}