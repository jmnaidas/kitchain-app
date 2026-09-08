namespace Kitchain.Domain.Courts;

public sealed class CourtAmenity
{
    private CourtAmenity() { }

    internal CourtAmenity(Guid courtId, AmenityCode amenityCode)
    {
        if (!Enum.IsDefined(amenityCode)) throw new ArgumentOutOfRangeException(nameof(amenityCode));
        CourtId = courtId;
        AmenityCode = amenityCode;
    }

    public Guid CourtId { get; private set; }
    public AmenityCode AmenityCode { get; private set; }
}
