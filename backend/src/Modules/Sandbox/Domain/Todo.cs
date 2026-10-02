namespace Sandbox.Domain;

public class Todo
{
    public int Id { get; private set; }
    public string Title { get; private set; } = null!;
    public bool IsCompleted { get; private set; }

    private Todo(){}

    public Todo(string title)
    {
        Title = title;
        IsCompleted = false;
    }
    public void Complete()
    {
        IsCompleted = true;
    }
}