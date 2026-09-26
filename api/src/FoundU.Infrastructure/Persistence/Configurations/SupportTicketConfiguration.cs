using FoundU.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundU.Infrastructure.Persistence.Configurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("SupportTickets");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Subject).HasMaxLength(200).IsRequired();
        // Stored as text so reordering an enum cannot silently re-label every old ticket.
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(t => t.RelatedEntityType).HasMaxLength(60);
        builder.Property(t => t.LastActivityAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(t => t.ResolvedAt).HasColumnType("timestamptz");
        builder.Property(t => t.DeletedAt).HasColumnType("timestamptz");
        builder.Property(t => t.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.AssignedToUser)
            .WithMany()
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // The two ways this is read: one person's tickets, and the desk queue oldest first.
        builder.HasIndex(t => new { t.UserId, t.LastActivityAt });
        builder.HasIndex(t => new { t.Status, t.LastActivityAt });

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}

public class SupportTicketMessageConfiguration : IEntityTypeConfiguration<SupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<SupportTicketMessage> builder)
    {
        builder.ToTable("SupportTicketMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body).HasMaxLength(4000).IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(m => m.SupportTicket)
            .WithMany(t => t.Messages)
            .HasForeignKey(m => m.SupportTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.SupportTicketId, m.CreatedAt });
    }
}
