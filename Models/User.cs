namespace MyMvcProject.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("Users")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [Column("UserName")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Column("Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mobile number is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [Column("Mobile")]
        public string Mobile { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Column("Email")]
        public string Email { get; set; } = string.Empty;
    
        [Column("Roles")]
        public string? Roles { get; set; }
        
        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }
        
        [Column("DateUpdated")]
        public DateTime? DateUpdated { get; set; }
    }
}