namespace Kitchain.Domain.Courts;

public enum IndoorOutdoorType { Indoor, Outdoor, Mixed }
public enum BookingMethod { ExternalPlatform, Website, GoogleForm, Phone, Message, WalkIn, Other }
public enum CourtStatus { Draft, Published, Inactive }
public enum CourtDataSource { OwnerSupplied, CommunitySupplied, KitchainCurated }
public enum PriceUnit { PerHour, PerPerson, PerSession }
public enum AmenityCode
{
    Parking, Restroom, Shower, ChangingRoom, AirConditioning, PaddleRental,
    BallOrEquipmentRental, ProShop, FoodAndDrinks, SeatingOrWaitingArea,
    Coaching, Lockers, Other
}
