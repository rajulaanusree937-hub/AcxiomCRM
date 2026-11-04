using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> BuildAsync(string? period, DateTime? from, DateTime? to);
}

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFollowUpService _followUps;

    public DashboardService(ApplicationDbContext db, ICurrentUserService currentUser, IFollowUpService followUps)
    {
        _db = db;
        _currentUser = currentUser;
        _followUps = followUps;
    }

    public async Task<DashboardViewModel> BuildAsync(string? period, DateTime? from, DateTime? to)
    {
        var m = new DashboardViewModel();
        SetPeriod(m, period, from, to);

        // Everything below uses VisibleTo -> charts and cards are server-authorized (4.3)
        var customers = _db.Customers.AsNoTracking().VisibleTo(_currentUser);

        var leads = _db.Leads.AsNoTracking().VisibleTo(_currentUser);
        var opps = _db.Opportunities.AsNoTracking().VisibleTo(_currentUser);
        var followUps = _db.FollowUps.AsNoTracking().VisibleTo(_currentUser);

        // UTC boundaries for CreatedDate (stored in UTC)
        var fromUtc = m.From.ToUniversalTime();
        var toUtc = m.To.ToUniversalTime();

        // ---------- KPI cards (17.11) ----------
        m.TotalCustomers = await customers.CountAsync();
        m.TotalLeads = await leads.CountAsync();
        m.OpenLeads = await leads.CountAsync(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost);
        m.TotalOpportunities = await opps.CountAsync();
        m.OpenOpportunities = await opps.CountAsync(o => o.Status == OpportunityStatus.Open);
        m.WonOpportunities = await opps.CountAsync(o => o.Stage == OpportunityStage.Won);
        m.LostOpportunities = await opps.CountAsync(o => o.Stage == OpportunityStage.Lost);
        m.TotalPipelineValue = await opps.Where(o => o.Status == OpportunityStatus.Open)
                                         .SumAsync(o => (decimal?)o.Amount) ?? 0;
        m.WeightedPipelineValue = await opps.Where(o => o.Status == OpportunityStatus.Open)
                                            .SumAsync(o => (decimal?)(o.Amount * o.Probability / 100m)) ?? 0;

        // ---------- Selected period ----------
        m.NewLeadsInPeriod = await leads.CountAsync(l => l.CreatedDate >= fromUtc && l.CreatedDate < toUtc);
        m.NewCustomersInPeriod = await customers.CountAsync(c => c.CreatedDate >= fromUtc && c.CreatedDate < toUtc);
        m.WonValueInPeriod = await opps
            .Where(o => o.Stage == OpportunityStage.Won && o.ModifiedDate >= fromUtc && o.ModifiedDate < toUtc)
            .SumAsync(o => (decimal?)o.Amount) ?? 0;

        var now = DateTime.Now;
        m.PendingFollowUps = await followUps.CountAsync(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate >= now);
        m.OverdueFollowUps = await followUps.CountAsync(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate < now);


        // ---------- Chart 1: Lead Status (17.12) ----------
        var leadCounts = await leads.GroupBy(l => l.Status)
                                    .Select(g => new { g.Key, Count = g.Count() })
                                    .ToListAsync();
        foreach (var s in new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified,
                                  LeadStatus.Unqualified, LeadStatus.Lost, LeadStatus.Converted })
        {
            m.LeadStatusLabels.Add(s.ToString());
            m.LeadStatusCounts.Add(leadCounts.FirstOrDefault(x => x.Key == s)?.Count ?? 0);
        }

        // ---------- Chart 2: Opportunity Pipeline (17.12) ----------
        var stageData = await opps.GroupBy(o => o.Stage)
                                  .Select(g => new { g.Key, Count = g.Count(), Amount = g.Sum(o => o.Amount) })
                                  .ToListAsync();
        foreach (var s in Enum.GetValues<OpportunityStage>())
        {
            var row = stageData.FirstOrDefault(x => x.Key == s);
            m.PipelineLabels.Add(s.ToString());
            m.PipelineCounts.Add(row?.Count ?? 0);
            m.PipelineAmounts.Add(row?.Amount ?? 0);
        }

        // ---------- Chart 3: Monthly Sales — won vs lost amount, last 6 months (17.12) ----------
        var firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        var closed = await opps
            .Where(o => (o.Stage == OpportunityStage.Won || o.Stage == OpportunityStage.Lost) &&
                        (o.ModifiedDate ?? o.CreatedDate) >= firstMonth.ToUniversalTime())
            .Select(o => new { o.Stage, o.Amount, When = o.ModifiedDate ?? o.CreatedDate })
            .ToListAsync();

        for (var month = firstMonth; month <= DateTime.Today; month = month.AddMonths(1))

        {
            var inMonth = closed.Where(c => c.When.ToLocalTime().Year == month.Year &&
                                            c.When.ToLocalTime().Month == month.Month).ToList();
            m.MonthlyLabels.Add(month.ToString("MMM yyyy"));
            m.MonthlyWon.Add(inMonth.Where(c => c.Stage == OpportunityStage.Won).Sum(c => c.Amount));
            m.MonthlyLost.Add(inMonth.Where(c => c.Stage == OpportunityStage.Lost).Sum(c => c.Amount));
        }

        // ---------- Upcoming follow-ups (4.3 Sales Executive view) ----------
        var upcoming = await _followUps.GetListAsync(new FollowUpListViewModel { View = "upcoming", PageSize = 5 });
        m.UpcomingFollowUps = upcoming.Items;

        // ---------- Admin: user / security statistics (4.3) ----------
        if (_currentUser.IsAdmin)
        {
            var nowOffset = DateTimeOffset.UtcNow;
            m.ShowAdminStats = true;
            m.ActiveUsers = await _db.Users.CountAsync(u => u.IsActive);
            m.InactiveUsers = await _db.Users.CountAsync(u => !u.IsActive);
            m.LockedUsers = await _db.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > nowOffset);
            m.FailedLoginsInPeriod = await _db.AuditLogs.CountAsync(a =>
                (a.Action == AuditActions.LoginFailed || a.Action == AuditActions.Lockout) &&
                a.CreatedDate >= fromUtc && a.CreatedDate < toUtc);
        }

        return m;
    }

    // Today / This Week / This Month / Custom Range (4.3)
    private static void SetPeriod(DashboardViewModel m, string? period, DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;

        m.Period = period ?? "month";

        switch (m.Period)
        {
            case "today":
                m.From = today; m.To = today.AddDays(1); m.PeriodLabel = "Today";
                break;
            case "week":
                var diff = ((int)today.DayOfWeek + 6) % 7;   // Monday = start of week
                m.From = today.AddDays(-diff); m.To = m.From.AddDays(7); m.PeriodLabel = "This Week";
                break;
            case "custom" when from.HasValue && to.HasValue && from <= to:
                m.From = from.Value.Date; m.To = to.Value.Date.AddDays(1);
                m.PeriodLabel = $"{m.From:dd MMM yyyy} – {to.Value:dd MMM yyyy}";
                break;
            default:
                m.Period = "month";
                m.From = new DateTime(today.Year, today.Month, 1); m.To = m.From.AddMonths(1);
                m.PeriodLabel = "This Month";
                break;
        }
    }
}