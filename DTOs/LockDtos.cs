// DTOs/LockDtos.cs (updated)
using System;
using System.Text.Json.Serialization;

namespace geoback.DTOs
{
    public class LockReportDto
    {
        public Guid ReportId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = "RM";
        public string SessionId { get; set; } = string.Empty; // New: unique per browser tab
        public int? LockDurationMinutes { get; set; } = 5; // Default 5 minutes
    }

    public class UnlockReportDto
    {
        public Guid ReportId { get; set; }
        public Guid UserId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Reason { get; set; } = "user_action";
    }

    public class HeartbeatDto
    {
        public Guid ReportId { get; set; }
        public Guid UserId { get; set; }
        public string SessionId { get; set; } = string.Empty;
    }

    public class LockInfoDto
    {
        public bool IsLocked { get; set; }
        public LockUserDto? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? SessionId { get; set; }
        public bool IsCurrentUser { get; set; }
        public bool IsCurrentSession { get; set; }
        public string? Message { get; set; }
    }

    public class LockUserDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class UserActiveLockDto
    {
        public Guid ReportId { get; set; }
        public string ReportNo { get; set; } = string.Empty;
        public DateTime LockedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string SessionId { get; set; } = string.Empty;
    }
}