using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcProject.Models
{
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Product ID")]
        public int ProductId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "SKU Number")]
        public string? SkuNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Model Number")]
        public string? ModelNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Barcode")]
        public string? BarcodeNumber { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        [Display(Name = "Stock Quantity")]
        public int StockQuantity { get; set; }

        [StringLength(200)]
        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Required]
        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Display(Name = "Date Updated")]
        public DateTime? DateUpdated { get; set; }
    }
}