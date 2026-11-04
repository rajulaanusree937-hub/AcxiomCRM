using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IActivityService
{
    Task<ActivityListViewModel> GetListAsync(ActivityListViewModel filter);
    Task<Activity?> FindAsync(int id);
    Task<Activity?> FindWithRelatedAsync(int id);
    ActivityFormViewModel ToFormModel(Activity a);
    Task<ServiceResult> CreateAsync(ActivityFormViewModel model);
    Task<ServiceResult> UpdateAsync(ActivityFormViewModel model);
    Task<ServiceResult> DeleteAsync(int id);
}

public class ActivityService : IActivityService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILookupService _lookup;

    public ActivityService(ApplicationDbContext db, ICurrentUserService currentUser, ILookupService lookup)
    {
        _db = db;
        _currentUser = currentUser;
        _lookup = lookup;
    }

    public async Task<ActivityListViewModel> GetListAsync(ActivityListViewModel filter)

    {
        var query = _db.Activities.AsNoTracking().VisibleTo(_currentUser);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(a => a.Subject.Contains(s) || (a.Description != null && a.Description.Contains(s)));
        }
        if (filter.ActivityType.HasValue) query = query.Where(a => a.ActivityType == filter.ActivityType.Value);
        if (filter.Status.HasValue) query = query.Where(a => a.Status == filter.Status.Value);
        if (filter.DateFrom.HasValue) query = query.Where(a => a.ActivityDate >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue) query = query.Where(a => a.ActivityDate < filter.DateTo.Value.Date.AddDays(1));
        if (_currentUser.CanSeeAllRecords && !string.IsNullOrEmpty(filter.AssignedTo))
            query = query.Where(a => a.AssignedTo == filter.AssignedTo);

        query = query.OrderByDescending(a => a.ActivityDate);

        filter.PageSize = Math.Clamp(filter.PageSize, 5, 50);
        filter.TotalCount = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, filter.TotalPages));

        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(a => new
            {
                a.ActivityId,
                a.ActivityType,
                a.Subject,
                a.ActivityDate,
                a.Status,
                a.AssignedTo,
                a.CustomerId,
                a.LeadId,
                CustomerName = a.Customer != null ? a.Customer.CustomerName : null,
                LeadName = a.Lead != null ? a.Lead.LeadName : null
            })
            .ToListAsync();


        var names = await _lookup.UserNamesAsync(rows.Select(r => r.AssignedTo));

        filter.Items = rows.Select(r => new ActivityListItem
        {
            ActivityId = r.ActivityId,
            ActivityType = r.ActivityType,
            Subject = r.Subject,
            ActivityDate = r.ActivityDate,
            Status = r.Status,
            AssignedToName = r.AssignedTo != null && names.TryGetValue(r.AssignedTo, out var n) ? n : null,
            RelatedType = r.CustomerId != null ? "Customer" : "Lead",
            RelatedName = r.CustomerId != null ? r.CustomerName ?? "" : r.LeadName ?? "",
            RelatedController = r.CustomerId != null ? "Customers" : "Leads",
            RelatedId = r.CustomerId ?? r.LeadId ?? 0
        }).ToList();

        return filter;
    }

    public Task<Activity?> FindAsync(int id) => _db.Activities.FirstOrDefaultAsync(a => a.ActivityId == id);

    public Task<Activity?> FindWithRelatedAsync(int id) =>
        _db.Activities.AsNoTracking().Include(a => a.Customer).Include(a => a.Lead)
                      .FirstOrDefaultAsync(a => a.ActivityId == id);

    public ActivityFormViewModel ToFormModel(Activity a) => new()
    {
        ActivityId = a.ActivityId,
        ActivityType = a.ActivityType,
        Subject = a.Subject,
        Description = a.Description,

        ActivityDate = a.ActivityDate,
        CustomerId = a.CustomerId,
        LeadId = a.LeadId,
        Status = a.Status,
        AssignedTo = a.AssignedTo
    };

    public async Task<ServiceResult> CreateAsync(ActivityFormViewModel model)
    {
        var result = new ServiceResult();
        var owner = await ValidateRelatedAsync(model, result);
        if (!result.Succeeded) return result;

        var activity = new Activity
        {
            ActivityType = model.ActivityType,
            Subject = model.Subject.Trim(),
            Description = model.Description,
            ActivityDate = model.ActivityDate!.Value,
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            Status = model.Status,
            AssignedTo = ResolveAssignedTo(model.AssignedTo, null, owner),
            CreatedDate = DateTime.UtcNow
        };

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok(activity.ActivityId);
    }

    public async Task<ServiceResult> UpdateAsync(ActivityFormViewModel model)

    {
        var activity = await FindAsync(model.ActivityId);
        if (activity == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(activity)) return ServiceResult.ForbiddenResult();

        var result = new ServiceResult();
        if (activity.Status != ActivityStatus.Planned && model.Status != activity.Status)
            result.AddError(nameof(model.Status), $"A {activity.Status} activity cannot change status.");

        var owner = await ValidateRelatedAsync(model, result);
        if (!result.Succeeded) return result;

        activity.ActivityType = model.ActivityType;
        activity.Subject = model.Subject.Trim();
        activity.Description = model.Description;
        activity.ActivityDate = model.ActivityDate!.Value;
        activity.CustomerId = model.CustomerId;
        activity.LeadId = model.LeadId;
        activity.Status = model.Status;
        activity.AssignedTo = ResolveAssignedTo(model.AssignedTo, activity.AssignedTo, owner);

        await _db.SaveChangesAsync();
        return ServiceResult.Ok(activity.ActivityId);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var activity = await FindAsync(id);
        if (activity == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(activity)) return ServiceResult.ForbiddenResult();

        _db.Activities.Remove(activity);

        await _db.SaveChangesAsync();
        return ServiceResult.Ok(id);
    }

    // Related records must exist and be visible to this user (5.2)
    private async Task<string?> ValidateRelatedAsync(ActivityFormViewModel m, ServiceResult result)
    {
        string? owner = null;

        if (m.CustomerId.HasValue)
        {
            var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == m.CustomerId);
            if (c == null || !_currentUser.CanAccess(c)) result.AddError(nameof(m.CustomerId), "Select a valid customer.");
            else owner = c.AssignedTo;
        }
        if (m.LeadId.HasValue)
        {
            var l = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.LeadId == m.LeadId);
            if (l == null || !_currentUser.CanAccess(l)) result.AddError(nameof(m.LeadId), "Select a valid lead.");
            else owner ??= l.AssignedTo;
        }
        return owner;
    }

    private string? ResolveAssignedTo(string? requested, string? currentValue, string? relatedOwner)
    {
        if (_currentUser.IsSalesExecutive && !_currentUser.CanSeeAllRecords)
            return currentValue ?? _currentUser.UserId;
        return string.IsNullOrEmpty(requested) ? relatedOwner : requested;
    }
}