using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface ILeadService
{
    Task<LeadListViewModel> GetListAsync(LeadListViewModel filter);
    Task<Lead?> FindAsync(int id);
    Task<Lead?> FindWithHistoryAsync(int id);
    LeadFormViewModel ToFormModel(Lead lead);
    List<SelectListItem> GetAllowedStatusOptions(LeadStatus? current);
    Task<ServiceResult> CreateAsync(LeadFormViewModel model);
    Task<ServiceResult> UpdateAsync(LeadFormViewModel model);
    Task<ServiceResult> DeleteAsync(int id);
    LeadConvertViewModel ToConvertModel(Lead lead);
    Task<ServiceResult> ConvertAsync(LeadConvertViewModel model);
}

public class LeadService : ILeadService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;

    public LeadService(ApplicationDbContext db, ICurrentUserService currentUser,
                       UserManager<ApplicationUser> userManager)
    {

        _db = db;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    // ===================== STATUS WORKFLOW (4.5, 17.7) =====================
    // Converted is reached ONLY through ConvertAsync. Converted and Lost are final.
    private static readonly Dictionary<LeadStatus, LeadStatus[]> Transitions = new()
    {
        [LeadStatus.New] = new[] { LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Contacted] = new[] { LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Qualified] = new[] { LeadStatus.Contacted, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Unqualified] = new[] { LeadStatus.Contacted, LeadStatus.Lost },
        [LeadStatus.Converted] = Array.Empty<LeadStatus>(),
        [LeadStatus.Lost] = Array.Empty<LeadStatus>()
    };

    private static readonly LeadStatus[] AllowedOnCreate =
        { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified };

    private static bool IsValidTransition(LeadStatus from, LeadStatus to) =>
        from == to || Transitions[from].Contains(to);

    // current == null -> Create form
    public List<SelectListItem> GetAllowedStatusOptions(LeadStatus? current)
    {
        var options = current is null
            ? AllowedOnCreate
            : new[] { current.Value }.Concat(Transitions[current.Value]).ToArray();

        return options.Select(s => new SelectListItem(s.ToString(), s.ToString(), s == current)).ToList();
    }


    // ===================== LIST + SEARCH (17.13) =====================
    public async Task<LeadListViewModel> GetListAsync(LeadListViewModel filter)
    {
        var query = _db.Leads.AsNoTracking().VisibleTo(_currentUser);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(l =>
                l.LeadName.Contains(s) ||
                l.LeadCode.Contains(s) ||
                (l.CompanyName != null && l.CompanyName.Contains(s)) ||
                (l.Email != null && l.Email.Contains(s)) ||
                (l.Phone != null && l.Phone.Contains(s)));
        }

        if (filter.Status.HasValue)
            query = query.Where(l => l.Status == filter.Status.Value);

        // "Assigned User" filter only makes sense for Admin/Manager
        if (_currentUser.CanSeeAllRecords && !string.IsNullOrEmpty(filter.AssignedTo))
            query = query.Where(l => l.AssignedTo == filter.AssignedTo);

        query = filter.SortBy switch
        {
            "oldest" => query.OrderBy(l => l.CreatedDate),
            "name" => query.OrderBy(l => l.LeadName),
            "value_desc" => query.OrderByDescending(l => l.ExpectedValue),
            _ => query.OrderByDescending(l => l.CreatedDate)
        };


        filter.PageSize = Math.Clamp(filter.PageSize, 5, 50);
        filter.TotalCount = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, filter.TotalPages));

        filter.Items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(l => new LeadListItem
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                CompanyName = l.CompanyName,
                Email = l.Email,
                Phone = l.Phone,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                CreatedDate = l.CreatedDate,
                AssignedToName = l.AssignedTo
            })
            .ToListAsync();

        var ids = filter.Items.Where(i => i.AssignedToName != null).Select(i => i.AssignedToName!).Distinct().ToList();
        var names = await _db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        foreach (var item in filter.Items)
            item.AssignedToName = item.AssignedToName != null && names.TryGetValue(item.AssignedToName, out var n) ? n : null;

        return filter;
    }


    // ===================== READ =====================
    public Task<Lead?> FindAsync(int id) => _db.Leads.FirstOrDefaultAsync(l => l.LeadId == id);

    public Task<Lead?> FindWithHistoryAsync(int id) =>
        _db.Leads.AsNoTracking()
            .Include(l => l.Opportunities)
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.LeadId == id);

    public LeadFormViewModel ToFormModel(Lead l) => new()
    {
        LeadId = l.LeadId,
        LeadCode = l.LeadCode,
        LeadName = l.LeadName,
        Email = l.Email,
        Phone = l.Phone,
        CompanyName = l.CompanyName,
        Source = l.Source,
        Status = l.Status,
        Priority = l.Priority,
        ExpectedValue = l.ExpectedValue,
        Notes = l.Notes,
        AssignedTo = l.AssignedTo
    };

    // ===================== CREATE =====================
    public async Task<ServiceResult> CreateAsync(LeadFormViewModel model)
    {
        Normalize(model);
        var result = new ServiceResult();


        // Only valid starting statuses can be transmitted (17.7 "Lead Status")
        if (!AllowedOnCreate.Contains(model.Status))
            result.AddError(nameof(model.Status), "A new lead can only be New, Contacted or Qualified.");

        var assignedTo = await ResolveAssignedToAsync(model.AssignedTo, null, result);
        if (!result.Succeeded) return result;

        var lead = new Lead
        {
            LeadCode = await NextCodeAsync("LEAD", () => _db.Leads.MaxAsync(l => (int?)l.LeadId),
                                                   code => _db.Leads.AnyAsync(l => l.LeadCode == code)),
            LeadName = model.LeadName,
            Email = model.Email,
            Phone = model.Phone,
            CompanyName = model.CompanyName,
            Source = model.Source,
            Status = model.Status,
            Priority = model.Priority,
            ExpectedValue = model.ExpectedValue,
            Notes = model.Notes,
            AssignedTo = assignedTo,
            CreatedDate = DateTime.UtcNow
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok(lead.LeadId);
    }

    // ===================== UPDATE =====================
    public async Task<ServiceResult> UpdateAsync(LeadFormViewModel model)
    {

        var lead = await FindAsync(model.LeadId);
        if (lead == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(lead)) return ServiceResult.ForbiddenResult();

        var result = new ServiceResult();

        if (lead.Status == LeadStatus.Converted)
            return result.AddError("", "Converted leads cannot be edited.");

        Normalize(model);

        if (model.Status == LeadStatus.Converted)
            result.AddError(nameof(model.Status), "Use the Convert action to convert a qualified lead.");
        else if (!IsValidTransition(lead.Status, model.Status))
            result.AddError(nameof(model.Status), $"A lead cannot move from {lead.Status} to {model.Status}.");

        var assignedTo = await ResolveAssignedToAsync(model.AssignedTo, lead.AssignedTo, result);
        if (!result.Succeeded) return result;

        lead.LeadName = model.LeadName;
        lead.Email = model.Email;
        lead.Phone = model.Phone;
        lead.CompanyName = model.CompanyName;
        lead.Source = model.Source;
        lead.Status = model.Status;
        lead.Priority = model.Priority;
        lead.ExpectedValue = model.ExpectedValue;
        lead.Notes = model.Notes;
        lead.AssignedTo = assignedTo;
        lead.ModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return ServiceResult.Ok(lead.LeadId);
    }

    // ===================== DELETE =====================
    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var lead = await FindAsync(id);
        if (lead == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(lead)) return ServiceResult.ForbiddenResult();

        var hasRelated =
            lead.ConvertedCustomerId != null ||
            await _db.Opportunities.AnyAsync(o => o.LeadId == id) ||
            await _db.FollowUps.AnyAsync(f => f.LeadId == id) ||
            await _db.Activities.AnyAsync(a => a.LeadId == id);

        if (hasRelated)
            return new ServiceResult().AddError("",
                "This lead has been converted or has related records and cannot be deleted. Mark it as Lost instead.");

        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok(id);
    }

    // ===================== CONVERSION (4.5, Section 8) =====================
    public LeadConvertViewModel ToConvertModel(Lead l) => new()
    {
        LeadId = l.LeadId,
        LeadCode = l.LeadCode,
        CustomerName = l.LeadName,
        Email = l.Email ?? string.Empty,

        Phone = l.Phone ?? string.Empty,
        CompanyName = l.CompanyName,
        CreateOpportunity = true,
        OpportunityName = $"{l.CompanyName ?? l.LeadName} - Deal",
        Amount = l.ExpectedValue > 0 ? l.ExpectedValue : null,
        Probability = 20,
        ExpectedCloseDate = DateTime.Today.AddDays(30)
    };

    public async Task<ServiceResult> ConvertAsync(LeadConvertViewModel model)
    {
        var lead = await FindAsync(model.LeadId);
        if (lead == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(lead)) return ServiceResult.ForbiddenResult();

        var result = new ServiceResult();

        // Workflow: only Qualified leads can be converted
        if (lead.Status != LeadStatus.Qualified)
            return result.AddError("", "Only Qualified leads can be converted.");

        model.CustomerName = model.CustomerName.Trim();
        model.Email = model.Email.Trim().ToLowerInvariant();
        model.Phone = model.Phone.Trim();
        model.CompanyName = string.IsNullOrWhiteSpace(model.CompanyName) ? null : model.CompanyName.Trim();

        // Same uniqueness rules as Customer (17.5)
        if (await _db.Customers.AnyAsync(c => c.Email == model.Email))
            result.AddError(nameof(model.Email), "A customer with this email already exists.");
        if (await _db.Customers.AnyAsync(c => c.Phone == model.Phone))
            result.AddError(nameof(model.Phone), "A customer with this phone number already exists.");
        if (!result.Succeeded) return result;


        // All-or-nothing: customer, opportunity and lead update succeed together
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var owner = lead.AssignedTo ?? _currentUser.UserId;

            var customer = new Customer
            {
                CustomerCode = await NextCodeAsync("CUS", () => _db.Customers.MaxAsync(c => (int?)c.CustomerId),
                                                          code => _db.Customers.AnyAsync(c => c.CustomerCode == code)),
                CustomerName = model.CustomerName,
                Email = model.Email,
                Phone = model.Phone,
                CompanyName = model.CompanyName,
                Status = CustomerStatus.Active,
                Notes = $"Converted from lead {lead.LeadCode}.",
                AssignedTo = owner,
                CreatedBy = _currentUser.UserId ?? string.Empty,
                CreatedDate = DateTime.UtcNow
            };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();

            if (model.CreateOpportunity)
            {
                _db.Opportunities.Add(new Opportunity
                {
                    OpportunityName = model.OpportunityName!.Trim(),
                    CustomerId = customer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = model.Amount!.Value,

                    Probability = model.Probability!.Value,
                    ExpectedCloseDate = model.ExpectedCloseDate!.Value.Date,
                    Stage = OpportunityStage.Qualification,
                    Status = OpportunityStatus.Open,
                    Source = lead.Source,
                    AssignedTo = owner,
                    CreatedDate = DateTime.UtcNow
                });
            }

            lead.Status = LeadStatus.Converted;
            lead.ConvertedCustomerId = customer.CustomerId;
            lead.ModifiedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ServiceResult.Ok(customer.CustomerId);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            return result.AddError("", "Conversion failed because a customer with the same details already exists.");
        }
    }

    // ===================== HELPERS =====================
    private static void Normalize(LeadFormViewModel m)
    {
        m.LeadName = m.LeadName.Trim();
        m.Email = string.IsNullOrWhiteSpace(m.Email) ? null : m.Email.Trim().ToLowerInvariant();
        m.Phone = string.IsNullOrWhiteSpace(m.Phone) ? null : m.Phone.Trim();

        m.CompanyName = string.IsNullOrWhiteSpace(m.CompanyName) ? null : m.CompanyName.Trim();
    }

    private async Task<string?> ResolveAssignedToAsync(string? requested, string? currentValue, ServiceResult result)
    {
        if (_currentUser.IsSalesExecutive && !_currentUser.CanSeeAllRecords)
            return currentValue ?? _currentUser.UserId;

        if (string.IsNullOrEmpty(requested)) return null;

        var user = await _userManager.FindByIdAsync(requested);
        if (user == null || !user.IsActive || !await _userManager.IsInRoleAsync(user, AppRoles.SalesExecutive))
        {
            result.AddError(nameof(LeadFormViewModel.AssignedTo), "Select a valid sales executive.");
            return null;
        }
        return requested;
    }

    private static async Task<string> NextCodeAsync(string prefix, Func<Task<int?>> maxId, Func<string, Task<bool>> exists)
    {
        var next = (await maxId() ?? 0) + 1;
        string code;
        do { code = $"{prefix}-{next++:D5}"; } while (await exists(code));
        return code;
    }
}