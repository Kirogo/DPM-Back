// DTOs/ChecklistDtos.cs
using System.Text.Json.Serialization;

namespace geoback.DTOs
{
    public class ChecklistDocumentItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "pendingrm";
        public string? Action { get; set; }
        public string? Comment { get; set; }
    }

    public class ChecklistDocumentCategoryDto
    {
        public string Category { get; set; } = string.Empty;
        public List<ChecklistDocumentItemDto> DocList { get; set; } = new();
    }

    public class CreateChecklistDto
    {
        public string? CustomerId { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        
        // FIX: Use LoanType instead of ProjectName
        public string LoanType { get; set; } = string.Empty;

        [JsonPropertyName("projectName")] // For backward compatibility with frontend
        public string ProjectName 
        { 
            get => LoanType; 
            set => LoanType = value; 
        }

        public string? IbpsNo { get; set; }
        public Guid? AssignedToRM { get; set; }
        public List<ChecklistDocumentCategoryDto> Documents { get; set; } = new();
        
        public object? SiteVisitForm { get; set; }
        
        public string? Priority { get; set; }
    }

    public class UpdateChecklistDto
    {
        public string? CustomerId { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        
        // FIX: Use LoanType instead of ProjectName
        public string LoanType { get; set; } = string.Empty;

        [JsonPropertyName("projectName")]
        public string ProjectName 
        { 
            get => LoanType; 
            set => LoanType = value; 
        }

        public string? IbpsNo { get; set; }
        public Guid? AssignedToRM { get; set; }
        public string? Status { get; set; }
        public List<ChecklistDocumentCategoryDto> Documents { get; set; } = new();
        
        public object? SiteVisitForm { get; set; }
        
        public string? Priority { get; set; }
    }

    public class ChecklistUserRefDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Role { get; set; }
    }

    public class ChecklistResponseDto
    {
        public Guid Id { get; set; }

        [JsonPropertyName("_id")]
        public Guid MongoLikeId => Id;

        public string DclNo { get; set; } = string.Empty;
        
        [JsonPropertyName("callReportNo")]
        public string CallReportNo => DclNo;

        public string? CustomerId { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        
        // FIX: Use LoanType to match database
        public string LoanType { get; set; } = string.Empty;
        
        [JsonPropertyName("projectName")]
        public string ProjectName 
        { 
            get => LoanType; 
            set => LoanType = value; 
        }

        public string? IbpsNo { get; set; }
        public string Status { get; set; } = "pending";
        public ChecklistUserRefDto? AssignedToRM { get; set; }
        public List<ChecklistDocumentCategoryDto> Documents { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        public object? SiteVisitForm { get; set; }
        
        public bool IsLocked { get; set; }
        public ChecklistUserRefDto? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
        
        public string? AssignedToQS { get; set; }
        public string? AssignedToQSName { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? Priority { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; }
         public string? LockSessionId { get; set; }
    public DateTime? LockHeartbeat { get; set; }
    public DateTime? LockExpiresAt { get; set; }

        // Navigation property for comments
        public List<CommentDto> Comments { get; set; } = new();
    }
}