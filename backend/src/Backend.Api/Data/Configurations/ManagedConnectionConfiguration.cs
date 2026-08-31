using Backend.Api.Models.Entities;
using Backend.Api.Models.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class ManagedConnectionConfiguration : IEntityTypeConfiguration<ManagedConnection>
    {
        public void Configure(EntityTypeBuilder<ManagedConnection> builder)
        {
            builder.ToTable("managed_connection");
            builder.HasKey(connection => connection.ID);

            builder.Property(connection => connection.ID).ValueGeneratedNever();
            builder.Property(connection => connection.UserID).IsRequired();
            builder.Property(connection => connection.Name).IsRequired().HasMaxLength(WorkflowLimits.ConnectionNameMaxLength);
            builder.Property(connection => connection.ConnectionType).IsRequired().HasMaxLength(WorkflowLimits.ConnectionTypeMaxLength);
            builder.Property(connection => connection.CanonicalOrigin).IsRequired().HasMaxLength(WorkflowLimits.ConnectionCanonicalOriginMaxLength);
            builder.Property(connection => connection.CredentialType).IsRequired().HasMaxLength(WorkflowLimits.ConnectionCredentialTypeMaxLength);
            builder.Property(connection => connection.SecretReference).IsRequired().HasMaxLength(WorkflowLimits.ConnectionSecretReferenceMaxLength);
            builder.Property(connection => connection.CredentialPlacement).IsRequired().HasMaxLength(WorkflowLimits.ConnectionCredentialPlacementMaxLength);
            builder.Property(connection => connection.IsEnabled).IsRequired();
            builder.Property(connection => connection.CreatedAt).IsRequired();
            builder.Property(connection => connection.UpdatedAt).IsRequired();

            builder.HasIndex(connection => new
            {
                connection.UserID,
                connection.Name
            })
                .IsUnique()
                .HasDatabaseName("IX_managed_connection_user_id_name");

            builder.HasIndex(connection => new
            {
                connection.UserID,
                connection.ConnectionType
            });

            builder.HasOne(connection => connection.User)
                .WithMany()
                .HasForeignKey(connection => connection.UserID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}