#nullable disable

using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcxiomCRM.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }


        public class InputModel
        {
            [Required(ErrorMessage = "Full Name is required.")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Enter a valid email address.")]
            [StringLength(150)]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Password is required.")]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)

        {
            returnUrl ??= Url.Content("~/");

            // Server-side validation — runs even if browser validation is bypassed
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    FullName = Input.FullName.Trim(),
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                // Identity hashes the password and applies the password policy
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    // Every self-registered user starts with the lowest-privilege role
                    await _userManager.AddToRoleAsync(user, AppRoles.SalesExecutive);
                    _logger.LogInformation("New user registered with SalesExecutive role.");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }


            return Page();
        }
    }
}