using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    // Protected by the global login filter (Part 3A)
    public class HomeController : Controller
    {
        // Logged-in users only. Will redirect to the Dashboard in Step 4.
        // After login, users land on "/" -> send them to the Dashboard (17.11)
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Dashboard");
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        // Must be public, or errors for anonymous users would loop to the login page
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
