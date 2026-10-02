namespace AgentCore.Infrastructure.Validation;

internal static class GroundingInstructions
{
    private const string FileName = "grounding-system.txt";

    public static string Load()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Prompts",
            FileName
        );

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Grounding prompt was not found: {path}"
            );
        }

        return File.ReadAllText(path);
    }
}
