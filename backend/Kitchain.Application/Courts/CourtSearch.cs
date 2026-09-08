using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using Kitchain.Domain.Courts;

namespace Kitchain.Application.Courts;

public sealed record CourtSearch
{
    [StringLength(100)]
    public string? City { get; init; }

    [EnumDataType(typeof(IndoorOutdoorType))]
    public IndoorOutdoorType? IndoorOutdoor { get; init; }

    [Range(1, int.MaxValue)]
    public int? MinCourts { get; init; }

    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal? MaxStartingPrice { get; init; }

    /// <summary>Currency for price comparison, or an explicit currency filter. Defaults to PHP when a price cap is supplied.</summary>
    [RegularExpression(@"^[A-Za-z]{3}$")]
    public string? CurrencyCode { get; init; }

    [EnumDataType(typeof(AmenityCode))]
    public AmenityCode? Amenity { get; init; }

    [Range(1, 1000000)]
    [DefaultValue(1)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    [DefaultValue(20)]
    public int PageSize { get; init; } = 20;
}
