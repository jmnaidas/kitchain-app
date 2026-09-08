using System.ComponentModel.DataAnnotations;

namespace Kitchain.Application.Courts;

public sealed class CourtDiscoveryService(ICourtDiscoveryReader reader)
{
    public Task<CourtPage> SearchAsync(CourtSearch search, CancellationToken cancellationToken = default)
    {
        // Shared validation also protects non-HTTP callers of this use case.
        Validator.ValidateObject(search, new ValidationContext(search), validateAllProperties: true);
        return reader.SearchAsync(search with
        {
            City = string.IsNullOrWhiteSpace(search.City) ? null : search.City.Trim(),
            CurrencyCode = search.CurrencyCode?.ToUpperInvariant() ??
                (search.MaxStartingPrice.HasValue ? "PHP" : null)
        }, cancellationToken);
    }

    public Task<CourtDetail?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        reader.FindPublishedAsync(id, cancellationToken);
}
