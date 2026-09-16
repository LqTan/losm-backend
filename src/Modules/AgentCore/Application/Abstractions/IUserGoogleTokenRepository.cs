using AgentCore.Domain.Entities;

namespace AgentCore.Application.Abstractions;

public interface IUserGoogleTokenRepository
{
    Task<UserGoogleToken?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserGoogleToken?> GetByGoogleSubAsync(
        string googleSub,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        UserGoogleToken token,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        UserGoogleToken token,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}