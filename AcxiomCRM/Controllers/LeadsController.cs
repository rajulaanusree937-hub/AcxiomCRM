using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    [Authorize(Policy = AppPolicies.AllCrmUsers)]
    public class LeadsController : Controller
    {
        private readonly ILeadService _leads;
        private readonly ICustomerService _customers;   // reused for sales-exec dropdown + names
        private readonly ICurrentUserService _currentUser;

        public LeadsController(ILeadService leads, ICustomerService customers, ICurrentUserService currentUser)
        {
            _leads = leads;
            _customers = customers;
            _currentUser = currentUser;
        }

        // GET /Leads?search=&status=&assignedTo=&sortBy=&page=
        public async Task<IActionResult> Index([FromQuery] LeadListViewModel filter)
        {
            ViewData["Title"] = "Leads";
            var model = await _leads.GetListAsync(filter);
            if (_currentUser.CanSeeAllRecords)
                model.AssignedUsers = await _customers.GetSalesExecutiveOptionsAsync(model.AssignedTo);
            return View(model);
        }


        // GET /Leads/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var lead = await _leads.FindWithHistoryAsync(id);
            if (lead == null) return NotFound();
            if (!_currentUser.CanAccess(lead)) return Forbid();

            ViewData["Title"] = lead.LeadName;
            ViewBag.AssignedToName = await _customers.GetUserNameAsync(lead.AssignedTo);
            return View(lead);
        }

        // ---------- CREATE ----------
        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "New Lead";
            var model = new LeadFormViewModel();
            await LoadDropdownsAsync(model, current: null);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeadFormViewModel model)
        {
            ViewData["Title"] = "New Lead";
            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(model, null);
                return View(model);
            }


            var result = await _leads.CreateAsync(model);
            if (!result.Succeeded)
            {
                AddErrors(result);
                await LoadDropdownsAsync(model, null);
                return View(model);
            }

            TempData["Success"] = $"Lead '{model.LeadName}' was created.";
            return RedirectToAction(nameof(Details), new { id = result.Id });
        }

        // ---------- EDIT ----------
        public async Task<IActionResult> Edit(int id)
        {
            var lead = await _leads.FindAsync(id);
            if (lead == null) return NotFound();
            if (!_currentUser.CanAccess(lead)) return Forbid();

            if (lead.Status == LeadStatus.Converted)
            {
                TempData["Warning"] = "Converted leads cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewData["Title"] = "Edit Lead";
            var model = _leads.ToFormModel(lead);
            await LoadDropdownsAsync(model, lead.Status);
            return View(model);
        }

        [HttpPost]

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LeadFormViewModel model)
        {
            if (id != model.LeadId) return BadRequest();
            ViewData["Title"] = "Edit Lead";

            var existing = await _leads.FindAsync(id);
            if (existing == null) return NotFound();
            if (!_currentUser.CanAccess(existing)) return Forbid();

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(model, existing.Status);
                return View(model);
            }

            var result = await _leads.UpdateAsync(model);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();
            if (!result.Succeeded)
            {
                AddErrors(result);
                await LoadDropdownsAsync(model, existing.Status);
                return View(model);
            }

            TempData["Success"] = $"Lead '{model.LeadName}' was updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- DELETE ----------
        public async Task<IActionResult> Delete(int id)

        {
            var lead = await _leads.FindAsync(id);
            if (lead == null) return NotFound();
            if (!_currentUser.CanAccess(lead)) return Forbid();

            ViewData["Title"] = "Delete Lead";
            ViewBag.AssignedToName = await _customers.GetUserNameAsync(lead.AssignedTo);
            return View(lead);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _leads.DeleteAsync(id);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();
            if (!result.Succeeded)
            {
                TempData["Error"] = result.Errors.First().Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] = "Lead was deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- CONVERT (4.5, Section 8) ----------
        public async Task<IActionResult> Convert(int id)
        {
            var lead = await _leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!_currentUser.CanAccess(lead)) return Forbid();

            if (lead.Status != LeadStatus.Qualified)
            {
                TempData["Warning"] = "Only Qualified leads can be converted.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewData["Title"] = "Convert Lead";
            return View(_leads.ToConvertModel(lead));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(int id, LeadConvertViewModel model)
        {
            if (id != model.LeadId) return BadRequest();
            ViewData["Title"] = "Convert Lead";

            if (!ModelState.IsValid) return View(model);

            var result = await _leads.ConvertAsync(model);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();
            if (!result.Succeeded)
            {
                AddErrors(result);
                return View(model);
            }

            TempData["Success"] = model.CreateOpportunity
                ? "Lead converted to a new customer and opportunity."

                : "Lead converted to a new customer.";
            return RedirectToAction("Details", "Customers", new { id = result.Id });
        }

        // ---------- helpers ----------
        private async Task LoadDropdownsAsync(LeadFormViewModel model, LeadStatus? current)
        {
            model.AllowedStatuses = _leads.GetAllowedStatusOptions(current);
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