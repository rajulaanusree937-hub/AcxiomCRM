using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IFollowUpService
{
    Task<FollowUpListViewModel> GetListAsync(FollowUpListViewModel filter);
    Task<FollowUp?> FindAsync(int id);
    Task<FollowUp?> FindWithRelatedAsync(int id);
    FollowUpFormViewModel ToFormModel(FollowUp f);
    List<SelectListItem> GetAllowedStatusOptions(FollowUpStatus? current);
    Task<ServiceResult> CreateAsync(FollowUpFormViewModel model);
    Task<ServiceResult> UpdateAsync(FollowUpFormViewModel model);
    Task<ServiceResult> CompleteAsync(int id);
    Task<ServiceResult> DeleteAsync(int id);
}

public class FollowUpService : IFollowUpService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILookupService _lookup;

    public FollowUpService(ApplicationDbContext db, ICurrentUserService currentUser, ILookupService lookup)
    {
        _db = db;
        _currentUser = currentUser;
        _lookup = lookup;
    }

    // ===================== STATUS WORKFLOW (4.6) =====================
    private static readonly Dictionary<FollowUpStatus, FollowUpStatus[]> Transitions = new()
    {
        [FollowUpStatus.Planned] = new[] { FollowUpStatus.Completed, FollowUpStatus.Missed, FollowUpStatus.Cancelled },
        [FollowUpStatus.Missed] = new[] { FollowUpStatus.Planned, FollowUpStatus.Completed, FollowUpStatus.Cancelled },
        [FollowUpStatus.Completed] = Array.Empty<FollowUpStatus>(),
        [FollowUpStatus.Cancelled] = Array.Empty<FollowUpStatus>()
    };

    private static bool IsValidTransition(FollowUpStatus from, FollowUpStatus to) =>
        from == to || Transitions[from].Contains(to);

    public List<SelectListItem> GetAllowedStatusOptions(FollowUpStatus? current)
    {
        // New follow-ups always start as Planned
        var options = current is null
            ? new[] { FollowUpStatus.Planned }
            : new[] { current.Value }.Concat(Transitions[current.Value]).ToArray();

        return options.Select(s => new SelectListItem(s.ToString(), s.ToString(), s == current)).ToList();
    }

    // ===================== LIST: Upcoming / Overdue / filters (4.6, 17.13) =====================
    public async Task<FollowUpListViewModel> GetListAsync(FollowUpListViewModel filter)
    {
        var now = DateTime.Now;
        var visible = _db.FollowUps.AsNoTracking().VisibleTo(_currentUser);

        filter.OverdueCount = await visible.CountAsync(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate < now);

        var query = filter.View switch
        {
            "upcoming" => visible.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate >= now),
            "overdue" => visible.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate < now),
            "today" => visible.Where(f => f.FollowUpDate >= DateTime.Today && f.FollowUpDate < DateTime.Today.AddDays(1)),
            _ => visible
        };

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(f => f.Subject.Contains(s) || (f.Remarks != null && f.Remarks.Contains(s)));
        }
        if (filter.Status.HasValue) query = query.Where(f => f.Status == filter.Status.Value);
        if (filter.CustomerId.HasValue) query = query.Where(f => f.CustomerId == filter.CustomerId);
        if (filter.LeadId.HasValue) query = query.Where(f => f.LeadId == filter.LeadId);
        if (filter.DateFrom.HasValue) query = query.Where(f => f.FollowUpDate >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue) query = query.Where(f => f.FollowUpDate < filter.DateTo.Value.Date.AddDays(1));
        if (_currentUser.CanSeeAllRecords && !string.IsNullOrEmpty(filter.AssignedTo))
            query = query.Where(f => f.AssignedTo == filter.AssignedTo);

        // Upcoming/overdue read best soonest-first; "all" newest-first
        query = filter.View is "upcoming" or "overdue" or "today"
            ? query.OrderBy(f => f.FollowUpDate)
            : query.OrderByDescending(f => f.FollowUpDate);

        filter.PageSize = Math.Clamp(filter.PageSize, 5, 50);
        filter.TotalCount = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, filter.TotalPages));

        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(f => new
            {
                f.FollowUpId,
                f.FollowUpDate,
                f.Subject,
                f.FollowUpType,
                f.Status,
                f.AssignedTo,
                f.CustomerId,
                f.LeadId,
                f.OpportunityId,
                CustomerName = f.Customer != null ? f.Customer.CustomerName : null,
                LeadName = f.Lead != null ? f.Lead.LeadName : null,
                OpportunityName = f.Opportunity != null ? f.Opportunity.OpportunityName : null
            })
            .ToListAsync();

        var names = await _lookup.UserNamesAsync(rows.Select(r => r.AssignedTo));

        filter.Items = rows.Select(r =>
        {
            var item = new FollowUpListItem
            {
                FollowUpId = r.FollowUpId,
                FollowUpDate = r.FollowUpDate,
                Subject = r.Subject,
                FollowUpType = r.FollowUpType,
                Status = r.Status,
                AssignedTo = r.AssignedTo,
                AssignedToName = r.AssignedTo != null && names.TryGetValue(r.AssignedTo, out var n) ? n : null
            };

            // Show the most specific related record
            if (r.OpportunityId != null) { item.RelatedType = "Opportunity"; item.RelatedName = r.OpportunityName ?? ""; item.RelatedController = "Opportunities"; item.RelatedId = r.OpportunityId.Value; }
            else if (r.LeadId != null) { item.RelatedType = "Lead"; item.RelatedName = r.LeadName ?? ""; item.RelatedController = "Leads"; item.RelatedId = r.LeadId.Value; }
            else if (r.CustomerId != null) { item.RelatedType = "Customer"; item.RelatedName = r.CustomerName ?? ""; item.RelatedController = "Customers"; item.RelatedId = r.CustomerId.Value; }
            return item;
        }).ToList();

        return filter;
    }

    // ===================== READ =====================
    public Task<FollowUp?> FindAsync(int id) => _db.FollowUps.FirstOrDefaultAsync(f => f.FollowUpId == id);

    public Task<FollowUp?> FindWithRelatedAsync(int id) =>
        _db.FollowUps.AsNoTracking()
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

    public FollowUpFormViewModel ToFormModel(FollowUp f) => new()
    {
        FollowUpId = f.FollowUpId,
        CustomerId = f.CustomerId,
        LeadId = f.LeadId,
        OpportunityId = f.OpportunityId,
        FollowUpDate = f.FollowUpDate,
        Subject = f.Subject,
        FollowUpType = f.FollowUpType,
        Remarks = f.Remarks,
        Status = f.Status,
        AssignedTo = f.AssignedTo
    };

    // ===================== CREATE =====================
    public async Task<ServiceResult> CreateAsync(FollowUpFormViewModel model)
    {
        var result = new ServiceResult();
        var date = model.FollowUpDate!.Value;

        if (model.Status != FollowUpStatus.Planned)
            result.AddError(nameof(model.Status), "A new follow-up must start as Planned.");

        // 4.6 / 5.4: "Follow-up date cannot be earlier than today."
        if (date.Date < DateTime.Today)
            result.AddError(nameof(model.FollowUpDate), "Follow-up date cannot be earlier than today.");

        var owner = await ValidateRelatedAsync(model, result);
        var assignedTo = ResolveAssignedTo(model.AssignedTo, null, owner);
        if (!result.Succeeded) return result;

        var followUp = new FollowUp
        {
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            OpportunityId = model.OpportunityId,
            FollowUpDate = date,
            Subject = model.Subject.Trim(),
            FollowUpType = model.FollowUpType,
            Remarks = model.Remarks,
            Status = FollowUpStatus.Planned,
            AssignedTo = assignedTo,
            CreatedDate = DateTime.UtcNow
        };

        _db.FollowUps.Add(followUp);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok(followUp.FollowUpId);
    }

    // ===================== UPDATE / RESCHEDULE =====================
    public async Task<ServiceResult> UpdateAsync(FollowUpFormViewModel model)
    {
        var followUp = await FindAsync(model.FollowUpId);
        if (followUp == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(followUp)) return ServiceResult.ForbiddenResult();

        var result = new ServiceResult();
        if (followUp.Status is FollowUpStatus.Completed or FollowUpStatus.Cancelled)
            return result.AddError("", "Completed or cancelled follow-ups cannot be edited.");

        var date = model.FollowUpDate!.Value;

        if (!IsValidTransition(followUp.Status, model.Status))
            result.AddError(nameof(model.Status), $"A follow-up cannot move from {followUp.Status} to {model.Status}.");

        // A Planned follow-up may not be (re)scheduled into the past.
        // Editing other fields of an existing planned one without touching its date is allowed.
        var dateChanged = date != followUp.FollowUpDate;
        var becomingPlanned = followUp.Status != FollowUpStatus.Planned;
        if (model.Status == FollowUpStatus.Planned && date.Date < DateTime.Today && (dateChanged || becomingPlanned))
            result.AddError(nameof(model.FollowUpDate), "Follow-up date cannot be earlier than today.");

        var owner = await ValidateRelatedAsync(model, result);
        var assignedTo = ResolveAssignedTo(model.AssignedTo, followUp.AssignedTo, owner);
        if (!result.Succeeded) return result;

        followUp.CustomerId = model.CustomerId;
        followUp.LeadId = model.LeadId;
        followUp.OpportunityId = model.OpportunityId;
        followUp.FollowUpDate = date;
        followUp.Subject = model.Subject.Trim();
        followUp.FollowUpType = model.FollowUpType;
        followUp.Remarks = model.Remarks;
        followUp.Status = model.Status;
        followUp.AssignedTo = assignedTo;

        await _db.SaveChangesAsync();
        return ServiceResult.Ok(followUp.FollowUpId);
    }

    // ===================== QUICK ACTION: Mark Completed (17.2) =====================
    public async Task<ServiceResult> CompleteAsync(int id)
    {
        var followUp = await FindAsync(id);
        if (followUp == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(followUp)) return ServiceResult.ForbiddenResult();

        if (!IsValidTransition(followUp.Status, FollowUpStatus.Completed))
            return new ServiceResult().AddError("", $"A {followUp.Status} follow-up cannot be completed.");

        followUp.Status = FollowUpStatus.Completed;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok(id);
    }

    // ===================== DELETE =====================
    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var followUp = await FindAsync(id);
        if (followUp == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(followUp)) return ServiceResult.ForbiddenResult();

        _db.FollowUps.Remove(followUp);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok(id);
    }

    // ===================== HELPERS =====================

    // Related records must exist AND be visible to this user (foreign-key validation, 5.2).
    // Returns the owner of the related record (used as default assignee).
    private async Task<string?> ValidateRelatedAsync(FollowUpFormViewModel m, ServiceResult result)
    {
        string? owner = null;

        if (m.OpportunityId.HasValue)
        {
            var opp = await _db.Opportunities.AsNoTracking().FirstOrDefaultAsync(o => o.OpportunityId == m.OpportunityId);
            if (opp == null || !_currentUser.CanAccess(opp))
                result.AddError(nameof(m.OpportunityId), "Select a valid opportunity.");
            else
            {
                owner = opp.AssignedTo;
                m.CustomerId ??= opp.CustomerId;   // link the opportunity's customer automatically
            }
        }

        if (m.LeadId.HasValue)
        {
            var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == m.LeadId);
            if (lead == null || !_currentUser.CanAccess(lead))
                result.AddError(nameof(m.LeadId), "Select a valid lead.");
            else owner ??= lead.AssignedTo;
        }

        if (m.CustomerId.HasValue)
        {
            var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == m.CustomerId);
            if (customer == null || !_currentUser.CanAccess(customer))
                result.AddError(nameof(m.CustomerId), "Select a valid customer.");
            else owner ??= customer.AssignedTo;
        }

        return owner;
    }

    // SalesExecutive: always themselves. Admin/Manager: chosen user, or the related record's owner.
    private string? ResolveAssignedTo(string? requested, string? currentValue, string? relatedOwner)
    {
        if (_currentUser.IsSalesExecutive && !_currentUser.CanSeeAllRecords)
            return currentValue ?? _currentUser.UserId;

        return string.IsNullOrEmpty(requested) ? relatedOwner : requested;
    }
}