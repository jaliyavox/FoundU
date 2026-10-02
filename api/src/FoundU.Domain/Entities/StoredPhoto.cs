using FoundU.Domain.Common;

namespace FoundU.Domain.Entities;

/// <summary>
/// An uploaded photo, kept in the database. Hosts with throwaway disks (Render's free plan
/// wipes a service's disk on every restart and deploy) lost every photo saved as a file; the
/// database is the one store such a host keeps. Photos are capped at 5 MB, two per report.
/// </summary>
public class StoredPhoto : BaseEntity
{
    public string ContentType { get; set; } = default!;
    public byte[] Data { get; set; } = default!;
}
