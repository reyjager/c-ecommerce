using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMvcProject.Models;

namespace MyMvcProject.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> entity)
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id").UseIdentityAlwaysColumn();

            // Important: Do NOT configure UserId as an identity column
            entity.Property(e => e.UserId)
                .HasColumnName("UserId")
                .ValueGeneratedNever(); // This is crucial - we'll set it manually

            entity.Property(e => e.UserName).IsRequired().HasColumnName("UserName");
            entity.Property(e => e.Password).IsRequired().HasColumnName("Password");
            entity.Property(e => e.Email).IsRequired().HasColumnName("Email");
            entity.Property(e => e.Mobile).IsRequired().HasColumnName("Mobile");
            entity.Property(e => e.Roles).IsRequired(false).HasColumnName("Roles");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.DateCreated).HasColumnName("DateCreated").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.DateUpdated).HasColumnName("DateUpdated").IsRequired(false);
        }
    }
}