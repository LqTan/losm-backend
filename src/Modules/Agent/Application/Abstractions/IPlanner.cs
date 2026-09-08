using Agent.Domain;

namespace Agent.Application.Abstractions;

public interface IPlanner
{
    AgentPlan CreatePlan(string query, bool hasUserLocation);
}