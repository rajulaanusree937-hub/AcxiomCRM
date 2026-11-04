namespace AcxiomCRM.ViewModels;

public class DashboardViewModel
{
    // ---------- Date filter (4.3): today, week, month, custom ----------
    public string Period { get; set; } = "month";
    public DateTime From { get; set; }
    public DateTime To { get; set; }          // exclusive
    public string PeriodLabel { get; set; } = "";

    // ---------- KPI cards (17.11) — always the user's authorized scope ----------
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }

    // ---------- In the selected period ----------
    public int NewLeadsInPeriod { get; set; }
    public int NewCustomersInPeriod { get; set; }
    public decimal WonValueInPeriod { get; set; }
    public int PendingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }

    // ---------- Charts (17.12) ----------
    public List<string> LeadStatusLabels { get; set; } = new();
    public List<int> LeadStatusCounts { get; set; } = new();

    public List<string> PipelineLabels { get; set; } = new();
    public List<int> PipelineCounts { get; set; } = new();
    public List<decimal> PipelineAmounts { get; set; } = new();

    public List<string> MonthlyLabels { get; set; } = new();
    public List<decimal> MonthlyWon { get; set; } = new();
    public List<decimal> MonthlyLost { get; set; } = new();

    // ---------- Lists ----------
    public List<FollowUpListItem> UpcomingFollowUps { get; set; } = new();

    // ---------- Admin only: user/security statistics (4.3) ----------
    public bool ShowAdminStats { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int LockedUsers { get; set; }
    public int FailedLoginsInPeriod { get; set; }
}