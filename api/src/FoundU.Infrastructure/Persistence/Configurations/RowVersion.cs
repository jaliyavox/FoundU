using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

internal static class RowVersion
{
    /// <summary>
    /// Optimistic concurrency on PostgreSQL's own row version (the <c>xmin</c> system column,
    /// so there is no column to add). Two staff deciding the same thing at the same moment -
    /// approving rival claims on one item, receiving the same handover code - would otherwise
    /// both succeed, the second silently overwriting the first. With this the second save
    /// fails, and GlobalExceptionHandler answers it with 409 "someone else changed this".
    /// </summary>
    public static void HasRowVersion<T>(this EntityTypeBuilder<T> builder) where T : class
        => builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
}
