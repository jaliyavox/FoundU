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

    // ItemTypes under Electronics
    public static readonly Guid ItemTypeLaptop = new("51111111-1111-1111-1111-111111111111");
    public static readonly Guid ItemTypePhone = new("51111111-1111-1111-1111-111111111112");
    public static readonly Guid ItemTypeHeadphones = new("51111111-1111-1111-1111-111111111113");
    public static readonly Guid ItemTypeEarphones = new("51111111-1111-1111-1111-111111111114");

    // ItemTypes under Bags & Wallets
    public static readonly Guid ItemTypeBackpack = new("51111111-1111-1111-1111-111111111115");
    public static readonly Guid ItemTypeLaptopBag = new("51111111-1111-1111-1111-111111111116");
    public static readonly Guid ItemTypePurse = new("51111111-1111-1111-1111-111111111117");
    public static readonly Guid ItemTypeWallet = new("51111111-1111-1111-1111-111111111118");

    // ItemTypes under Clothing
    public static readonly Guid ItemTypeJacket = new("51111111-1111-1111-1111-111111111119");
    public static readonly Guid ItemTypeHoodie = new("51111111-1111-1111-1111-11111111111a");
    public static readonly Guid ItemTypeScarf = new("51111111-1111-1111-1111-11111111111b");
    public static readonly Guid ItemTypeCap = new("51111111-1111-1111-1111-11111111111c");
    public static readonly Guid ItemTypeUmbrella = new("51111111-1111-1111-1111-11111111111d");

    // ItemTypes under Documents & Cards
    public static readonly Guid ItemTypeStudentCard = new("51111111-1111-1111-1111-11111111111e");
    public static readonly Guid ItemTypeIdCard = new("51111111-1111-1111-1111-11111111111f");
    public static readonly Guid ItemTypeBusPass = new("51111111-1111-1111-1111-111111111120");
    public static readonly Guid ItemTypeBankCard = new("51111111-1111-1111-1111-111111111121");

    // ItemTypes under Keys
    public static readonly Guid ItemTypeHouseKeys = new("51111111-1111-1111-1111-111111111122");
    public static readonly Guid ItemTypeCarKeys = new("51111111-1111-1111-1111-111111111123");
    public static readonly Guid ItemTypeLockerKey = new("51111111-1111-1111-1111-111111111124");

    // ItemTypes under Jewelry & Accessories
    public static readonly Guid ItemTypeWatch = new("51111111-1111-1111-1111-111111111125");
    public static readonly Guid ItemTypeGlasses = new("51111111-1111-1111-1111-111111111126");
    public static readonly Guid ItemTypeRing = new("51111111-1111-1111-1111-111111111127");
    public static readonly Guid ItemTypeBracelet = new("51111111-1111-1111-1111-111111111128");

    // ItemTypes under Books & Stationery
    public static readonly Guid ItemTypeTextbook = new("51111111-1111-1111-1111-111111111129");
    public static readonly Guid ItemTypeNotebook = new("51111111-1111-1111-1111-11111111112a");
    public static readonly Guid ItemTypeCalculator = new("51111111-1111-1111-1111-11111111112b");
    public static readonly Guid ItemTypePencilCase = new("51111111-1111-1111-1111-11111111112c");

    // ItemTypes under Other
    public static readonly Guid ItemTypeWaterBottle = new("51111111-1111-1111-1111-11111111112d");
    public static readonly Guid ItemTypeLunchBox = new("51111111-1111-1111-1111-11111111112e");
    public static readonly Guid ItemTypeSportsGear = new("51111111-1111-1111-1111-11111111112f");
    public static readonly Guid ItemTypeCharger = new("51111111-1111-1111-1111-111111111130");

    // Additional storage locations - every found report needs one, and a single desk for
    // the whole campus is not a realistic demo.
    public static readonly Guid StorageLibraryDesk = new("41111111-1111-1111-1111-111111111112");
    public static readonly Guid StorageSportsComplexOffice = new("41111111-1111-1111-1111-111111111113");
    public static readonly Guid StorageStudentServices = new("41111111-1111-1111-1111-111111111114");

    // Identity documents - highlighted, because losing one is urgent.
    public static readonly Guid CategoryIdentityDocuments = new("21111111-1111-1111-1111-111111111119");

    public static readonly Guid ItemTypeUniversityId = new("51111111-1111-1111-1111-111111111131");
    public static readonly Guid ItemTypeNationalId = new("51111111-1111-1111-1111-111111111132");
    public static readonly Guid ItemTypeDrivingLicence = new("51111111-1111-1111-1111-111111111133");
    public static readonly Guid ItemTypePassport = new("51111111-1111-1111-1111-111111111134");

    // "Other" exists in every category, so nobody is blocked by a missing item type.
    public static readonly Guid ItemTypeOtherElectronics = new("51111111-1111-1111-1111-111111111141");
    public static readonly Guid ItemTypeOtherBags = new("51111111-1111-1111-1111-111111111142");
    public static readonly Guid ItemTypeOtherClothing = new("51111111-1111-1111-1111-111111111143");
    public static readonly Guid ItemTypeOtherDocuments = new("51111111-1111-1111-1111-111111111144");
    public static readonly Guid ItemTypeOtherKeys = new("51111111-1111-1111-1111-111111111145");
    public static readonly Guid ItemTypeOtherJewelry = new("51111111-1111-1111-1111-111111111146");
    public static readonly Guid ItemTypeOtherBooks = new("51111111-1111-1111-1111-111111111147");
    public static readonly Guid ItemTypeOtherOther = new("51111111-1111-1111-1111-111111111148");
    public static readonly Guid ItemTypeOtherIdentity = new("51111111-1111-1111-1111-111111111149");
}
