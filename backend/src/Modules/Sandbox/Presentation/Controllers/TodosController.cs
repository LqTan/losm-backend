using Microsoft.AspNetCore.Mvc;
using Sandbox.Application.Todos.Commands;
using Sandbox.Application.Todos.Queries;

namespace Sandbox.Presentation.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController : ControllerBase
{
    private readonly CreateTodo _createTodo;
    private readonly GetTodos _getTodos;
    private readonly GetTodoById _getTodoById;

    public TodosController(
        CreateTodo createTodo,
        GetTodos getTodos,
        GetTodoById getTodoById
    )
    {
        _createTodo = createTodo;
        _getTodos = getTodos;
        _getTodoById = getTodoById;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTodoRequest request)
    {
        var id = await _createTodo.ExecuteAsync(request.Title);
        return Ok(new { Id = id });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _getTodos.ExecuteAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var todo = await _getTodoById.ExecuteAsync(id);
        return todo is null ? NotFound() : Ok(todo);
    }
}

public record CreateTodoRequest(string Title);