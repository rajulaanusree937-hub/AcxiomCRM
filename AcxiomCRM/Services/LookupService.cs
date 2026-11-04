using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

// Shared dropdown data, always filtered by what the current user may see (7.1)
public interface ILookupService
{
    Task<List<SelectListItem>> CustomerOptionsAsync(int? selected = null);
    Task<List<SelectListItem>> LeadOptionsAsync(int? selected = null);
    Task<List<SelectListItem>> OpportunityOptionsAsync(int? selected = null);
    Task<List<SelectListItem>> SalesExecutiveOptionsAsync(string? selected = null);
    Task<Dictionary<string, string>> UserNamesAsync(IEnumerable<string?> userIds);
}

public class LookupService : ILookupService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;

    public LookupService(ApplicationDbContext db, ICurrentUserService currentUser,
                         UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _currentUser = currentUser;
        _userManager = userManager;
    }


    public async Task<List<SelectListItem>> CustomerOptionsAsync(int? selected = null)
    {
        var rows = await _db.Customers.AsNoTracking().VisibleTo(_currentUser)
            .Where(c => c.Status == CustomerStatus.Active || c.CustomerId == selected)
            .OrderBy(c => c.CustomerName)
            .Select(c => new { c.CustomerId, c.CustomerCode, c.CustomerName })
            .ToListAsync();

        return rows.Select(c => new SelectListItem($"{c.CustomerCode} - {c.CustomerName}",
                                                   c.CustomerId.ToString(), c.CustomerId == selected)).ToList();
    }

    public async Task<List<SelectListItem>> LeadOptionsAsync(int? selected = null)
    {
        // Open leads only (plus the currently selected one when editing)
        var rows = await _db.Leads.AsNoTracking().VisibleTo(_currentUser)
            .Where(l => (l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost) || l.LeadId == selected)
            .OrderBy(l => l.LeadName)
            .Select(l => new { l.LeadId, l.LeadCode, l.LeadName })
            .ToListAsync();

        return rows.Select(l => new SelectListItem($"{l.LeadCode} - {l.LeadName}",
                                                   l.LeadId.ToString(), l.LeadId == selected)).ToList();
    }

    public async Task<List<SelectListItem>> OpportunityOptionsAsync(int? selected = null)
    {
        var rows = await _db.Opportunities.AsNoTracking().VisibleTo(_currentUser)
            .Where(o => o.Status == OpportunityStatus.Open || o.OpportunityId == selected)
            .OrderBy(o => o.OpportunityName)
            .Select(o => new { o.OpportunityId, o.OpportunityName })
            .ToListAsync();


        return rows.Select(o => new SelectListItem(o.OpportunityName,
                                                   o.OpportunityId.ToString(), o.OpportunityId == selected)).ToList();
    }

    public async Task<List<SelectListItem>> SalesExecutiveOptionsAsync(string? selected = null)
    {
        var users = await _userManager.GetUsersInRoleAsync(AppRoles.SalesExecutive);
        return users.Where(u => u.IsActive)
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem($"{u.FullName} ({u.Email})", u.Id, u.Id == selected))
                    .ToList();
    }

    public async Task<Dictionary<string, string>> UserNamesAsync(IEnumerable<string?> userIds)
    {
        var ids = userIds.Where(i => !string.IsNullOrEmpty(i)).Select(i => i!).Distinct().ToList();
        if (ids.Count == 0) return new();
        return await _db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }
}