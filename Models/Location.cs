using System;
using System.ComponentModel.DataAnnotations;

namespace MyMvcProject.Models
{
    public class Location
    {
        public int Id { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationType { get; set; } = string.Empty; // Store, Production, Warehouse
        public string? Description { get; set; }
        public int BranchId { get; set; }
        public bool IsActive { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
        
        // Navigation property
        public Branch? Branch { get; set; }
    }
}