using AcxiomCRM.Data;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IUserAdminService
{
    Task<UserListViewModel> GetListAsync(UserListViewModel filter);
    Task<UserEditViewModel?> GetEditModelAsync(string id);
    Task<ServiceResult> CreateAsync(UserCreateViewModel model);
    Task<ServiceResult> UpdateAsync(UserEditViewModel model);
    Task<ServiceResult> UnlockAsync(string id);
    Task<ServiceResult> ResetPasswordAsync(UserResetPasswordViewModel model);
    Task<Dictionary<string, int>> GetRoleCountsAsync();
}

public class UserAdminService : IUserAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public UserAdminService(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
                            ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _currentUser = currentUser;
        _audit = audit;
    }

    // ===================== LIST =====================
    public async Task<UserListViewModel> GetListAsync(UserListViewModel filter)
    {
        var now = DateTimeOffset.UtcNow;

        // Users + their (single) role. Only safe fields are selected — no PasswordHash.
        var query =
            from u in _db.Users.AsNoTracking()
            join ur in _db.UserRoles on u.Id equals ur.UserId into urj
            from ur in urj.DefaultIfEmpty()
            join r in _db.Roles on ur.RoleId equals r.Id into rj
            from r in rj.DefaultIfEmpty()
            select new UserListItem
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "",
                Role = r != null ? r.Name : null,
                IsActive = u.IsActive,
                IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > now,
                LockoutEnd = u.LockoutEnd,
                FailedLoginCount = u.AccessFailedCount,
                CreatedDate = u.CreatedDate
            };

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(x => x.FullName.Contains(s) || x.Email.Contains(s));
        }
        if (!string.IsNullOrEmpty(filter.Role)) query = query.Where(x => x.Role == filter.Role);

        query = filter.Status switch
        {
            "active" => query.Where(x => x.IsActive),
            "inactive" => query.Where(x => !x.IsActive),
            "locked" => query.Where(x => x.IsLockedOut),
            _ => query
        };

        filter.Items = await query.OrderBy(x => x.FullName).ToListAsync();
        return filter;
    }

    public async Task<Dictionary<string, int>> GetRoleCountsAsync() =>
        await (from r in _db.Roles
               select new { r.Name, Count = _db.UserRoles.Count(ur => ur.RoleId == r.Id) })
              .ToDictionaryAsync(x => x.Name!, x => x.Count);

    public async Task<UserEditViewModel?> GetEditModelAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return new UserEditViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            Role = roles.FirstOrDefault() ?? AppRoles.SalesExecutive,
            IsActive = user.IsActive,
            IsLockedOut = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
            LockoutEnd = user.LockoutEnd,
            FailedLoginCount = user.AccessFailedCount
        };
    }

    // ===================== CREATE =====================
    public async Task<ServiceResult> CreateAsync(UserCreateViewModel model)
    {
        var result = new ServiceResult();
        if (!AppRoles.All.Contains(model.Role))
            return result.AddError(nameof(model.Role), "Select a valid role.");

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true,
            FullName = model.FullName.Trim(),
            IsActive = model.IsActive,
            CreatedDate = DateTime.UtcNow
        };

        // Identity hashes the password and enforces the password policy (6.2)
        var create = await _userManager.CreateAsync(user, model.Password);
        if (!create.Succeeded)
        {
            foreach (var e in create.Errors)
                result.AddError(e.Code.Contains("Password") ? nameof(model.Password) : "", e.Description);
            return result;
        }

        await _userManager.AddToRoleAsync(user, model.Role);

        await _audit.LogAsync(AuditActions.Create, "User", user.Id,
            newValue: new { user.Email, user.FullName, model.Role, user.IsActive });

        return ServiceResult.Ok();
    }

    // ===================== UPDATE: name, role, active =====================
    public async Task<ServiceResult> UpdateAsync(UserEditViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null) return ServiceResult.NotFoundResult();

        var result = new ServiceResult();
        if (!AppRoles.All.Contains(model.Role))
            return result.AddError(nameof(model.Role), "Select a valid role.");

        // Safety: an admin cannot lock themselves out of administration
        var isSelf = user.Id == _currentUser.UserId;
        if (isSelf && model.Role != AppRoles.Admin)
            result.AddError(nameof(model.Role), "You cannot remove your own Admin role.");
        if (isSelf && !model.IsActive)
            result.AddError(nameof(model.IsActive), "You cannot deactivate your own account.");
        if (!result.Succeeded) return result;

        var oldRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
        var oldActive = user.IsActive;
        var oldName = user.FullName;

        // Name + active status
        user.FullName = model.FullName.Trim();
        user.IsActive = model.IsActive;
        await _userManager.UpdateAsync(user);

        // Exactly one role (4.7)
        if (oldRole != model.Role)
        {
            var current = await _userManager.GetRolesAsync(user);
            if (current.Any()) await _userManager.RemoveFromRolesAsync(user, current);
            await _userManager.AddToRoleAsync(user, model.Role);

            await _audit.LogAsync(AuditActions.RoleChange, "User", user.Id,
                oldValue: new { Role = oldRole }, newValue: new { Role = model.Role });
        }

        if (oldActive != model.IsActive)
        {
            await _audit.LogAsync(AuditActions.UserStatusChange, "User", user.Id,
                oldValue: new { IsActive = oldActive }, newValue: new { IsActive = model.IsActive });
        }

        if (oldName != user.FullName)
        {
            await _audit.LogAsync(AuditActions.Update, "User", user.Id,
                oldValue: new { FullName = oldName }, newValue: new { user.FullName });
        }

        // Role or status changed -> invalidate the user's existing login cookie
        if (oldRole != model.Role || oldActive != model.IsActive)
            await _userManager.UpdateSecurityStampAsync(user);

        return ServiceResult.Ok();
    }

    // ===================== UNLOCK (6.3) =====================
    public async Task<ServiceResult> UnlockAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return ServiceResult.NotFoundResult();

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        await _audit.LogAsync(AuditActions.Unlock, "User", user.Id);
        return ServiceResult.Ok();
    }

    // ===================== RESET PASSWORD (4.7) =====================
    public async Task<ServiceResult> ResetPasswordAsync(UserResetPasswordViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null) return ServiceResult.NotFoundResult();

        var result = new ServiceResult();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (!reset.Succeeded)
        {
            foreach (var e in reset.Errors) result.AddError(nameof(model.NewPassword), e.Description);
            return result;
        }

        // A reset also clears any lockout
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        // The password itself is NEVER logged
        await _audit.LogAsync(AuditActions.PasswordReset, "User", user.Id);
        return ServiceResult.Ok();
    }
}