using Agent.Domain;

namespace Agent.Application.Abstractions;

public interface IPlanValidator
{
    ValidationDecision Validate(
        AgentPlan plan,
        AgentAction action,
        bool hasUserLocation);
}