using Microsoft.EntityFrameworkCore;
using MyMvcProject.Models;

namespace MyMvcProject.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            // Ensure database is created
            try
            {
                Database.EnsureCreated();
            }
            catch
            {
                // Ignore errors here, they will be handled elsewhere
            }
        }

        public DbSet<User> Users { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id").UseIdentityAlwaysColumn();
                entity.Property(e => e.UserName).IsRequired().HasColumnName("UserName");
                entity.Property(e => e.Password).IsRequired().HasColumnName("Password");
                entity.Property(e => e.Email).IsRequired().HasColumnName("Email");
                entity.Property(e => e.Mobile).IsRequired().HasColumnName("Mobile");
                entity.Property(e => e.Roles).IsRequired(false).HasColumnName("Roles");
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
            base.OnConfiguring(optionsBuilder);
        }
    }
}