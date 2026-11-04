using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    // All roles; WHAT they see is scoped by the service (4.3, 17.11)
    [Authorize(Policy = AppPolicies.AllCrmUsers)]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboard;

        public DashboardController(IDashboardService dashboard)
        {
            _dashboard = dashboard;
        }

        // GET /Dashboard?period=week   or   ?period=custom&from=2026-11-01&to=2026-11-15
        public async Task<IActionResult> Index(string? period, DateTime? from, DateTime? to)
        {
            ViewData["Title"] = "Dashboard";
            return View(await _dashboard.BuildAsync(period, from, to));
        }
    }
}