// Models/ReportLock.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace geoback.Models
{
    [Table("ReportLocks")]
    public class ReportLock
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ReportId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string UserEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string UserRole { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        public DateTime LockedAt { get; set; }

        [Required]
        public DateTime LastHeartbeat { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        [Required]
        public bool IsActive { get; set; }

        [MaxLength(50)]
        public string Source { get; set; } = "web";

        // Navigation property
        [ForeignKey("ReportId")]
        public virtual Checklist? Report { get; set; }
    }
}