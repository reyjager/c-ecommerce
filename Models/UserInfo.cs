namespace MyMvcProject.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("UserInfos")]
    public class UserInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [Column("FirstName")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [Column("LastName")]
        public string LastName { get; set; } = string.Empty;

        [Column("Address")]
        public string? Address { get; set; }

        [Column("DateOfBirth")]
        public DateTime? DateOfBirth { get; set; }

        [ForeignKey("User")]
        [Column("UserId")]
        public int UserId { get; set; }

        public User User { get; set; } = null!;
    }
}