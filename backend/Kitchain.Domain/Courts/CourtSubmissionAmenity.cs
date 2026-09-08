namespace Kitchain.Domain.Courts;

public sealed class CourtSubmissionAmenity
{
    private CourtSubmissionAmenity() { }

    internal CourtSubmissionAmenity(Guid courtSubmissionId, AmenityCode amenityCode)
    {
        if (!Enum.IsDefined(amenityCode)) throw new ArgumentOutOfRangeException(nameof(amenityCode));
        CourtSubmissionId = courtSubmissionId;
        AmenityCode = amenityCode;
    }

    public Guid CourtSubmissionId { get; private set; }
    public AmenityCode AmenityCode { get; private set; }
}
