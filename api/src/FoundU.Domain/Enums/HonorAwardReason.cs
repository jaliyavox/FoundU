namespace FoundU.Domain.Enums;

/// <summary>
/// Why someone earned honor points. Only outcomes a second party confirmed are here: pressing
/// "I found this" earns nothing on its own, or the board would reward clicking rather than
/// returning things.
/// </summary>
public enum HonorAwardReason
{
    /// <summary>A desk confirmed the item they found is now in storage.</summary>
    HandedInAtDesk,

    /// <summary>An item they helped find reached its owner.</summary>
    HelpedReturn
}
