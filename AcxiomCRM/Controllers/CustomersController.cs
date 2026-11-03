using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    // All three roles may use Customers; WHICH customers is decided by ownership (7.1)
    [Authorize(Policy = AppPolicies.AllCrmUsers)]
    public class CustomersController : Controller
    {
        private readonly ICustomerService _customers;
        private readonly ICurrentUserService _currentUser;

        public CustomersController(ICustomerService customers, ICurrentUserService currentUser)
        {
            _customers = customers;
            _currentUser = currentUser;
        }

        // GET /Customers?search=ravi&status=Active&sortBy=name&page=2
        public async Task<IActionResult> Index([FromQuery] CustomerListViewModel filter)
        {
            ViewData["Title"] = "Customers";
            return View(await _customers.GetListAsync(filter));
        }

        // GET /Customers/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var customer = await _customers.FindWithHistoryAsync(id);
            if (customer == null) return NotFound();

            if (!_currentUser.CanAccess(customer)) return Forbid();   // URL tampering blocked

            ViewData["Title"] = customer.CustomerName;
            ViewBag.AssignedToName = await _customers.GetUserNameAsync(customer.AssignedTo);
            return View(customer);
        }

        // GET /Customers/Create
        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "New Customer";
            var model = new CustomerFormViewModel();
            await LoadDropdownAsync(model);
            return View(model);
        }

        // POST /Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]                       // 17.9
        public async Task<IActionResult> Create(CustomerFormViewModel model)
        {
            ViewData["Title"] = "New Customer";

            // Server-side validation — catches anything the browser was tricked into sending (17.8)
            if (!ModelState.IsValid)
            {
                await LoadDropdownAsync(model);
                return View(model);
            }

            var result = await _customers.CreateAsync(model);
            if (!result.Succeeded)

            {
                AddErrors(result);
                await LoadDropdownAsync(model);
                return View(model);
            }

            TempData["Success"] = $"Customer '{model.CustomerName}' was created.";
            return RedirectToAction(nameof(Details), new { id = result.Id });
        }

        // GET /Customers/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _customers.FindAsync(id);
            if (customer == null) return NotFound();
            if (!_currentUser.CanAccess(customer)) return Forbid();

            ViewData["Title"] = "Edit Customer";
            var model = _customers.ToFormModel(customer);
            await LoadDropdownAsync(model);
            return View(model);
        }

        // POST /Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CustomerFormViewModel model)
        {
            if (id != model.CustomerId) return BadRequest();
            ViewData["Title"] = "Edit Customer";

            if (!ModelState.IsValid)

            {
                await LoadDropdownAsync(model);
                return View(model);
            }

            var result = await _customers.UpdateAsync(model);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();
            if (!result.Succeeded)
            {
                AddErrors(result);
                await LoadDropdownAsync(model);
                return View(model);
            }

            TempData["Success"] = $"Customer '{model.CustomerName}' was updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET /Customers/Delete/5  (confirmation page)
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _customers.FindAsync(id);
            if (customer == null) return NotFound();
            if (!_currentUser.CanAccess(customer)) return Forbid();

            ViewData["Title"] = "Delete Customer";
            ViewBag.AssignedToName = await _customers.GetUserNameAsync(customer.AssignedTo);
            return View(customer);
        }

        // POST /Customers/Delete/5

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _customers.DeleteAsync(id);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Errors.First().Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] = "Customer was deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- helpers ----------
        private async Task LoadDropdownAsync(CustomerFormViewModel model)
        {
            // Only Admin/Manager choose the assignee; SalesExecutive is auto-assigned to themselves
            if (_currentUser.CanSeeAllRecords)
                model.SalesExecutives = await _customers.GetSalesExecutiveOptionsAsync(model.AssignedTo);
        }

        private void AddErrors(ServiceResult result)
        {
            foreach (var (field, message) in result.Errors)
                ModelState.AddModelError(field, message);
        }
    }

}