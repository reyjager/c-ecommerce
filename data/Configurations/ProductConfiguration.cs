using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMvcProject.Models;

namespace MyMvcProject.Data.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> entity)
        {
            entity.ToTable("Products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id").UseIdentityAlwaysColumn();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasColumnName("Name")
                .HasMaxLength(100);

            entity.Property(e => e.Description)
                .HasColumnName("Description")
                .HasMaxLength(500);

            entity.Property(e => e.Price)
                .IsRequired()
                .HasColumnName("Price")
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.StockQuantity)
                .IsRequired()
                .HasColumnName("StockQuantity");

            entity.Property(e => e.ImageUrl)
                .HasColumnName("ImageUrl")
                .HasMaxLength(200);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasColumnName("IsActive")
                .HasDefaultValue(true);

            entity.Property(e => e.DateCreated)
                .IsRequired()
                .HasColumnName("DateCreated")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.DateUpdated)
                .HasColumnName("DateUpdated");

            // Add indexes for frequently queried columns
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Price);
            entity.HasIndex(e => e.IsActive);

            // If you have a Category relationship, you would configure it like this:
            // entity.HasOne(e => e.Category)
            //     .WithMany(c => c.Products)
            //     .HasForeignKey(e => e.CategoryId)
            //     .OnDelete(DeleteBehavior.Restrict);
        }
    }
}