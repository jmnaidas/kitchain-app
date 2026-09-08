namespace Kitchain.Domain.Courts;

/// <summary>A fixed lookup vocabulary; codes are persisted as readable strings.</summary>
public sealed class Amenity
{
    private Amenity() { }

    public Amenity(AmenityCode code)
    {
        if (!Enum.IsDefined(code)) throw new ArgumentOutOfRangeException(nameof(code));
        Code = code;
    }

    public AmenityCode Code { get; private set; }
}
