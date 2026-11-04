using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IOpportunityService
{
    Task<OpportunityListViewModel> GetListAsync(OpportunityListViewModel filter);
    Task<PipelineViewModel> GetPipelineAsync();
    Task<Opportunity?> FindAsync(int id);
    Task<Opportunity?> FindWithRelatedAsync(int id);
    OpportunityFormViewModel ToFormModel(Opportunity o);
    List<SelectListItem> GetAllowedStageOptions(OpportunityStage? current);
    Task<ServiceResult> CreateAsync(OpportunityFormViewModel model);
    Task<ServiceResult> UpdateAsync(OpportunityFormViewModel model);
    Task<ServiceResult> CloseAsync(int id, bool won);
    Task<ServiceResult> DeleteAsync(int id);
}

public class OpportunityService : IOpportunityService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILookupService _lookup;
    private readonly UserManager<ApplicationUser> _userManager;

    public OpportunityService(ApplicationDbContext db, ICurrentUserService currentUser,
                              ILookupService lookup, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _currentUser = currentUser;
        _lookup = lookup;
        _userManager = userManager;
    }

    // ===================== PIPELINE STAGES (4.8) =====================
    private static readonly OpportunityStage[] OpenStages =
        { OpportunityStage.Qualification, OpportunityStage.Proposal, OpportunityStage.Negotiation };

    private static bool IsClosed(OpportunityStage s) => s is OpportunityStage.Won or OpportunityStage.Lost;

    // Open stages can move freely between each other or close as Won/Lost. Won/Lost are final.
    private static bool IsValidTransition(OpportunityStage from, OpportunityStage to) =>
        from == to || !IsClosed(from);

    public List<SelectListItem> GetAllowedStageOptions(OpportunityStage? current)
    {
        IEnumerable<OpportunityStage> options = current switch
        {
            null => OpenStages,                                       // new: open stages only
            var s when IsClosed(s.Value) => new[] { s.Value },        // closed: locked
            _ => Enum.GetValues<OpportunityStage>()                   // open: anything
        };
        return options.Select(s => new SelectListItem(s.ToString(), s.ToString(), s == current)).ToList();
    }

    // ===================== LIST (17.13: Name, Customer, Stage, Status) =====================
    public async Task<OpportunityListViewModel> GetListAsync(OpportunityListViewModel filter)
    {
        var query = _db.Opportunities.AsNoTracking().VisibleTo(_currentUser);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(o => o.OpportunityName.Contains(s) || o.Customer!.CustomerName.Contains(s));
        }
        if (filter.CustomerId.HasValue) query = query.Where(o => o.CustomerId == filter.CustomerId);
        if (filter.Stage.HasValue) query = query.Where(o => o.Stage == filter.Stage.Value);
        if (filter.Status.HasValue) query = query.Where(o => o.Status == filter.Status.Value);
        if (_currentUser.CanSeeAllRecords && !string.IsNullOrEmpty(filter.AssignedTo))
            query = query.Where(o => o.AssignedTo == filter.AssignedTo);

        filter.TotalCount = await query.CountAsync();
        filter.TotalAmount = await query.SumAsync(o => (decimal?)o.Amount) ?? 0;
        filter.TotalWeighted = await query.SumAsync(o => (decimal?)(o.Amount * o.Probability / 100m)) ?? 0;

        query = filter.SortBy switch
        {
            "amount_desc" => query.OrderByDescending(o => o.Amount),
            "newest" => query.OrderByDescending(o => o.CreatedDate),
            "name" => query.OrderBy(o => o.OpportunityName),
            _ => query.OrderBy(o => o.ExpectedCloseDate)     // close_asc
        };

        filter.PageSize = Math.Clamp(filter.PageSize, 5, 50);
        filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, filter.TotalPages));

        filter.Items = await ProjectAsync(query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize));
        return filter;
    }

    // ===================== SALES PIPELINE (17.2) =====================
    public async Task<PipelineViewModel> GetPipelineAsync()
    {
        var all = await ProjectAsync(_db.Opportunities.AsNoTracking().VisibleTo(_currentUser)
                                                      .OrderBy(o => o.ExpectedCloseDate));

        var model = new PipelineViewModel
        {
            Columns = Enum.GetValues<OpportunityStage>()
                          .Select(stage => new PipelineColumn { Stage = stage, Items = all.Where(i => i.Stage == stage).ToList() })
                          .ToList()
        };

        var open = all.Where(i => !IsClosed(i.Stage)).ToList();
        model.OpenPipelineAmount = open.Sum(i => i.Amount);
        model.OpenWeightedAmount = open.Sum(i => i.WeightedAmount);
        return model;
    }

    private async Task<List<OpportunityListItem>> ProjectAsync(IQueryable<Opportunity> query)
    {
        var items = await query.Select(o => new OpportunityListItem
        {
            OpportunityId = o.OpportunityId,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer != null ? o.Customer.CustomerName : "",
            Stage = o.Stage,
            Status = o.Status,
            Amount = o.Amount,
            Probability = o.Probability,
            ExpectedCloseDate = o.ExpectedCloseDate,
            AssignedToName = o.AssignedTo
        }).ToListAsync();

        var names = await _lookup.UserNamesAsync(items.Select(i => i.AssignedToName));
        foreach (var i in items)
            i.AssignedToName = i.AssignedToName != null && names.TryGetValue(i.AssignedToName, out var n) ? n : null;
        return items;
    }

    // ===================== READ =====================
    public Task<Opportunity?> FindAsync(int id) => _db.Opportunities.FirstOrDefaultAsync(o => o.OpportunityId == id);

    public Task<Opportunity?> FindWithRelatedAsync(int id) =>
        _db.Opportunities.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.FollowUps)
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

    public OpportunityFormViewModel ToFormModel(Opportunity o) => new()
    {
        OpportunityId = o.OpportunityId,
        OpportunityName = o.OpportunityName,
        CustomerId = o.CustomerId,
        LeadId = o.LeadId,
        Amount = o.Amount,
        Probability = o.Probability,
        ExpectedCloseDate = o.ExpectedCloseDate,
        Stage = o.Stage,
        Source = o.Source,
        Notes = o.Notes,
        AssignedTo = o.AssignedTo
    };

    // ===================== CREATE =====================
    public async Task<ServiceResult> CreateAsync(OpportunityFormViewModel model)
    {
        var result = new ServiceResult();

        if (IsClosed(model.Stage))
            result.AddError(nameof(model.Stage), "A new opportunity must start in an open stage.");

        ValidateBusinessRules(model, result);
        var owner = await ValidateRelatedAsync(model, result);
        var assignedTo = await ResolveAssignedToAsync(model.AssignedTo, null, owner, result);
        if (!result.Succeeded) return result;

        var opp = new Opportunity
        {
            OpportunityName = model.OpportunityName.Trim(),
            CustomerId = model.CustomerId!.Value,
            LeadId = model.LeadId,
            Amount = model.Amount!.Value,
            Probability = model.Probability!.Value,
            ExpectedCloseDate = model.ExpectedCloseDate!.Value.Date,
            Stage = model.Stage,
            Status = OpportunityStatus.Open,
            Source = model.Source,
            Notes = model.Notes,
            AssignedTo = assignedTo,
            CreatedDate = DateTime.UtcNow
        };

        _db.Opportunities.Add(opp);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok(opp.OpportunityId);
    }

    // ===================== UPDATE / MOVE STAGE =====================
    public async Task<ServiceResult> UpdateAsync(OpportunityFormViewModel model)
    {
        var opp = await FindAsync(model.OpportunityId);
        if (opp == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(opp)) return ServiceResult.ForbiddenResult();

        var result = new ServiceResult();
        if (IsClosed(opp.Stage))
            return result.AddError("", $"This opportunity is already {opp.Stage} and cannot be edited.");

        if (!IsValidTransition(opp.Stage, model.Stage))
            result.AddError(nameof(model.Stage), $"Cannot move from {opp.Stage} to {model.Stage}.");

        ApplyClosingRules(model);
        ValidateBusinessRules(model, result);
        var owner = await ValidateRelatedAsync(model, result);
        var assignedTo = await ResolveAssignedToAsync(model.AssignedTo, opp.AssignedTo, owner, result);
        if (!result.Succeeded) return result;

        opp.OpportunityName = model.OpportunityName.Trim();
        opp.CustomerId = model.CustomerId!.Value;
        opp.LeadId = model.LeadId;
        opp.Amount = model.Amount!.Value;
        opp.Probability = model.Probability!.Value;
        opp.ExpectedCloseDate = model.ExpectedCloseDate!.Value.Date;
        opp.Stage = model.Stage;
        opp.Status = IsClosed(model.Stage) ? OpportunityStatus.Closed : OpportunityStatus.Open;
        opp.Source = model.Source;
        opp.Notes = model.Notes;
        opp.AssignedTo = assignedTo;
        opp.ModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ServiceResult.Ok(opp.OpportunityId);
    }

    // ===================== QUICK ACTION: Mark Won / Lost (Section 8 workflow) =====================
    public async Task<ServiceResult> CloseAsync(int id, bool won)
    {
        var opp = await FindAsync(id);
        if (opp == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(opp)) return ServiceResult.ForbiddenResult();

        if (IsClosed(opp.Stage))
            return new ServiceResult().AddError("", $"This opportunity is already {opp.Stage}.");

        opp.Stage = won ? OpportunityStage.Won : OpportunityStage.Lost;
        opp.Probability = won ? 100 : 0;
        opp.Status = OpportunityStatus.Closed;
        opp.ModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ServiceResult.Ok(id);
    }

    // ===================== DELETE =====================
    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var opp = await FindAsync(id);
        if (opp == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(opp)) return ServiceResult.ForbiddenResult();

        if (await _db.FollowUps.AnyAsync(f => f.OpportunityId == id))
            return new ServiceResult().AddError("",
                "This opportunity has follow-ups and cannot be deleted. Mark it as Lost instead.");

        _db.Opportunities.Remove(opp);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok(id);
    }

    // ===================== BUSINESS RULES (server-side, 5.2 + 17.7) =====================
    private static void ApplyClosingRules(OpportunityFormViewModel m)
    {
        if (m.Stage == OpportunityStage.Won) m.Probability = 100;
        if (m.Stage == OpportunityStage.Lost) m.Probability = 0;
    }

    // Repeated here on purpose: even if model validation were skipped, the service still refuses bad data
    private static void ValidateBusinessRules(OpportunityFormViewModel m, ServiceResult result)
    {
        var active = !IsClosed(m.Stage);

        if (m.Amount is null || m.Amount < 0)
            result.AddError(nameof(m.Amount), "Opportunity Amount cannot be negative.");
        else if (active && m.Amount <= 0)
            result.AddError(nameof(m.Amount), "Opportunity Amount must be greater than 0.");

        if (m.Probability is null or < 0 or > 100)
            result.AddError(nameof(m.Probability), "Probability must be between 0 and 100.");

        if (m.ExpectedCloseDate is null)
            result.AddError(nameof(m.ExpectedCloseDate), "Expected Close Date is required.");
        else if (active && m.ExpectedCloseDate.Value.Date < DateTime.Today)
            result.AddError(nameof(m.ExpectedCloseDate), "Expected Close Date cannot be in the past.");
    }

    // Customer (required) and Lead (optional) must exist and be visible to this user (5.2)
    private async Task<string?> ValidateRelatedAsync(OpportunityFormViewModel m, ServiceResult result)
    {
        string? owner = null;

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == m.CustomerId);
        if (customer == null || !_currentUser.CanAccess(customer))
            result.AddError(nameof(m.CustomerId), "Select a valid customer.");
        else
            owner = customer.AssignedTo;

        if (m.LeadId.HasValue)
        {
            var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == m.LeadId);
            if (lead == null || !_currentUser.CanAccess(lead))
                result.AddError(nameof(m.LeadId), "Select a valid lead.");
        }
        return owner;
    }

    private async Task<string?> ResolveAssignedToAsync(string? requested, string? currentValue,
                                                       string? customerOwner, ServiceResult result)
    {
        if (_currentUser.IsSalesExecutive && !_currentUser.CanSeeAllRecords)
            return currentValue ?? _currentUser.UserId;

        if (string.IsNullOrEmpty(requested)) return customerOwner;

        var user = await _userManager.FindByIdAsync(requested);
        if (user == null || !user.IsActive || !await _userManager.IsInRoleAsync(user, AppRoles.SalesExecutive))
        {
            result.AddError(nameof(OpportunityFormViewModel.AssignedTo), "Select a valid sales executive.");
            return null;
        }
        return requested;
    }
}