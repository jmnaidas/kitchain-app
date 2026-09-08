namespace Kitchain.Application.Courts;

/// <summary>The two current published-venue reads; no generic CRUD or IQueryable exposure.</summary>
public interface ICourtDiscoveryReader
{
    Task<CourtPage> SearchAsync(CourtSearch search, CancellationToken cancellationToken);
    Task<CourtDetail?> FindPublishedAsync(Guid id, CancellationToken cancellationToken);
}
