using System.Text.Json;
using geoback.Data;
using geoback.DTOs;
using geoback.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;

namespace geoback.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RmChecklistController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<RmChecklistController> _logger;

    public class UploadChecklistDocumentRequest
    {
        public IFormFile? File { get; set; }
        public string? DocumentType { get; set; }
    }

    public RmChecklistController(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        ILogger<RmChecklistController> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    [HttpPost("documents")]
    [RequestSizeLimit(20_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> UploadChecklistDocument([FromForm] UploadChecklistDocumentRequest request)
    {
        var file = request.File;
        var documentType = request.DocumentType;

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Document file is required." });
        }

        if (file.Length > 20_000_000)
        {
            return BadRequest(new { message = "Maximum allowed document size is 20MB." });
        }

        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Only PDF, DOC, DOCX, XLS, XLSX, JPG, PNG files are allowed." });
        }

        var uploadsRoot = Path.Combine(_environment.ContentRootPath, "uploads", "rm-checklist-documents");
        Directory.CreateDirectory(uploadsRoot);

        var safeDocType = string.IsNullOrWhiteSpace(documentType)
            ? "general"
            : new string(documentType.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray()).ToLowerInvariant();

        var generatedFileName = $"{safeDocType}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, generatedFileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        var publicUrl = $"/api/rmChecklist/documents/{generatedFileName}";

        return Ok(new
        {
            message = "Document uploaded successfully",
            url = publicUrl,
            fileName = generatedFileName,
            documentType = safeDocType
        });
    }

    [HttpGet("documents/{fileName}")]
    public IActionResult GetChecklistDocument(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return BadRequest(new { message = "Invalid file name." });
        }

        var fullPath = Path.Combine(_environment.ContentRootPath, "uploads", "rm-checklist-documents", safeFileName);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { message = "Document not found." });
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(safeFileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(fullPath, contentType);
    }

    public class UploadChecklistPhotoRequest
    {
        public IFormFile? File { get; set; }
        public string? Section { get; set; }
        public int? Slot { get; set; }
    }

    [HttpPost("photos")]
    [RequestSizeLimit(10_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> UploadChecklistPhoto([FromForm] UploadChecklistPhotoRequest request)
    {
        var file = request.File;
        var section = request.Section;
        var slot = request.Slot;

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Photo file is required." });
        }

        if (file.Length > 10_000_000)
        {
            return BadRequest(new { message = "Maximum allowed photo size is 10MB." });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Only JPG, PNG and WEBP files are allowed." });
        }

        var uploadsRoot = Path.Combine(_environment.ContentRootPath, "uploads", "rm-checklist-photos");
        Directory.CreateDirectory(uploadsRoot);

        var safeSection = string.IsNullOrWhiteSpace(section)
            ? "general"
            : new string(section.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray()).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(safeSection))
        {
            safeSection = "general";
        }

        var generatedFileName = $"{safeSection}-slot{slot ?? 0}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, generatedFileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        var publicUrl = $"/api/rmChecklist/photos/{generatedFileName}";

        return Ok(new
        {
            message = "Photo uploaded successfully",
            url = publicUrl,
            fileName = generatedFileName,
            section = safeSection,
            slot,
        });
    }

    [HttpGet("photos/{fileName}")]
    public IActionResult GetChecklistPhoto(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return BadRequest(new { message = "Invalid file name." });
        }

        var fullPath = Path.Combine(_environment.ContentRootPath, "uploads", "rm-checklist-photos", safeFileName);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { message = "Photo not found." });
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(safeFileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(fullPath, contentType);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChecklistResponseDto>>> GetAllRmChecklists()
    {
        try
        {
            var checklists = await _context.Checklists
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var rmIds = checklists
                .Where(c => c.AssignedToRM.HasValue)
                .Select(c => c.AssignedToRM!.Value)
                .Distinct()
                .ToList();

            var rmMap = await _context.Users
                .Where(u => rmIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new ChecklistUserRefDto
                {
                    Id = u.Id,
                    Name = $"{u.FirstName} {u.LastName}".Trim(),
                    Email = u.Email,
                });

            var result = checklists.Select(c => new ChecklistResponseDto
            {
                Id = c.Id,
                DclNo = c.DclNo,
                CustomerId = c.CustomerId,
                CustomerNumber = c.CustomerNumber,
                CustomerName = c.CustomerName,
                CustomerEmail = c.CustomerEmail,
                // Use LoanType to match database column
                LoanType = c.LoanType,
                IbpsNo = c.IbpsNo,
                Status = c.Status,
                AssignedToRM = c.AssignedToRM.HasValue && rmMap.ContainsKey(c.AssignedToRM.Value)
                    ? rmMap[c.AssignedToRM.Value]
                    : null,
                Documents = DeserializeDocuments(c.DocumentsJson),
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                SiteVisitForm = DeserializeSiteVisitForm(c.SiteVisitFormJson),
                IsLocked = c.IsLocked,
                LockedBy = c.LockedByUserId.HasValue ? new ChecklistUserRefDto
                {
                    Id = c.LockedByUserId.Value,
                    Name = c.LockedByUserName ?? "Unknown",
                    Role = c.LockedByUserRole ?? "RM"
                } : null,
                LockedAt = c.LockedAt,
                // New lock fields
                LockSessionId = c.LockSessionId,
                LockHeartbeat = c.LockHeartbeat,
                LockExpiresAt = c.LockExpiresAt,
                // QS fields
                AssignedToQS = c.AssignedToQS,
                AssignedToQSName = c.AssignedToQSName,
                SubmittedAt = c.SubmittedAt,
                Priority = c.Priority,
                ReviewedAt = c.ReviewedAt,
                ReviewedBy = c.ReviewedBy
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checklists");
            return StatusCode(500, new { message = "Error fetching checklists", error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChecklistResponseDto>> GetChecklistById(Guid id)
    {
        try
        {
            var checklist = await _context.Checklists
                .FirstOrDefaultAsync(c => c.Id == id);

            if (checklist == null)
            {
                return NotFound(new { message = "Checklist not found." });
            }

            ChecklistUserRefDto? assignedRM = null;
            if (checklist.AssignedToRM.HasValue)
            {
                var rm = await _context.Users
                    .Where(u => u.Id == checklist.AssignedToRM.Value)
                    .Select(u => new ChecklistUserRefDto
                    {
                        Id = u.Id,
                        Name = $"{u.FirstName} {u.LastName}".Trim(),
                        Email = u.Email,
                    })
                    .FirstOrDefaultAsync();
                assignedRM = rm;
            }

            // Parse site visit form
            object? siteVisitForm = null;
            if (!string.IsNullOrWhiteSpace(checklist.SiteVisitFormJson) && checklist.SiteVisitFormJson != "null")
            {
                try
                {
                    siteVisitForm = JsonSerializer.Deserialize<object>(checklist.SiteVisitFormJson);
                }
                catch
                {
                    // Ignore parsing errors
                }
            }

            var result = new ChecklistResponseDto
            {
                Id = checklist.Id,
                DclNo = checklist.DclNo,
                CustomerId = checklist.CustomerId,
                CustomerNumber = checklist.CustomerNumber,
                CustomerName = checklist.CustomerName,
                CustomerEmail = checklist.CustomerEmail,
                LoanType = checklist.LoanType,
                IbpsNo = checklist.IbpsNo,
                Status = checklist.Status,
                AssignedToRM = assignedRM,
                Documents = DeserializeDocuments(checklist.DocumentsJson),
                CreatedAt = checklist.CreatedAt,
                UpdatedAt = checklist.UpdatedAt,
                SiteVisitForm = siteVisitForm,
                IsLocked = checklist.IsLocked,
                LockedBy = checklist.LockedByUserId.HasValue ? new ChecklistUserRefDto
                {
                    Id = checklist.LockedByUserId.Value,
                    Name = checklist.LockedByUserName ?? "Unknown",
                    Role = checklist.LockedByUserRole ?? "RM"
                } : null,
                LockedAt = checklist.LockedAt,
                // New lock fields
                LockSessionId = checklist.LockSessionId,
                LockHeartbeat = checklist.LockHeartbeat,
                LockExpiresAt = checklist.LockExpiresAt,
                // QS fields
                AssignedToQS = checklist.AssignedToQS,
                AssignedToQSName = checklist.AssignedToQSName,
                SubmittedAt = checklist.SubmittedAt,
                Priority = checklist.Priority,
                ReviewedAt = checklist.ReviewedAt,
                ReviewedBy = checklist.ReviewedBy
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checklist by ID");
            return StatusCode(500, new { message = "Error fetching checklist", error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> CreateChecklist([FromBody] CreateChecklistDto payload)
    {
        try
        {
            var resolvedProjectName = !string.IsNullOrWhiteSpace(payload.LoanType)
                ? payload.LoanType
                : payload.ProjectName ?? string.Empty;

            if (string.IsNullOrWhiteSpace(payload.CustomerNumber) ||
                string.IsNullOrWhiteSpace(payload.CustomerName) ||
                string.IsNullOrWhiteSpace(resolvedProjectName) ||
                payload.AssignedToRM == null ||
                string.IsNullOrWhiteSpace(payload.IbpsNo))
            {
                return BadRequest(new { message = "Please fill all required fields." });
            }

            var existingCrnNumbers = await _context.Checklists
                .AsNoTracking()
                .Where(c => c.DclNo.StartsWith("CRN-"))
                .Select(c => c.DclNo)
                .ToListAsync();

            var nextNumber = existingCrnNumbers
                .Select(ExtractCrnSequence)
                .DefaultIfEmpty(0)
                .Max() + 1;

            var dclNo = $"CRN-{nextNumber:000}";

            var checklist = new Checklist
            {
                DclNo = dclNo,
                CustomerId = payload.CustomerId,
                CustomerNumber = payload.CustomerNumber,
                CustomerName = payload.CustomerName,
                CustomerEmail = payload.CustomerEmail,
                LoanType = resolvedProjectName,
                IbpsNo = payload.IbpsNo,
                AssignedToRM = payload.AssignedToRM,
                Status = "pending",
                DocumentsJson = JsonSerializer.Serialize(payload.Documents, _jsonOptions),
                SiteVisitFormJson = payload.SiteVisitForm != null
                    ? JsonSerializer.Serialize(payload.SiteVisitForm, _jsonOptions)
                    : null,
                IsLocked = false,
                Priority = "Medium",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.Checklists.Add(checklist);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetChecklistById), new { id = checklist.Id }, new
            {
                message = "Checklist created successfully",
                checklist = new
                {
                    id = checklist.Id,
                    _id = checklist.Id,
                    checklist.DclNo,
                    checklist.CustomerName,
                    checklist.CustomerNumber,
                    checklist.CustomerEmail,
                    projectName = checklist.LoanType,
                    loanType = checklist.LoanType,
                    checklist.IbpsNo,
                    checklist.Status,
                    documents = payload.Documents,
                    siteVisitForm = payload.SiteVisitForm,
                    checklist.IsLocked,
                    checklist.Priority,
                    checklist.CreatedAt,
                    checklist.UpdatedAt,
                },
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checklist");
            return StatusCode(500, new { message = "Error creating checklist", error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateChecklist(Guid id, [FromBody] UpdateChecklistDto payload)
    {
        try
        {
            var checklist = await _context.Checklists.FirstOrDefaultAsync(c => c.Id == id);

            if (checklist == null)
            {
                return NotFound(new { message = "Checklist not found." });
            }

            var resolvedProjectName = !string.IsNullOrWhiteSpace(payload.LoanType)
                ? payload.LoanType
                : payload.ProjectName ?? string.Empty;

            // Update only the fields that are provided
            if (!string.IsNullOrWhiteSpace(payload.CustomerNumber))
                checklist.CustomerNumber = payload.CustomerNumber.Trim();

            if (!string.IsNullOrWhiteSpace(payload.CustomerName))
                checklist.CustomerName = payload.CustomerName.Trim();

            if (!string.IsNullOrWhiteSpace(payload.CustomerEmail))
                checklist.CustomerEmail = payload.CustomerEmail;

            if (!string.IsNullOrWhiteSpace(resolvedProjectName))
                checklist.LoanType = resolvedProjectName.Trim();

            if (!string.IsNullOrWhiteSpace(payload.IbpsNo))
                checklist.IbpsNo = payload.IbpsNo.Trim();

            if (payload.AssignedToRM != null)
                checklist.AssignedToRM = payload.AssignedToRM;

            // Update status if provided
            if (!string.IsNullOrWhiteSpace(payload.Status))
            {
                var oldStatus = checklist.Status;
                checklist.Status = NormalizeWorkflowStatus(payload.Status, checklist.Status);

                // If status is changing to Submitted, set SubmittedAt
                if (checklist.Status == "submitted" && oldStatus != "submitted")
                {
                    checklist.SubmittedAt = DateTime.UtcNow;
                }

                // If status is changing to rework, log it
                if (checklist.Status == "rework" && oldStatus != "rework")
                {
                    _logger.LogInformation($"Report {id} returned for rework");
                }
            }

            // Update priority if provided
            if (!string.IsNullOrWhiteSpace(payload.Priority))
            {
                checklist.Priority = payload.Priority;
            }

            // Update documents if provided
            if (payload.Documents != null)
            {
                checklist.DocumentsJson = JsonSerializer.Serialize(payload.Documents, _jsonOptions);
            }

            // Update site visit form if provided
            if (payload.SiteVisitForm != null)
            {
                checklist.SiteVisitFormJson = JsonSerializer.Serialize(payload.SiteVisitForm, _jsonOptions);
                _logger.LogInformation($"Updated SiteVisitFormJson for report {id}");
            }

            checklist.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Checklist updated successfully",
                checklist = new
                {
                    id = checklist.Id,
                    _id = checklist.Id,
                    checklist.DclNo,
                    checklist.CustomerName,
                    checklist.CustomerNumber,
                    checklist.CustomerEmail,
                    projectName = checklist.LoanType,
                    loanType = checklist.LoanType,
                    checklist.IbpsNo,
                    checklist.Status,
                    documents = DeserializeDocuments(checklist.DocumentsJson),
                    siteVisitForm = DeserializeSiteVisitForm(checklist.SiteVisitFormJson),
                    checklist.IsLocked,
                    checklist.LockedByUserId,
                    checklist.LockedByUserName,
                    checklist.LockedByUserRole,
                    checklist.LockSessionId,
                    checklist.LockHeartbeat,
                    checklist.LockExpiresAt,
                    checklist.LockedAt,
                    checklist.Priority,
                    checklist.SubmittedAt,
                    checklist.CreatedAt,
                    checklist.UpdatedAt,
                },
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating checklist");
            return StatusCode(500, new { message = "Error updating checklist", error = ex.Message });
        }
    }

    // ADD THIS DELETE ENDPOINT
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteChecklist(Guid id)
    {
        try
        {
            var checklist = await _context.Checklists.FirstOrDefaultAsync(c => c.Id == id);

            if (checklist == null)
            {
                return NotFound(new { message = "Checklist not found." });
            }

            // Optional: Add authorization check - only allow deletion if status is pending or draft
            if (checklist.Status != "pending" && checklist.Status != "draft")
            {
                return BadRequest(new { message = "Only reports with status 'pending' or 'draft' can be deleted." });
            }

            _context.Checklists.Remove(checklist);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Checklist {id} deleted successfully");

            return Ok(new
            {
                message = "Checklist deleted successfully",
                id = id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting checklist");
            return StatusCode(500, new { message = "Error deleting checklist", error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult> SubmitReport(Guid id)
    {
        try
        {
            var checklist = await _context.Checklists.FirstOrDefaultAsync(c => c.Id == id);

            if (checklist == null)
            {
                return NotFound(new { message = "Checklist not found." });
            }

            checklist.Status = "submitted";
            checklist.SubmittedAt = DateTime.UtcNow;
            checklist.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Report submitted successfully",
                submittedAt = checklist.SubmittedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting report");
            return StatusCode(500, new { message = "Error submitting report", error = ex.Message });
        }
    }

    // Note: Lock endpoints have been moved to LockController
    // Remove any existing lock endpoints from this controller

    private static List<ChecklistDocumentCategoryDto> DeserializeDocuments(string documentsJson)
    {
        if (string.IsNullOrWhiteSpace(documentsJson))
        {
            return new List<ChecklistDocumentCategoryDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ChecklistDocumentCategoryDto>>(documentsJson) ?? new List<ChecklistDocumentCategoryDto>();
        }
        catch
        {
            return new List<ChecklistDocumentCategoryDto>();
        }
    }

    private static object? DeserializeSiteVisitForm(string? siteVisitFormJson)
    {
        if (string.IsNullOrWhiteSpace(siteVisitFormJson) || siteVisitFormJson == "null")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<object>(siteVisitFormJson);
        }
        catch
        {
            return null;
        }
    }

    private static int ExtractCrnSequence(string? callReportNumber)
    {
        if (string.IsNullOrWhiteSpace(callReportNumber))
        {
            return 0;
        }

        if (!callReportNumber.StartsWith("CRN-", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var numberPart = callReportNumber[4..];
        return int.TryParse(numberPart, out var parsedNumber) && parsedNumber > 0
            ? parsedNumber
            : 0;
    }

    private static string NormalizeWorkflowStatus(string? requestedStatus, string currentStatus)
    {
        var status = (requestedStatus ?? string.Empty).Trim().ToLowerInvariant();

        return status switch
        {
            "draft" => "draft",
            "pending" => "pending",
            "submitted" => "submitted",
            "rework" => "rework",
            "approved" => "approved",
            "rejected" => "rejected",
            "revision_requested" => "rework",
            "returned" => "rework",
            _ => string.IsNullOrWhiteSpace(currentStatus) ? "pending" : currentStatus,
        };
    }
}