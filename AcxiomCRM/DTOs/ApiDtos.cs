using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models.Enums;

namespace AcxiomCRM.DTOs;

// ===================== Common =====================
public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// ===================== Auth =====================
public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class AuthUserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Role { get; set; }

}

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAtUtc { get; set; }
    public AuthUserDto User { get; set; } = new();
}

// ===================== Customer =====================
public class CustomerDto
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public CustomerStatus Status { get; set; }
    public string? AssignedToId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class CustomerSaveDto
{
    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100, ErrorMessage = "Customer Name cannot exceed 100 characters.")]

    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid phone number (10-digit Indian mobile).")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)] public string? CompanyName { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? State { get; set; }

    [EnumDataType(typeof(CustomerStatus), ErrorMessage = "Select a valid status.")]
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    [StringLength(1000)] public string? Notes { get; set; }

    // Admin/Manager only; ignored for SalesExecutive (auto-assigned)
    public string? AssignedToId { get; set; }
}

// ===================== Lead =====================
public class LeadDto
{
    public int Id { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string? Email { get; set; }

    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public LeadSource Source { get; set; }
    public LeadStatus Status { get; set; }
    public Priority Priority { get; set; }
    public decimal ExpectedValue { get; set; }
    public string? AssignedToId { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class LeadCreateDto
{
    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
    public string LeadName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string? Email { get; set; }

    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid phone number (10-digit Indian mobile).")]
    public string? Phone { get; set; }

    [StringLength(150)] public string? CompanyName { get; set; }

    [EnumDataType(typeof(LeadSource), ErrorMessage = "Select a valid source.")]
    public LeadSource Source { get; set; } = LeadSource.Website;

    [EnumDataType(typeof(LeadStatus), ErrorMessage = "Select a valid lead status.")]
    public LeadStatus Status { get; set; } = LeadStatus.New;


    [EnumDataType(typeof(Priority), ErrorMessage = "Select a valid priority.")]
    public Priority Priority { get; set; } = Priority.Medium;

    [Range(0, 100000000, ErrorMessage = "Expected Value must be between 0 and 10,00,00,000.")]
    public decimal ExpectedValue { get; set; }

    [StringLength(1000)] public string? Notes { get; set; }
    public string? AssignedToId { get; set; }
}

// ===================== Opportunity =====================
public class OpportunityDto
{
    public int Id { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public OpportunityStage Stage { get; set; }
    public OpportunityStatus Status { get; set; }
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public decimal WeightedAmount { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string? AssignedToName { get; set; }
}

public class OpportunityCreateDto
{
    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;


    [Required(ErrorMessage = "Customer is required.")]
    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0, 100000000, ErrorMessage = "Amount cannot be negative or exceed 10,00,00,000.")]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Probability is required.")]
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int? Probability { get; set; }

    [Required(ErrorMessage = "Expected Close Date is required.")]
    public DateTime? ExpectedCloseDate { get; set; }

    [EnumDataType(typeof(OpportunityStage), ErrorMessage = "Select a valid stage.")]
    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    public LeadSource? Source { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public string? AssignedToId { get; set; }
}

// ===================== Follow-Up =====================
public class FollowUpDto
{
    public int Id { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string Subject { get; set; } = string.Empty;
    public FollowUpType FollowUpType { get; set; }
    public FollowUpStatus Status { get; set; }

    public string RelatedType { get; set; } = string.Empty;
    public int RelatedId { get; set; }
    public string RelatedName { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public string? AssignedToName { get; set; }
}

public class FollowUpCreateDto
{
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }

    [Required(ErrorMessage = "Follow-up date is required.")]
    public DateTime? FollowUpDate { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    [EnumDataType(typeof(FollowUpType), ErrorMessage = "Select a valid type.")]
    public FollowUpType FollowUpType { get; set; } = FollowUpType.Call;

    [StringLength(1000)] public string? Remarks { get; set; }
    public string? AssignedToId { get; set; }
}

// ===================== Reports =====================
public class PipelineStageDto
{
    public OpportunityStage Stage { get; set; }
    public int Count { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}

public class PipelineReportDto
{
    public List<PipelineStageDto> Stages { get; set; } = new();
    public decimal OpenPipelineAmount { get; set; }
    public decimal OpenWeightedAmount { get; set; }
}