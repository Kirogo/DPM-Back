// Models/Checklist.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace geoback.Models
{
    [Table("Checklists")]
    public class Checklist
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string DclNo { get; set; } = string.Empty;

        public string? CustomerId { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        
        // Use LoanType to match database
        public string LoanType { get; set; } = string.Empty;
        
        public string? IbpsNo { get; set; }

        public string Status { get; set; } = "pending";
        
        public Guid? AssignedToRM { get; set; }
        
        [Column(TypeName = "longtext")]
        public string DocumentsJson { get; set; } = "[]";

        [Column(TypeName = "longtext")]
        public string? SiteVisitFormJson { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Lock fields
        public bool IsLocked { get; set; }
        public Guid? LockedByUserId { get; set; }
        public string? LockedByUserName { get; set; }
        public string? LockedByUserRole { get; set; }
        public DateTime? LockedAt { get; set; }
        
        // NEW: Enhanced lock fields
        public string? LockSessionId { get; set; }
        public DateTime? LockHeartbeat { get; set; }
        public DateTime? LockExpiresAt { get; set; }

        // QS fields
        public string? AssignedToQS { get; set; }
        public string? AssignedToQSName { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? Priority { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; }

        // Navigation property for comments
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}