using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    [Authorize(Policy = AppPolicies.AllCrmUsers)]
    public class FollowUpsController : Controller
    {
        private readonly IFollowUpService _followUps;
        private readonly ILookupService _lookup;
        private readonly ICurrentUserService _currentUser;

        public FollowUpsController(IFollowUpService followUps, ILookupService lookup, ICurrentUserService currentUser)
        {
            _followUps = followUps;
            _lookup = lookup;
            _currentUser = currentUser;
        }

        // GET /FollowUps?view=overdue&status=&assignedTo=&customerId=&leadId=&dateFrom=&dateTo=&page=
        public async Task<IActionResult> Index([FromQuery] FollowUpListViewModel filter)
        {
            ViewData["Title"] = "Follow-Ups";
            var model = await _followUps.GetListAsync(filter);

            model.CustomerOptions = await _lookup.CustomerOptionsAsync(model.CustomerId);
            model.LeadOptions = await _lookup.LeadOptionsAsync(model.LeadId);
            if (_currentUser.CanSeeAllRecords)
                model.AssignedUsers = await _lookup.SalesExecutiveOptionsAsync(model.AssignedTo);


            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var followUp = await _followUps.FindWithRelatedAsync(id);
            if (followUp == null) return NotFound();
            if (!_currentUser.CanAccess(followUp)) return Forbid();

            ViewData["Title"] = followUp.Subject;
            var names = await _lookup.UserNamesAsync(new[] { followUp.AssignedTo });
            ViewBag.AssignedToName = followUp.AssignedTo != null && names.TryGetValue(followUp.AssignedTo, out var n) ? n : null;
            return View(followUp);
        }

        // ---------- CREATE (can be pre-filled from a customer / lead / opportunity page) ----------
        public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
        {
            ViewData["Title"] = "Schedule Follow-Up";
            var model = new FollowUpFormViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                OpportunityId = opportunityId,
                FollowUpDate = DateTime.Today.AddDays(1).AddHours(10)   // tomorrow 10:00 AM
            };
            await LoadDropdownsAsync(model, null);
            return View(model);
        }

        [HttpPost]

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FollowUpFormViewModel model)
        {
            ViewData["Title"] = "Schedule Follow-Up";
            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(model, null);
                return View(model);
            }

            var result = await _followUps.CreateAsync(model);
            if (!result.Succeeded)
            {
                AddErrors(result);
                await LoadDropdownsAsync(model, null);
                return View(model);
            }

            TempData["Success"] = "Follow-up was scheduled.";
            return RedirectToAction(nameof(Details), new { id = result.Id });
        }

        // ---------- EDIT / RESCHEDULE ----------
        public async Task<IActionResult> Edit(int id)
        {
            var followUp = await _followUps.FindAsync(id);
            if (followUp == null) return NotFound();
            if (!_currentUser.CanAccess(followUp)) return Forbid();

            if (followUp.Status is FollowUpStatus.Completed or FollowUpStatus.Cancelled)
            {
                TempData["Warning"] = "Completed or cancelled follow-ups cannot be edited.";

                return RedirectToAction(nameof(Details), new { id });
            }

            ViewData["Title"] = "Edit Follow-Up";
            var model = _followUps.ToFormModel(followUp);
            await LoadDropdownsAsync(model, followUp.Status);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FollowUpFormViewModel model)
        {
            if (id != model.FollowUpId) return BadRequest();
            ViewData["Title"] = "Edit Follow-Up";

            var existing = await _followUps.FindAsync(id);
            if (existing == null) return NotFound();
            if (!_currentUser.CanAccess(existing)) return Forbid();

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(model, existing.Status);
                return View(model);
            }

            var result = await _followUps.UpdateAsync(model);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();
            if (!result.Succeeded)
            {
                AddErrors(result);

                await LoadDropdownsAsync(model, existing.Status);
                return View(model);
            }

            TempData["Success"] = "Follow-up was updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- QUICK ACTION: Mark Completed ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, string? returnUrl = null)
        {
            var result = await _followUps.CompleteAsync(id);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();

            if (result.Succeeded) TempData["Success"] = "Follow-up marked as completed.";
            else TempData["Error"] = result.Errors.First().Message;

            // Only redirect to our own pages (blocks open-redirect attacks)
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- DELETE ----------
        public async Task<IActionResult> Delete(int id)
        {
            var followUp = await _followUps.FindWithRelatedAsync(id);
            if (followUp == null) return NotFound();
            if (!_currentUser.CanAccess(followUp)) return Forbid();


            ViewData["Title"] = "Delete Follow-Up";
            return View(followUp);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _followUps.DeleteAsync(id);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();

            TempData["Success"] = "Follow-up was deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- helpers ----------
        private async Task LoadDropdownsAsync(FollowUpFormViewModel model, FollowUpStatus? current)
        {
            model.Customers = await _lookup.CustomerOptionsAsync(model.CustomerId);
            model.Leads = await _lookup.LeadOptionsAsync(model.LeadId);
            model.Opportunities = await _lookup.OpportunityOptionsAsync(model.OpportunityId);
            model.AllowedStatuses = _followUps.GetAllowedStatusOptions(current);
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