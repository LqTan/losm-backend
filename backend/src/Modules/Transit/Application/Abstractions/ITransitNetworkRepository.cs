using Transit.Domain.Models;
namespace Transit.Application.Abstractions;

public interface ITransitNetworkRepository
{
    Task<TransitNetwork> ReadActiveAsync(string? mode, CancellationToken ct = default);
}
