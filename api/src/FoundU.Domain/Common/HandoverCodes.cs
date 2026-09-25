using System.Security.Cryptography;

namespace FoundU.Domain.Common;

/// <summary>
/// The two six-digit codes the desk works with.
///
/// A <b>hand-in code</b> is printed on every lost report. A finder quotes it at the desk, and
/// staff type it to link the item to the right report instantly - it is routing, not proof,
/// which is why it is fine for it to be visible on the public feed.
///
/// A <b>collection code</b> is issued to the owner when their claim is approved. They quote it
/// at the desk to collect; it works once and dies when the item is marked collected. It is a
/// receipt for a decision already made, never a substitute for the verification questions.
///
/// Both are drawn from a cryptographic source, never a counter: a sequential code would let
/// anyone guess the next post's code from their own.
/// </summary>
public static class HandoverCodes
{
    public const int Length = 6;

    public static string Generate()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>Digits only, the right length - checked before any lookup so a typo is a 400, not a miss.</summary>
    public static bool LooksValid(string? code)
        => code is not null && code.Length == Length && code.All(char.IsAsciiDigit);

    /// <summary>"483921" reads as "483 921" on a card or a screen.</summary>
    public static string Display(string code)
        => code.Length == Length ? $"{code[..3]} {code[3..]}" : code;
}
