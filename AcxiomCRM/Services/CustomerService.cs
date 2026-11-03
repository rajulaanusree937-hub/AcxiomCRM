using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface ICustomerService
{
    Task<CustomerListViewModel> GetListAsync(CustomerListViewModel filter);
    Task<Customer?> FindAsync(int id);
    Task<Customer?> FindWithHistoryAsync(int id);
    CustomerFormViewModel ToFormModel(Customer customer);
    Task<ServiceResult> CreateAsync(CustomerFormViewModel model);
    Task<ServiceResult> UpdateAsync(CustomerFormViewModel model);
    Task<ServiceResult> DeleteAsync(int id);
    Task<List<SelectListItem>> GetSalesExecutiveOptionsAsync(string? selectedId = null);
    Task<string?> GetUserNameAsync(string? userId);
}

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;

    public CustomerService(ApplicationDbContext db, ICurrentUserService currentUser,
                           UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    // ===================== LIST + SEARCH + FILTER + SORT + PAGING (17.13, 12.0) =====================
    public async Task<CustomerListViewModel> GetListAsync(CustomerListViewModel filter)
    {
        // Ownership first: SalesExecutive only gets their own rows (7.1)
        var query = _db.Customers.AsNoTracking().VisibleTo(_currentUser);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(c =>
                c.CustomerName.Contains(s) ||
                c.Email.Contains(s) ||
                c.Phone.Contains(s) ||
                c.CustomerCode.Contains(s) ||
                (c.CompanyName != null && c.CompanyName.Contains(s)));
        }

        if (filter.Status.HasValue)
            query = query.Where(c => c.Status == filter.Status.Value);

        query = filter.SortBy switch
        {
            "oldest" => query.OrderBy(c => c.CreatedDate),
            "name" => query.OrderBy(c => c.CustomerName),
            "name_desc" => query.OrderByDescending(c => c.CustomerName),
            _ => query.OrderByDescending(c => c.CreatedDate)   // newest
        };

        filter.PageSize = Math.Clamp(filter.PageSize, 5, 50);
        filter.TotalCount = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, filter.TotalPages));

        filter.Items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(c => new CustomerListItem
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                City = c.City,
                Status = c.Status,
                CreatedDate = c.CreatedDate,
                AssignedToName = c.AssignedTo   // replaced with the real name below
            })
            .ToListAsync();

        // Show names instead of user IDs
        var ids = filter.Items.Where(i => i.AssignedToName != null)
                              .Select(i => i.AssignedToName!).Distinct().ToList();
        var names = await _db.Users.Where(u => ids.Contains(u.Id))
                                   .ToDictionaryAsync(u => u.Id, u => u.FullName);
        foreach (var item in filter.Items)
            item.AssignedToName = item.AssignedToName != null && names.TryGetValue(item.AssignedToName, out var n)
                ? n : null;

        return filter;
    }

    // ===================== READ =====================
    public Task<Customer?> FindAsync(int id) =>
        _db.Customers.FirstOrDefaultAsync(c => c.CustomerId == id);

    // Details page: customer + its history (4.4 "customer history and related activities")
    public Task<Customer?> FindWithHistoryAsync(int id) =>
        _db.Customers.AsNoTracking()
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

    public CustomerFormViewModel ToFormModel(Customer c) => new()
    {
        CustomerId = c.CustomerId,
        CustomerCode = c.CustomerCode,
        CustomerName = c.CustomerName,
        Email = c.Email,
        Phone = c.Phone,
        CompanyName = c.CompanyName,
        Address = c.Address,
        City = c.City,
        State = c.State,
        Status = c.Status,
        Notes = c.Notes,
        AssignedTo = c.AssignedTo
    };

    // ===================== CREATE =====================
    public async Task<ServiceResult> CreateAsync(CustomerFormViewModel model)
    {
        Normalize(model);

        var result = await ValidateBusinessRulesAsync(model);
        var assignedTo = await ResolveAssignedToAsync(model.AssignedTo, currentValue: null, result);
        if (!result.Succeeded) return result;

        var customer = new Customer
        {
            CustomerCode = await GenerateCustomerCodeAsync(),
            CustomerName = model.CustomerName,
            Email = model.Email,
            Phone = model.Phone,
            CompanyName = model.CompanyName,
            Address = model.Address,
            City = model.City,
            State = model.State,
            Status = model.Status,
            Notes = model.Notes,
            AssignedTo = assignedTo,
            CreatedBy = _currentUser.UserId ?? string.Empty,
            CreatedDate = DateTime.UtcNow
        };

        _db.Customers.Add(customer);
        if (!await TrySaveAsync(result)) return result;

        return ServiceResult.Ok(customer.CustomerId);
    }

    // ===================== UPDATE =====================
    public async Task<ServiceResult> UpdateAsync(CustomerFormViewModel model)
    {
        var customer = await FindAsync(model.CustomerId);
        if (customer == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(customer)) return ServiceResult.ForbiddenResult();

        Normalize(model);

        var result = await ValidateBusinessRulesAsync(model);
        var assignedTo = await ResolveAssignedToAsync(model.AssignedTo, customer.AssignedTo, result);
        if (!result.Succeeded) return result;

        customer.CustomerName = model.CustomerName;
        customer.Email = model.Email;
        customer.Phone = model.Phone;
        customer.CompanyName = model.CompanyName;
        customer.Address = model.Address;
        customer.City = model.City;
        customer.State = model.State;
        customer.Status = model.Status;
        customer.Notes = model.Notes;
        customer.AssignedTo = assignedTo;
        customer.ModifiedDate = DateTime.UtcNow;
        // CustomerCode, CreatedBy, CreatedDate are never changed from the form

        if (!await TrySaveAsync(result)) return result;
        return ServiceResult.Ok(customer.CustomerId);
    }

    // ===================== DELETE =====================
    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var customer = await FindAsync(id);
        if (customer == null) return ServiceResult.NotFoundResult();
        if (!_currentUser.CanAccess(customer)) return ServiceResult.ForbiddenResult();

        // Protect history: a customer with related records is deactivated, not deleted
        var hasRelated =
            await _db.Opportunities.AnyAsync(o => o.CustomerId == id) ||
            await _db.FollowUps.AnyAsync(f => f.CustomerId == id) ||
            await _db.Activities.AnyAsync(a => a.CustomerId == id);

        if (hasRelated)
            return new ServiceResult().AddError("",
                "This customer has related opportunities, follow-ups or activities and cannot be deleted. " +
                "Set the status to Inactive instead.");

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok(id);
    }

    // ===================== DROPDOWN + NAMES =====================
    public async Task<List<SelectListItem>> GetSalesExecutiveOptionsAsync(string? selectedId = null)
    {
        var users = await _userManager.GetUsersInRoleAsync(AppRoles.SalesExecutive);
        return users.Where(u => u.IsActive)
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem($"{u.FullName} ({u.Email})", u.Id, u.Id == selectedId))
                    .ToList();
    }

    public async Task<string?> GetUserNameAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;
        return await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync();
    }

    // ===================== BUSINESS RULES (17.5, 17.7) =====================
    private static void Normalize(CustomerFormViewModel m)
    {
        m.CustomerName = m.CustomerName.Trim();
        m.Email = m.Email.Trim().ToLowerInvariant();
        m.Phone = m.Phone.Trim();
        m.CompanyName = string.IsNullOrWhiteSpace(m.CompanyName) ? null : m.CompanyName.Trim();
    }

    private async Task<ServiceResult> ValidateBusinessRulesAsync(CustomerFormViewModel m)
    {
        var result = new ServiceResult();

        // Email uniqueness (17.5)
        if (await _db.Customers.AnyAsync(c => c.Email == m.Email && c.CustomerId != m.CustomerId))
            result.AddError(nameof(m.Email), "A customer with this email already exists.");

        // Phone uniqueness (17.5)
        if (await _db.Customers.AnyAsync(c => c.Phone == m.Phone && c.CustomerId != m.CustomerId))
            result.AddError(nameof(m.Phone), "A customer with this phone number already exists.");

        // Duplicate customer: same name at the same company (17.7)
        if (m.CompanyName != null &&
            await _db.Customers.AnyAsync(c => c.CustomerName == m.CustomerName &&
                                              c.CompanyName == m.CompanyName &&
                                              c.CustomerId != m.CustomerId))
            result.AddError("", "This customer already exists for this company.");

        return result;
    }

    // SalesExecutive: always themselves (can't reassign). Admin/Manager: must pick a real, active Sales Executive.
    private async Task<string?> ResolveAssignedToAsync(string? requested, string? currentValue, ServiceResult result)
    {
        if (_currentUser.IsSalesExecutive && !_currentUser.CanSeeAllRecords)
            return currentValue ?? _currentUser.UserId;

        if (string.IsNullOrEmpty(requested))
            return null;   // unassigned is allowed for Admin/Manager

        var user = await _userManager.FindByIdAsync(requested);
        if (user == null || !user.IsActive || !await _userManager.IsInRoleAsync(user, AppRoles.SalesExecutive))
        {
            result.AddError(nameof(CustomerFormViewModel.AssignedTo), "Select a valid sales executive.");
            return null;
        }
        return requested;
    }

    private async Task<string> GenerateCustomerCodeAsync()
    {
        var next = (await _db.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 0) + 1;
        string code;
        do { code = $"CUS-{next++:D5}"; }
        while (await _db.Customers.AnyAsync(c => c.CustomerCode == code));
        return code;
    }

    // Last line of defence: the unique indexes in the database (17.5)
    private async Task<bool> TrySaveAsync(ServiceResult result)
    {
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            result.AddError("", "A customer with the same email, phone or code already exists.");
            return false;
        }
    }
}
