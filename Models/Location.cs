// using System;
// using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyMvcProject.Models
{
    public class Location
    {
        public int Id { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;

        public string? Description { get; set; }

        [ForeignKey("Branch")]
        public int BranchId { get; set; }
        public bool IsActive { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }

        // Navigation property
        public Branch? Branch { get; set; }
    }
}