using Agent.Domain;

namespace Agent.Application.Abstractions;

public interface IPromptBuilder
{
    string BuildPlannerPrompt(string userQuery, AgentPlan? previousPlan = null);
}