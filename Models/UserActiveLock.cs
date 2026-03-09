// Models/UserActiveLock.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace geoback.Models
{
    [Table("UserActiveLocks")]
    public class UserActiveLock
    {
        [Key]
        public Guid UserId { get; set; }

        [Required]
        public Guid ReportId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        public DateTime LockedAt { get; set; }

        [Required]
        public DateTime LastHeartbeat { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        // Navigation property
        [ForeignKey("ReportId")]
        public virtual Checklist? Report { get; set; }
    }
}