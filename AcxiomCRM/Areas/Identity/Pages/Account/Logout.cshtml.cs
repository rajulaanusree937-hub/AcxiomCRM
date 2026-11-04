#nullable disable

using AcxiomCRM.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcxiomCRM.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuditService _audit;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(SignInManager<ApplicationUser> signInManager, IAuditService audit, ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _audit = audit;
            _logger = logger;
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            // Log BEFORE signing out, while we still know who the user is
            if (User.Identity?.IsAuthenticated == true)
                await _audit.LogAsync(AuditActions.Logout, "User", _signInManager.UserManager.GetUserId(User));

            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");

            if (returnUrl != null && Url.IsLocalUrl(returnUrl))

                return LocalRedirect(returnUrl);

            return RedirectToPage("./Login");
        }
    }
}