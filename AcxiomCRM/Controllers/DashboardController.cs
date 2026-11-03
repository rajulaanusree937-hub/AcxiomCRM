using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    // All three roles can open the Dashboard; WHAT they see is scoped in Step 12 (4.3, 17.11)
    [Authorize(Policy = AppPolicies.AllCrmUsers)]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Dashboard";
            return View();
        }
    }
}