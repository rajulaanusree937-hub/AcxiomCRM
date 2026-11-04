using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IReportService
{
    List<ReportDefinition> AvailableReports();
    ReportDefinition? Find(string key);
    bool CanView(ReportDefinition def);
    Task<ReportViewModel> BuildAsync(ReportViewModel filter, bool paged = true);
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ILookupService _lookup;

    public ReportService(ApplicationDbContext db, ICurrentUserService user, ILookupService lookup)
    {
        _db = db;
        _user = user;
        _lookup = lookup;
    }

    // ===================== CATALOG (Section 11) =====================
    private static readonly List<ReportDefinition> Catalog = new()
    {
        new() { Key = "customers",     Title = "Customer Report",         Icon = "bi-people",       Description = "Customer list, status, owner and creation date." },

        new() { Key = "leads",         Title = "Lead Report",             Icon = "bi-person-plus",  Description = "Lead source, status, owner and conversion information." },
        new() { Key = "followups",     Title = "Follow-Up Report",        Icon = "bi-calendar-check", Description = "Planned, completed, missed and overdue activities." },
        new() { Key = "opportunities", Title = "Opportunity Report",      Icon = "bi-briefcase",    Description = "Stage, amount, probability and expected close date." },
        new() { Key = "pipeline",      Title = "Pipeline Report",         Icon = "bi-kanban",       Description = "Stage-wise and owner-wise pipeline amount." },
        new() { Key = "conversion",    Title = "Sales / Conversion Report", Icon = "bi-arrow-repeat", Description = "Lead conversion and opportunity outcome metrics." },
        new() { Key = "useractivity",  Title = "User Activity Report",    Icon = "bi-activity",     Description = "User actions and activity counts.", ManagerOrAdminOnly = true },
        new() { Key = "audit",         Title = "Audit Report",            Icon = "bi-journal-text", Description = "Security and business audit events.", AdminOnly = true, ExternalController = "AuditLogs" }
    };

    public List<ReportDefinition> AvailableReports() => Catalog.Where(CanView).ToList();
    public ReportDefinition? Find(string key) => Catalog.FirstOrDefault(r => r.Key == key);

    public bool CanView(ReportDefinition def) =>
        def.AdminOnly ? _user.IsAdmin :
        def.ManagerOrAdminOnly ? _user.CanSeeAllRecords :
        true;

    // ===================== BUILD =====================
    public async Task<ReportViewModel> BuildAsync(ReportViewModel f, bool paged = true)
    {
        var def = Find(f.Key)!;
        f.Title = def.Title;
        f.Description = def.Description;
        f.ShowOwnerFilter = _user.CanSeeAllRecords;
        if (f.ShowOwnerFilter)
            f.OwnerOptions = await _lookup.SalesExecutiveOptionsAsync(f.Owner);

        switch (f.Key)
        {
            case "customers": await Customers(f); break;
            case "leads": await Leads(f); break;
            case "followups": await FollowUps(f); break;

            case "opportunities": await Opportunities(f); break;
            case "pipeline": await Pipeline(f); break;
            case "conversion": await Conversion(f); break;
            case "useractivity": await UserActivity(f); break;
        }

        // ---------- Search across all columns ----------
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            f.Rows = f.Rows.Where(r => r.Any(c => c?.ToString()?.Contains(s, StringComparison.OrdinalIgnoreCase) == true)).ToList();
        }

        // ---------- Sorting (click a column header) ----------
        var sortIndex = f.Columns.FindIndex(c => c.Key == f.SortBy);
        if (sortIndex >= 0)
        {
            f.Rows = f.Desc
                ? f.Rows.OrderByDescending(r => r[sortIndex], Comparer<object?>.Default).ToList()
                : f.Rows.OrderBy(r => r[sortIndex], Comparer<object?>.Default).ToList();
        }

        // ---------- Paging ----------
        f.TotalCount = f.Rows.Count;
        if (paged)
        {
            f.PageSize = Math.Clamp(f.PageSize, 10, 100);
            f.Page = Math.Clamp(f.Page, 1, Math.Max(1, f.TotalPages));
            f.Rows = f.Rows.Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToList();
        }
        return f;
    }


    // ===================== HELPERS =====================
    private (DateTime? fromUtc, DateTime? toUtc) Range(ReportViewModel f) =>
        (f.DateFrom?.Date.ToUniversalTime(), f.DateTo?.Date.AddDays(1).ToUniversalTime());

    private string? OwnerFilter(ReportViewModel f) =>
        _user.CanSeeAllRecords && !string.IsNullOrEmpty(f.Owner) ? f.Owner : null;

    private static void Cols(ReportViewModel f, params (string key, string header, string kind)[] cols) =>
        f.Columns = cols.Select(c => new ReportColumn { Key = c.key, Header = c.header, Kind = c.kind }).ToList();

    private static string NameOf(Dictionary<string, string> names, string? id) =>
        id != null && names.TryGetValue(id, out var n) ? n : "Unassigned";

    // ===================== 1. CUSTOMER REPORT =====================
    private async Task Customers(ReportViewModel f)
    {
        var (from, to) = Range(f);
        var owner = OwnerFilter(f);
        f.StatusOptions = Enum.GetNames<CustomerStatus>().ToList();

        var q = _db.Customers.AsNoTracking().VisibleTo(_user);
        if (from.HasValue) q = q.Where(c => c.CreatedDate >= from);
        if (to.HasValue) q = q.Where(c => c.CreatedDate < to);
        if (owner != null) q = q.Where(c => c.AssignedTo == owner);
        if (Enum.TryParse<CustomerStatus>(f.Status, out var st)) q = q.Where(c => c.Status == st);

        var data = await q.OrderByDescending(c => c.CreatedDate).ToListAsync();
        var names = await _lookup.UserNamesAsync(data.Select(d => d.AssignedTo));

        Cols(f, ("code", "Code", "text"), ("name", "Customer", "text"), ("email", "Email", "text"), ("phone", "Phone", "text"),
                ("company", "Company", "text"), ("city", "City", "text"), ("status", "Status", "text"),

                ("owner", "Owner", "text"), ("created", "Created", "date"));

        f.Rows = data.Select(c => new object?[] { c.CustomerCode, c.CustomerName, c.Email, c.Phone, c.CompanyName,
                                                  c.City, c.Status.ToString(), NameOf(names, c.AssignedTo),
                                                  c.CreatedDate.ToLocalTime() }).ToList();

        f.Summary.Add(new("Total customers", data.Count.ToString()));
        f.Summary.Add(new("Active", data.Count(c => c.Status == CustomerStatus.Active).ToString()));
        f.Summary.Add(new("Inactive", data.Count(c => c.Status == CustomerStatus.Inactive).ToString()));
    }

    // ===================== 2. LEAD REPORT =====================
    private async Task Leads(ReportViewModel f)
    {
        var (from, to) = Range(f);
        var owner = OwnerFilter(f);
        f.StatusOptions = Enum.GetNames<LeadStatus>().ToList();

        var q = _db.Leads.AsNoTracking().VisibleTo(_user);
        if (from.HasValue) q = q.Where(l => l.CreatedDate >= from);
        if (to.HasValue) q = q.Where(l => l.CreatedDate < to);
        if (owner != null) q = q.Where(l => l.AssignedTo == owner);
        if (Enum.TryParse<LeadStatus>(f.Status, out var st)) q = q.Where(l => l.Status == st);

        var data = await q.OrderByDescending(l => l.CreatedDate).ToListAsync();
        var names = await _lookup.UserNamesAsync(data.Select(d => d.AssignedTo));

        Cols(f, ("code", "Code", "text"), ("name", "Lead", "text"), ("company", "Company", "text"), ("source", "Source", "text"),
                ("status", "Status", "text"), ("priority", "Priority", "text"), ("value", "Expected Value", "money"),
                ("owner", "Owner", "text"), ("converted", "Converted", "text"), ("created", "Created", "date"));

        f.Rows = data.Select(l => new object?[] { l.LeadCode, l.LeadName, l.CompanyName, l.Source.ToString(), l.Status.ToString(),

                                                  l.Priority.ToString(), l.ExpectedValue, NameOf(names, l.AssignedTo),
                                                  l.ConvertedCustomerId != null ? "Yes" : "No", l.CreatedDate.ToLocalTime() }).ToList();

        var converted = data.Count(l => l.Status == LeadStatus.Converted);
        f.Summary.Add(new("Total leads", data.Count.ToString()));
        f.Summary.Add(new("Converted", converted.ToString()));
        f.Summary.Add(new("Conversion rate", data.Count == 0 ? "0%" : $"{converted * 100.0 / data.Count:0.0}%"));
        f.Summary.Add(new("Expected value", "₹" + data.Sum(l => l.ExpectedValue).ToString("N0")));
    }

    // ===================== 3. FOLLOW-UP REPORT =====================
    private async Task FollowUps(ReportViewModel f)
    {
        var (from, to) = (f.DateFrom?.Date, f.DateTo?.Date.AddDays(1));   // FollowUpDate is local time
        var owner = OwnerFilter(f);
        f.DateFieldLabel = "Follow-up date";
        f.StatusOptions = Enum.GetNames<FollowUpStatus>().Append("Overdue").ToList();
        var now = DateTime.Now;

        var q = _db.FollowUps.AsNoTracking().VisibleTo(_user);
        if (from.HasValue) q = q.Where(x => x.FollowUpDate >= from);
        if (to.HasValue) q = q.Where(x => x.FollowUpDate < to);
        if (owner != null) q = q.Where(x => x.AssignedTo == owner);
        if (f.Status == "Overdue") q = q.Where(x => x.Status == FollowUpStatus.Planned && x.FollowUpDate < now);
        else if (Enum.TryParse<FollowUpStatus>(f.Status, out var st)) q = q.Where(x => x.Status == st);

        var data = await q.OrderBy(x => x.FollowUpDate)
            .Select(x => new
            {
                x.FollowUpDate,
                x.Subject,
                x.FollowUpType,
                x.Status,
                x.AssignedTo,
                Related = x.Opportunity != null ? "Opportunity: " + x.Opportunity.OpportunityName
                        : x.Lead != null ? "Lead: " + x.Lead.LeadName

                        : x.Customer != null ? "Customer: " + x.Customer.CustomerName : "—"
            }).ToListAsync();
        var names = await _lookup.UserNamesAsync(data.Select(d => d.AssignedTo));

        Cols(f, ("date", "Date", "datetime"), ("subject", "Subject", "text"), ("type", "Type", "text"), ("status", "Status", "text"),
                ("overdue", "Overdue", "text"), ("related", "Related To", "text"), ("owner", "Owner", "text"));

        f.Rows = data.Select(x => new object?[] { x.FollowUpDate, x.Subject, x.FollowUpType.ToString(), x.Status.ToString(),
                                                  x.Status == FollowUpStatus.Planned && x.FollowUpDate < now ? "Yes" : "No",
                                                  x.Related, NameOf(names, x.AssignedTo) }).ToList();

        f.Summary.Add(new("Planned", data.Count(x => x.Status == FollowUpStatus.Planned).ToString()));
        f.Summary.Add(new("Completed", data.Count(x => x.Status == FollowUpStatus.Completed).ToString()));
        f.Summary.Add(new("Missed", data.Count(x => x.Status == FollowUpStatus.Missed).ToString()));
        f.Summary.Add(new("Overdue", data.Count(x => x.Status == FollowUpStatus.Planned && x.FollowUpDate < now).ToString()));
    }

    // ===================== 4. OPPORTUNITY REPORT =====================
    private async Task Opportunities(ReportViewModel f)
    {
        var (from, to) = Range(f);
        var owner = OwnerFilter(f);
        f.StatusOptions = Enum.GetNames<OpportunityStage>().ToList();
        f.DateFieldLabel = "Created";

        var q = _db.Opportunities.AsNoTracking().VisibleTo(_user);
        if (from.HasValue) q = q.Where(o => o.CreatedDate >= from);
        if (to.HasValue) q = q.Where(o => o.CreatedDate < to);
        if (owner != null) q = q.Where(o => o.AssignedTo == owner);
        if (Enum.TryParse<OpportunityStage>(f.Status, out var st)) q = q.Where(o => o.Stage == st);

        var data = await q.OrderBy(o => o.ExpectedCloseDate)

            .Select(o => new {
                o.OpportunityName,
                Customer = o.Customer!.CustomerName,
                o.Stage,
                o.Amount,
                o.Probability,
                o.ExpectedCloseDate,
                o.Status,
                o.AssignedTo
            })
            .ToListAsync();
        var names = await _lookup.UserNamesAsync(data.Select(d => d.AssignedTo));

        Cols(f, ("name", "Opportunity", "text"), ("customer", "Customer", "text"), ("stage", "Stage", "text"),
                ("amount", "Amount", "money"), ("prob", "Probability", "percent"), ("weighted", "Weighted", "money"),
                ("close", "Expected Close", "date"), ("status", "Status", "text"), ("owner", "Owner", "text"));

        f.Rows = data.Select(o => new object?[] { o.OpportunityName, o.Customer, o.Stage.ToString(), o.Amount, (decimal)o.Probability,
                                                  o.Amount * o.Probability / 100m, o.ExpectedCloseDate, o.Status.ToString(),
                                                  NameOf(names, o.AssignedTo) }).ToList();

        f.Summary.Add(new("Opportunities", data.Count.ToString()));
        f.Summary.Add(new("Total amount", "₹" + data.Sum(o => o.Amount).ToString("N0")));
        f.Summary.Add(new("Weighted", "₹" + data.Sum(o => o.Amount * o.Probability / 100m).ToString("N0")));
    }

    // ===================== 5. PIPELINE REPORT: stage-wise / owner-wise =====================
    private async Task Pipeline(ReportViewModel f)
    {
        var (from, to) = Range(f);
        var owner = OwnerFilter(f);
        f.ShowGroupBy = true;
        f.GroupBy = f.GroupBy == "owner" ? "owner" : "stage";

        var q = _db.Opportunities.AsNoTracking().VisibleTo(_user);
        if (from.HasValue) q = q.Where(o => o.CreatedDate >= from);
        if (to.HasValue) q = q.Where(o => o.CreatedDate < to);
        if (owner != null) q = q.Where(o => o.AssignedTo == owner);

        var data = await q.Select(o => new { o.Stage, o.Status, o.Amount, o.Probability, o.AssignedTo }).ToListAsync();


        if (f.GroupBy == "stage")
        {
            Cols(f, ("stage", "Stage", "text"), ("count", "Opportunities", "number"), ("amount", "Total Amount", "money"),
                    ("weighted", "Weighted Amount", "money"));
            f.Rows = Enum.GetValues<OpportunityStage>().Select(s =>
            {
                var rows = data.Where(d => d.Stage == s).ToList();
                return new object?[] { s.ToString(), rows.Count, rows.Sum(r => r.Amount), rows.Sum(r => r.Amount * r.Probability / 100m) };
            }).ToList();
        }
        else
        {
            var names = await _lookup.UserNamesAsync(data.Select(d => d.AssignedTo));
            Cols(f, ("owner", "Owner", "text"), ("open", "Open", "number"), ("openAmount", "Open Amount", "money"),
                    ("weighted", "Weighted Open", "money"), ("won", "Won", "number"), ("wonAmount", "Won Amount", "money"),
                    ("lost", "Lost", "number"));
            f.Rows = data.GroupBy(d => d.AssignedTo).Select(g =>
            {
                var open = g.Where(x => x.Status == OpportunityStatus.Open).ToList();
                var won = g.Where(x => x.Stage == OpportunityStage.Won).ToList();
                return new object?[] { NameOf(names, g.Key), open.Count, open.Sum(x => x.Amount),
                                       open.Sum(x => x.Amount * x.Probability / 100m), won.Count, won.Sum(x => x.Amount),
                                       g.Count(x => x.Stage == OpportunityStage.Lost) };
            }).ToList();
        }

        var openAll = data.Where(d => d.Status == OpportunityStatus.Open).ToList();
        f.Summary.Add(new("Open pipeline", "₹" + openAll.Sum(d => d.Amount).ToString("N0")));
        f.Summary.Add(new("Weighted pipeline", "₹" + openAll.Sum(d => d.Amount * d.Probability / 100m).ToString("N0")));
        f.Summary.Add(new("Won", "₹" + data.Where(d => d.Stage == OpportunityStage.Won).Sum(d => d.Amount).ToString("N0")));
    }


    // ===================== 6. SALES / CONVERSION REPORT =====================
    private async Task Conversion(ReportViewModel f)
    {
        var (from, to) = Range(f);
        var owner = OwnerFilter(f);

        var leads = _db.Leads.AsNoTracking().VisibleTo(_user);
        var opps = _db.Opportunities.AsNoTracking().VisibleTo(_user);
        if (from.HasValue) { leads = leads.Where(l => l.CreatedDate >= from); opps = opps.Where(o => o.CreatedDate >= from); }
        if (to.HasValue) { leads = leads.Where(l => l.CreatedDate < to); opps = opps.Where(o => o.CreatedDate < to); }
        if (owner != null) { leads = leads.Where(l => l.AssignedTo == owner); opps = opps.Where(o => o.AssignedTo == owner); }

        var leadData = await leads.Select(l => new { l.Source, l.Status }).ToListAsync();
        var oppData = await opps.Select(o => new { o.Stage, o.Amount }).ToListAsync();

        Cols(f, ("source", "Lead Source", "text"), ("total", "Total Leads", "number"), ("converted", "Converted", "number"),
                ("lost", "Lost", "number"), ("open", "Still Open", "number"), ("rate", "Conversion Rate", "percent"));

        f.Rows = Enum.GetValues<LeadSource>().Select(src =>
        {
            var g = leadData.Where(l => l.Source == src).ToList();
            var conv = g.Count(l => l.Status == LeadStatus.Converted);
            return new object?[] { src.ToString(), g.Count, conv, g.Count(l => l.Status == LeadStatus.Lost),
                                   g.Count(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost),
                                   g.Count == 0 ? 0m : Math.Round(conv * 100m / g.Count, 1) };
        }).ToList();

        var totalConv = leadData.Count(l => l.Status == LeadStatus.Converted);
        var won = oppData.Where(o => o.Stage == OpportunityStage.Won).ToList();
        var lost = oppData.Count(o => o.Stage == OpportunityStage.Lost);
        var closed = won.Count + lost;


        f.Summary.Add(new("Leads converted", $"{totalConv} of {leadData.Count}"));
        f.Summary.Add(new("Lead conversion rate", leadData.Count == 0 ? "0%" : $"{totalConv * 100.0 / leadData.Count:0.0}%"));
        f.Summary.Add(new("Opportunities won / lost", $"{won.Count} / {lost}"));
        f.Summary.Add(new("Win rate", closed == 0 ? "—" : $"{won.Count * 100.0 / closed:0.0}%"));
        f.Summary.Add(new("Won value", "₹" + won.Sum(o => o.Amount).ToString("N0")));
    }

    // ===================== 7. USER ACTIVITY REPORT (Admin/Manager) =====================
    private async Task UserActivity(ReportViewModel f)
    {
        var (from, to) = Range(f);
        f.DateFieldLabel = "Activity date";

        var q = _db.AuditLogs.AsNoTracking().Where(a => a.UserId != null);
        if (from.HasValue) q = q.Where(a => a.CreatedDate >= from);
        if (to.HasValue) q = q.Where(a => a.CreatedDate < to);
        if (!string.IsNullOrEmpty(f.Owner)) q = q.Where(a => a.UserId == f.Owner);

        var stats = await q.GroupBy(a => a.UserId!)
            .Select(g => new
            {
                UserId = g.Key,
                Logins = g.Count(a => a.Action == AuditActions.Login),
                Failed = g.Count(a => a.Action == AuditActions.LoginFailed || a.Action == AuditActions.Lockout),
                Creates = g.Count(a => a.Action == AuditActions.Create),
                Updates = g.Count(a => a.Action == AuditActions.Update),
                Deletes = g.Count(a => a.Action == AuditActions.Delete),
                Total = g.Count(),
                Last = g.Max(a => a.CreatedDate)
            }).ToListAsync();


        var names = await _lookup.UserNamesAsync(stats.Select(s => (string?)s.UserId));

        Cols(f, ("user", "User", "text"), ("logins", "Logins", "number"), ("failed", "Failed Logins", "number"),
                ("creates", "Creates", "number"), ("updates", "Updates", "number"), ("deletes", "Deletes", "number"),
                ("total", "Total Actions", "number"), ("last", "Last Activity", "datetime"));

        f.Rows = stats.OrderByDescending(s => s.Total)
            .Select(s => new object?[] { NameOf(names, s.UserId), s.Logins, s.Failed, s.Creates, s.Updates, s.Deletes,
                                         s.Total, s.Last.ToLocalTime() }).ToList();

        f.Summary.Add(new("Active users", stats.Count.ToString()));
        f.Summary.Add(new("Total actions", stats.Sum(s => s.Total).ToString()));
        f.Summary.Add(new("Failed logins", stats.Sum(s => s.Failed).ToString()));
    }
}