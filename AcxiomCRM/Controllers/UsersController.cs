using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    // User Management + Role Management = Admin only (7.1)
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class UsersController : Controller
    {
        private readonly IUserAdminService _users;

        public UsersController(IUserAdminService users)
        {
            _users = users;
        }

        public async Task<IActionResult> Index([FromQuery] UserListViewModel filter)
        {
            ViewData["Title"] = "Users & Roles";
            return View(await _users.GetListAsync(filter));
        }

        // Role list + permission matrix (7.1)
        public async Task<IActionResult> Roles()
        {
            ViewData["Title"] = "Roles & Permissions";
            return View(await _users.GetRoleCountsAsync());
        }

        public IActionResult Create()
        {
            ViewData["Title"] = "New User";
            return View(new UserCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserCreateViewModel model)
        {
            ViewData["Title"] = "New User";
            if (!ModelState.IsValid) return View(model);

            var result = await _users.CreateAsync(model);
            if (!result.Succeeded)
            {
                foreach (var (field, message) in result.Errors) ModelState.AddModelError(field, message);
                return View(model);
            }

            TempData["Success"] = $"User '{model.Email}' was created with role {model.Role}.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            var model = await _users.GetEditModelAsync(id);
            if (model == null) return NotFound();

            ViewData["Title"] = "Edit User";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserEditViewModel model)
        {
            if (id != model.Id) return BadRequest();
            ViewData["Title"] = "Edit User";

            if (!ModelState.IsValid) return View(await RefillAsync(model));

            var result = await _users.UpdateAsync(model);
            if (result.NotFound) return NotFound();
            if (!result.Succeeded)
            {
                foreach (var (field, message) in result.Errors) ModelState.AddModelError(field, message);
                return View(await RefillAsync(model));
            }

            TempData["Success"] = $"User '{model.Email}' was updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var result = await _users.UnlockAsync(id);
            if (result.NotFound) return NotFound();

            TempData["Success"] = "Account unlocked.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _users.GetEditModelAsync(id);
            if (user == null) return NotFound();

            ViewData["Title"] = "Reset Password";
            return View(new UserResetPasswordViewModel { Id = user.Id, Email = user.Email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, UserResetPasswordViewModel model)
        {
            if (id != model.Id) return BadRequest();
            ViewData["Title"] = "Reset Password";
            if (!ModelState.IsValid) return View(model);

            var result = await _users.ResetPasswordAsync(model);
            if (result.NotFound) return NotFound();
            if (!result.Succeeded)
            {
                foreach (var (field, message) in result.Errors) ModelState.AddModelError(field, message);
                return View(model);
            }

            TempData["Success"] = $"Password for '{model.Email}' was reset. Share the temporary password securely.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        // Keep read-only display fields (lockout info) when redisplaying the form
        private async Task<UserEditViewModel> RefillAsync(UserEditViewModel posted)
        {
            var fresh = await _users.GetEditModelAsync(posted.Id);
            if (fresh != null)
            {
                posted.Email = fresh.Email;
                posted.IsLockedOut = fresh.IsLockedOut;
                posted.LockoutEnd = fresh.LockoutEnd;
                posted.FailedLoginCount = fresh.FailedLoginCount;
            }
            return posted;
        }
    }
}