using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMvcProject.Models;

namespace MyMvcProject.Data.Configurations
{
    public class BranchConfiguration : IEntityTypeConfiguration<Branch>
    {
        public void Configure(EntityTypeBuilder<Branch> builder)
        {
            builder.ToTable("Branches");
            
            builder.HasKey(b => b.Id);
            
            builder.Property(b => b.BranchId)
                .IsRequired();
            
            builder.Property(b => b.BranchName)
                .IsRequired()
                .HasMaxLength(100);
            
            builder.Property(b => b.BranchAddress)
                .HasMaxLength(200);
            
            builder.Property(b => b.ContactNumber)
                .HasMaxLength(50);
            
            builder.Property(b => b.LocationsString)
                .HasMaxLength(500);
            
            builder.Property(b => b.IsActive)
                .IsRequired()
                .HasDefaultValue(true);
            
            builder.Property(b => b.DateCreated)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            builder.Property(b => b.DateUpdated);
            
            // Add unique constraint on BranchId
            builder.HasIndex(b => b.BranchId)
                .IsUnique();
        }
    }
}