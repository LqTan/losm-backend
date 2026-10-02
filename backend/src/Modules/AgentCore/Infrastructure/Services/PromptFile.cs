namespace AgentCore.Infrastructure.Services;

internal static class PromptFile
{
    public static string Load(string fileName)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Prompts",
            fileName
        );
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Prompt was not found: {path}"
            );
        }

        return File.ReadAllText(path);
    }
}
