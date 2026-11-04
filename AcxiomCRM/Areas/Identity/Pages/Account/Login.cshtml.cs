#nullable disable

using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcxiomCRM.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _audit;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService audit,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _audit = audit;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Enter a valid email address.")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Password is required.")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
                ModelState.AddModelError(string.Empty, ErrorMessage);

            returnUrl ??= Url.Content("~/");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // Deactivated account (4.7)
            if (user != null && !user.IsActive)
            {
                if (await _userManager.CheckPasswordAsync(user, Input.Password))
                    ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact the administrator.");
                else
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");

                await _audit.LogAsync(AuditActions.LoginFailed, "User", user.Id,
                    newValue: new { Input.Email, Reason = "Account deactivated" }, userId: user.Id);
                return Page();
            }

            // lockoutOnFailure: true -> 5 wrong passwords = 15 min lockout (6.3)
            var result = await _signInManager.PasswordSignInAsync(
                Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");
                await _audit.LogAsync(AuditActions.Login, "User", user?.Id, userId: user?.Id);
                return LocalRedirect(returnUrl);
            }

            if (result.RequiresTwoFactor)
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                await _audit.LogAsync(AuditActions.Lockout, "User", user?.Id,
                    newValue: new { Input.Email, Reason = "Too many failed attempts" }, userId: user?.Id);
                return RedirectToPage("./Lockout");
            }

            // Wrong email or password — the attempted email is logged, the password NEVER is
            await _audit.LogAsync(AuditActions.LoginFailed, "User", user?.Id,
                newValue: new { Input.Email, Reason = "Invalid credentials" }, userId: user?.Id);

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }
    }
}