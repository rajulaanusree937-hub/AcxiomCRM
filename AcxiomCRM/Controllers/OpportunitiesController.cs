using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    [Authorize(Policy = AppPolicies.AllCrmUsers)]
    public class OpportunitiesController : Controller
    {
        private readonly IOpportunityService _opportunities;
        private readonly ILookupService _lookup;
        private readonly ICurrentUserService _currentUser;

        public OpportunitiesController(IOpportunityService opportunities, ILookupService lookup, ICurrentUserService currentUser)
        {
            _opportunities = opportunities;
            _lookup = lookup;
            _currentUser = currentUser;
        }

        // GET /Opportunities?search=&customerId=&stage=&status=&assignedTo=&sortBy=&page=
        public async Task<IActionResult> Index([FromQuery] OpportunityListViewModel filter)
        {
            ViewData["Title"] = "Opportunities";
            var model = await _opportunities.GetListAsync(filter);
            model.CustomerOptions = await _lookup.CustomerOptionsAsync(model.CustomerId);
            if (_currentUser.CanSeeAllRecords)
                model.AssignedUsers = await _lookup.SalesExecutiveOptionsAsync(model.AssignedTo);
            return View(model);
        }

        // GET /Opportunities/Pipeline  (17.2 "Sales Pipeline")
        public async Task<IActionResult> Pipeline()
        {
            ViewData["Title"] = "Sales Pipeline";
            return View(await _opportunities.GetPipelineAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var opp = await _opportunities.FindWithRelatedAsync(id);
            if (opp == null) return NotFound();
            if (!_currentUser.CanAccess(opp)) return Forbid();

            ViewData["Title"] = opp.OpportunityName;
            var names = await _lookup.UserNamesAsync(new[] { opp.AssignedTo });
            ViewBag.AssignedToName = opp.AssignedTo != null && names.TryGetValue(opp.AssignedTo, out var n) ? n : null;
            return View(opp);
        }

        // ---------- CREATE (can be pre-filled from a customer page) ----------
        public async Task<IActionResult> Create(int? customerId)
        {
            ViewData["Title"] = "New Opportunity";
            var model = new OpportunityFormViewModel
            {
                CustomerId = customerId,
                ExpectedCloseDate = DateTime.Today.AddDays(30)
            };
            await LoadDropdownsAsync(model, null);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OpportunityFormViewModel model)
        {
            ViewData["Title"] = "New Opportunity";
            if (!ModelState.IsValid) { await LoadDropdownsAsync(model, null); return View(model); }

            var result = await _opportunities.CreateAsync(model);
            if (!result.Succeeded) { AddErrors(result); await LoadDropdownsAsync(model, null); return View(model); }

            TempData["Success"] = $"Opportunity '{model.OpportunityName}' was created.";
            return RedirectToAction(nameof(Details), new { id = result.Id });
        }

        // ---------- EDIT / MOVE STAGE ----------
        public async Task<IActionResult> Edit(int id)
        {
            var opp = await _opportunities.FindAsync(id);
            if (opp == null) return NotFound();
            if (!_currentUser.CanAccess(opp)) return Forbid();

            if (opp.Status == OpportunityStatus.Closed)
            {
                TempData["Warning"] = $"This opportunity is {opp.Stage} and can no longer be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewData["Title"] = "Edit Opportunity";
            var model = _opportunities.ToFormModel(opp);
            await LoadDropdownsAsync(model, opp.Stage);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OpportunityFormViewModel model)
        {
            if (id != model.OpportunityId) return BadRequest();
            ViewData["Title"] = "Edit Opportunity";

            var existing = await _opportunities.FindAsync(id);
            if (existing == null) return NotFound();
            if (!_currentUser.CanAccess(existing)) return Forbid();

            if (!ModelState.IsValid) { await LoadDropdownsAsync(model, existing.Stage); return View(model); }

            var result = await _opportunities.UpdateAsync(model);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();
            if (!result.Succeeded) { AddErrors(result); await LoadDropdownsAsync(model, existing.Stage); return View(model); }

            TempData["Success"] = $"Opportunity '{model.OpportunityName}' was updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- QUICK ACTION: Mark Won / Lost ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id, bool won)
        {
            var result = await _opportunities.CloseAsync(id, won);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();

            if (result.Succeeded) TempData["Success"] = won ? "Opportunity marked as Won 🎉" : "Opportunity marked as Lost.";
            else TempData["Error"] = result.Errors.First().Message;

            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- DELETE ----------
        public async Task<IActionResult> Delete(int id)
        {
            var opp = await _opportunities.FindWithRelatedAsync(id);
            if (opp == null) return NotFound();
            if (!_currentUser.CanAccess(opp)) return Forbid();

            ViewData["Title"] = "Delete Opportunity";
            return View(opp);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _opportunities.DeleteAsync(id);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();
            if (!result.Succeeded)
            {
                TempData["Error"] = result.Errors.First().Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] = "Opportunity was deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- helpers ----------
        private async Task LoadDropdownsAsync(OpportunityFormViewModel model, OpportunityStage? current)
        {
            model.Customers = await _lookup.CustomerOptionsAsync(model.CustomerId);
            model.Leads = await _lookup.LeadOptionsAsync(model.LeadId);
            model.AllowedStages = _opportunities.GetAllowedStageOptions(current);
            if (_currentUser.CanSeeAllRecords)
                model.SalesExecutives = await _lookup.SalesExecutiveOptionsAsync(model.AssignedTo);
        }

        private void AddErrors(ServiceResult result)
        {
            foreach (var (field, message) in result.Errors)
                ModelState.AddModelError(field, message);
        }
    }
}