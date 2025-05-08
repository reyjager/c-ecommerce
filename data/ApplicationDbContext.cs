using Microsoft.EntityFrameworkCore;
using MyMvcProject.Data.Configurations;
using MyMvcProject.Models;

namespace MyMvcProject.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Branch> Branches { get; set; } = null!;

        // Add other DbSet properties for additional models here
        // public DbSet<Order> Orders { get; set; } = null!;
        // etc.

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply entity configurations individually
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new ProductConfiguration());
            modelBuilder.ApplyConfiguration(new BranchConfiguration());


            // Alternative approach: automatically apply all configurations in the assembly
            // modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}