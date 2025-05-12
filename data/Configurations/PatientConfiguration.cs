using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyMvcProject.Models;

namespace MyMvcProject.Data.Configurations
{
    public class PatientConfiguration : IEntityTypeConfiguration<Patient>
    {
        public void Configure(EntityTypeBuilder<Patient> entity)
        {
            entity.ToTable("Patient");
            entity.Property(e => e.PatientId).HasColumnName("PatientId").ValueGeneratedNever();
            entity.Property(e => e.PatientName).IsRequired().HasColumnName("PatientName");
            entity.Property(e => e.PatientAddress).IsRequired().HasColumnName("PatientAddress");
            entity.Property(e => e.PatientEmail).IsRequired().HasColumnName("PatientEmail");
            entity.Property(e => e.PatientPhone).IsRequired().HasColumnName("Mobile");
            entity.Property(e => e.PatientGender).IsRequired(false).HasColumnName("PatientGender");
            entity.Property(e => e.BirthDate).HasColumnName("BirthDate").HasDefaultValue(true);
            entity.Property(e => e.PatientAge).HasColumnName("DateCreated").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.PatientHealthCard).HasColumnName("PatientHealthCard");
        }
    }
}