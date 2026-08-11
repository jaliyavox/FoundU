namespace FoundU.Infrastructure.Persistence.Seed;

/// <summary>
/// Fixed GUIDs used by HasData() seed calls. Migrations require deterministic values
/// (Guid.NewGuid() would produce a new migration every time the project is rebuilt).
/// </summary>
public static class SeedIds
{
    public static readonly Guid AdminUserId = new("11111111-1111-1111-1111-111111111111");

    public static readonly Guid CategoryElectronics = new("21111111-1111-1111-1111-111111111111");
    public static readonly Guid CategoryBagsWallets = new("21111111-1111-1111-1111-111111111112");
    public static readonly Guid CategoryClothing = new("21111111-1111-1111-1111-111111111113");
    public static readonly Guid CategoryDocumentsCards = new("21111111-1111-1111-1111-111111111114");
    public static readonly Guid CategoryKeys = new("21111111-1111-1111-1111-111111111115");
    public static readonly Guid CategoryJewelryAccessories = new("21111111-1111-1111-1111-111111111116");
    public static readonly Guid CategoryBooksStationery = new("21111111-1111-1111-1111-111111111117");
    public static readonly Guid CategoryOther = new("21111111-1111-1111-1111-111111111118");

    public static readonly Guid LocationLibrary = new("31111111-1111-1111-1111-111111111111");
    public static readonly Guid LocationLectureHallB12 = new("31111111-1111-1111-1111-111111111112");
    public static readonly Guid LocationCafeteria = new("31111111-1111-1111-1111-111111111113");
    public static readonly Guid LocationSportsComplex = new("31111111-1111-1111-1111-111111111114");
    public static readonly Guid LocationMainAuditorium = new("31111111-1111-1111-1111-111111111115");
    public static readonly Guid LocationSecurityDeskBuildingA = new("31111111-1111-1111-1111-111111111116");
    public static readonly Guid LocationParkingLot = new("31111111-1111-1111-1111-111111111117");

    public static readonly Guid StorageSecurityDeskA = new("41111111-1111-1111-1111-111111111111");
}
