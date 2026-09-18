using Kitchain.Domain.Gear;

namespace Kitchain.Tests.Gear;

public sealed class GearDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("", "labs")]
    [InlineData("   ", "labs")]
    [InlineData("Labs", "bad slug")]
    [InlineData("Labs", "bad--slug")]
    public void Brand_requires_name_and_valid_slug(string name, string slug) =>
        Assert.Throws<ArgumentException>(() => new GearBrand(Guid.NewGuid(), name, slug, Now));

    [Fact]
    public void Identity_normalizes_matching_keys_without_conflating_variants()
    {
        var brand = new GearBrand(Guid.NewGuid(), " Kitchain  Labs ", "KITCHAIN-LABS", Now);
        Assert.Equal("KITCHAIN LABS", brand.NormalizedName);
        Assert.Equal("kitchain-labs", brand.Slug);
        var variant = new PaddleVariant(Guid.NewGuid(), Guid.NewGuid(), "16 mm", "16-mm", Now, " lab-16 ");
        Assert.Equal("LAB-16", variant.NormalizedSku);
        Assert.Throws<ArgumentException>(() => new Paddle(Guid.NewGuid(), Guid.Empty, "Test", "test", Now));
        Assert.Throws<ArgumentException>(() => new Paddle(Guid.NewGuid(), brand.Id, " ", "test", Now));
        Assert.Throws<ArgumentException>(() => new Paddle(Guid.NewGuid(), brand.Id, "Test", "test", Now, releaseYear: 1900));
        Assert.Throws<ArgumentException>(() => new PaddleVariant(Guid.NewGuid(), Guid.NewGuid(), " ", "test", Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000000)]
    [InlineData(0.0001)]
    public void Every_numeric_spec_uses_positive_explicit_precision(decimal value)
    {
        Assert.Throws<ArgumentException>(() => new GearSpecifications(thicknessMm: value));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(lengthInches: value));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(widthInches: value));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(handleLengthInches: value));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(gripCircumferenceInches: value));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(advertisedWeightMinOz: value));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(advertisedWeightMaxOz: value));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(swingWeightKgCm2: value));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(twistWeightKgCm2: value));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(measuredWeightOz: value));
    }

    [Fact]
    public void Specs_preserve_unknowns_and_reject_contradictory_ranges()
    {
        Assert.Null(new GearSpecifications().ThicknessMm);
        Assert.Throws<ArgumentException>(() => new GearSpecifications(advertisedWeightMinOz: 9, advertisedWeightMaxOz: 8));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(lengthInches: 5, handleLengthInches: 6));
        Assert.Throws<ArgumentException>(() => new GearSpecifications(shape: (PaddleShape)99));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void All_derived_rating_bounds_are_enforced(int rating)
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new PaddlePerformanceProfile(id, "test-v1", Now, power: rating));
        Assert.Throws<ArgumentException>(() => new PaddlePerformanceProfile(id, "test-v1", Now, control: rating));
        Assert.Throws<ArgumentException>(() => new PaddlePerformanceProfile(id, "test-v1", Now, forgiveness: rating));
        Assert.Throws<ArgumentException>(() => new PaddlePerformanceProfile(id, "test-v1", Now, handSpeed: rating));
        Assert.Throws<ArgumentException>(() => new PaddlePerformanceProfile(id, "test-v1", Now, spin: rating));
        Assert.Throws<ArgumentException>(() => new PaddlePerformanceProfile(id, "test-v1", Now, sweetSpot: rating));
        Assert.Throws<ArgumentException>(() => new PaddlePerformanceProfile(id, " ", Now));
    }

    [Theory]
    [InlineData("file:///tmp/test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.test")]
    [InlineData("not-a-url")]
    public void Sources_reject_unsafe_reference_urls(string url) =>
        Assert.Throws<ArgumentException>(() => new GearDataSource(Guid.NewGuid(), GearSourceType.Manufacturer, "Synthetic", url, Now, Now));

    [Fact]
    public void Sources_require_external_reference_and_monotonic_freshness()
    {
        Assert.Throws<ArgumentException>(() => new GearDataSource(Guid.NewGuid(), GearSourceType.Manufacturer, "Synthetic", null, Now, Now));
        var source = new GearDataSource(Guid.NewGuid(), GearSourceType.Manual, "Synthetic", null, Now, Now);
        source.Checked(Now.AddDays(1));
        Assert.Throws<ArgumentException>(() => source.Checked(Now));
        Assert.Equal(Now.AddDays(1), source.LastCheckedAt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.001)]
    [InlineData(10000000000)]
    public void Listings_reject_invalid_price(decimal price) =>
        Assert.Throws<ArgumentException>(() => Listing(price, "USD"));

    private static PaddleListing Listing(decimal? price, string currency) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Fictional seller", "https://example.test/synthetic", currency, price, null, null, Now, Now);

    [Fact]
    public void Listings_allow_unknown_or_zero_price_but_require_currency()
    {
        Assert.Null(Listing(null, "php").Price);
        Assert.Equal("PHP", Listing(0, "php").CurrencyCode);
        Assert.Throws<ArgumentException>(() => Listing(1, "US"));
        Assert.Throws<ArgumentException>(() => Listing(1, "1SD"));
    }

    [Fact]
    public void Publishing_is_one_way_and_failed_transitions_do_not_mutate()
    {
        var paddle = new Paddle(Guid.NewGuid(), Guid.NewGuid(), "Synthetic", "synthetic", Now);
        Assert.Throws<ArgumentException>(() => paddle.SetStatus(GearPublicationStatus.Published, Now.AddDays(-1)));
        Assert.Equal(GearPublicationStatus.Draft, paddle.Status);
        paddle.SetStatus(GearPublicationStatus.Published, Now);
        Assert.Throws<GearConflictException>(() => paddle.SetStatus(GearPublicationStatus.Published, Now));
        paddle.SetStatus(GearPublicationStatus.Archived, Now);
        Assert.Throws<GearConflictException>(() => paddle.SetStatus(GearPublicationStatus.Published, Now));
        var variant = new PaddleVariant(Guid.NewGuid(), paddle.Id, "16 mm", "16-mm", Now);
        variant.SetStatus(GearPublicationStatus.Archived, Now);
        Assert.Throws<GearConflictException>(() => variant.SetStatus(GearPublicationStatus.Draft, Now));
        var profile = new PaddlePerformanceProfile(variant.Id, "synthetic-v1", Now, control: 10);
        profile.SetStatus(GearPublicationStatus.Published, Now);
        profile.SetStatus(GearPublicationStatus.Archived, Now);
        Assert.Throws<GearConflictException>(() => profile.SetStatus(GearPublicationStatus.Published, Now));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Candidate_decisions_are_final_and_validation_is_atomic(bool approve)
    {
        var c = new GearImportCandidate(Guid.NewGuid(), Guid.NewGuid(), "Kitchain Labs", "kitchain-labs",
            "Baseline One", "baseline-one", "16 mm", "16-mm", null, GearEvidenceType.ManufacturerStated, new(), Now, Now);
        Assert.Throws<ArgumentException>(() => c.Decide(Guid.NewGuid(), new string('x', 2001), Now));
        Assert.Equal(GearImportStatus.Pending, c.Status);
        Assert.Null(c.ReviewedAt);
        c.Decide(approve ? Guid.NewGuid() : null, "Synthetic review", Now);
        Assert.Equal(approve ? GearImportStatus.Approved : GearImportStatus.Rejected, c.Status);
        Assert.Throws<GearConflictException>(() => c.Decide(Guid.NewGuid(), null, Now));
        Assert.Throws<GearConflictException>(() => c.Decide(null, null, Now));
    }
}
