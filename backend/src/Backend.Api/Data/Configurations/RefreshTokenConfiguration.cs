using Backend.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("refresh_token");

            builder.HasKey(token => token.Id);

            builder.Property(token => token.Id)
                .ValueGeneratedNever();

            builder.Property(token => token.UserId)
                .IsRequired();

            builder.Property(token => token.TokenHash)
                .IsRequired()
                .HasMaxLength(64)
                .IsFixedLength();

            builder.Property(token => token.FamilyId)
                .IsRequired();

            builder.Property(token => token.CreatedAt)
                .IsRequired();

            builder.Property(token => token.ExpiresAt)
                .IsRequired();

            builder.Property(token => token.RevocationReason)
                .HasMaxLength(100);

            builder.HasIndex(token => token.TokenHash)
                .IsUnique();

            builder.HasIndex(token => new
            {
                token.UserId,
                token.RevokedAt,
                token.ExpiresAt
            });

            builder.HasIndex(token => token.FamilyId);

            builder.HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<RefreshToken>()
                .WithOne()
                .HasForeignKey<RefreshToken>(token => token.ReplacedByTokenId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
