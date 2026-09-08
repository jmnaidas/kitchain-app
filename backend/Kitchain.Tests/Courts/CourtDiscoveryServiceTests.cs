using System.ComponentModel.DataAnnotations;
using Kitchain.Application.Courts;

namespace Kitchain.Tests.Courts;

public sealed class CourtDiscoveryServiceTests
{
    [Fact]
    public async Task Normalizes_filters_and_scopes_price_comparison_to_php_by_default()
    {
        var reader = new RecordingReader();
        var service = new CourtDiscoveryService(reader);
        using var cancellation = new CancellationTokenSource();
        await service.SearchAsync(new CourtSearch { City = " Makati ", MaxStartingPrice = 500 }, cancellation.Token);
        Assert.Equal("Makati", reader.Search?.City);
        Assert.Equal("PHP", reader.Search?.CurrencyCode);
        Assert.Equal(cancellation.Token, reader.Token);
        await service.SearchAsync(new CourtSearch { CurrencyCode = "usd" });
        Assert.Equal("USD", reader.Search?.CurrencyCode);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task Invalid_pagination_is_rejected_before_persistence(int page, int pageSize)
    {
        var reader = new RecordingReader();
        await Assert.ThrowsAsync<ValidationException>(() => new CourtDiscoveryService(reader)
            .SearchAsync(new CourtSearch { Page = page, PageSize = pageSize }));
        Assert.Null(reader.Search);
    }

    private sealed class RecordingReader : ICourtDiscoveryReader
    {
        public CourtSearch? Search { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<CourtPage> SearchAsync(CourtSearch search, CancellationToken cancellationToken)
        {
            Search = search;
            Token = cancellationToken;
            return Task.FromResult(new CourtPage([], search.Page, search.PageSize, 0));
        }
        public Task<CourtDetail?> FindPublishedAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<CourtDetail?>(null);
    }
}
