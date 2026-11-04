using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    [Authorize(Policy = AppPolicies.AllCrmUsers)]
    public class ActivitiesController : Controller
    {
        private readonly IActivityService _activities;
        private readonly ILookupService _lookup;
        private readonly ICurrentUserService _currentUser;

        public ActivitiesController(IActivityService activities, ILookupService lookup, ICurrentUserService currentUser)
        {
            _activities = activities;
            _lookup = lookup;
            _currentUser = currentUser;
        }

        public async Task<IActionResult> Index([FromQuery] ActivityListViewModel filter)
        {
            ViewData["Title"] = "Activities";
            var model = await _activities.GetListAsync(filter);
            if (_currentUser.CanSeeAllRecords)
                model.AssignedUsers = await _lookup.SalesExecutiveOptionsAsync(model.AssignedTo);
            return View(model);
        }

        public async Task<IActionResult> Details(int id)

        {
            var activity = await _activities.FindWithRelatedAsync(id);
            if (activity == null) return NotFound();
            if (!_currentUser.CanAccess(activity)) return Forbid();

            ViewData["Title"] = activity.Subject;
            var names = await _lookup.UserNamesAsync(new[] { activity.AssignedTo });
            ViewBag.AssignedToName = activity.AssignedTo != null && names.TryGetValue(activity.AssignedTo, out var n) ? n : null;
            return View(activity);
        }

        public async Task<IActionResult> Create(int? customerId, int? leadId)
        {
            ViewData["Title"] = "Log Activity";
            var model = new ActivityFormViewModel { CustomerId = customerId, LeadId = leadId, ActivityDate = DateTime.Now };
            await LoadDropdownsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ActivityFormViewModel model)
        {
            ViewData["Title"] = "Log Activity";
            if (!ModelState.IsValid) { await LoadDropdownsAsync(model); return View(model); }

            var result = await _activities.CreateAsync(model);
            if (!result.Succeeded) { AddErrors(result); await LoadDropdownsAsync(model); return View(model); }

            TempData["Success"] = $"{model.ActivityType} activity was logged.";
            return RedirectToAction(nameof(Details), new { id = result.Id });
        }


        public async Task<IActionResult> Edit(int id)
        {
            var activity = await _activities.FindAsync(id);
            if (activity == null) return NotFound();
            if (!_currentUser.CanAccess(activity)) return Forbid();

            ViewData["Title"] = "Edit Activity";
            var model = _activities.ToFormModel(activity);
            await LoadDropdownsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ActivityFormViewModel model)
        {
            if (id != model.ActivityId) return BadRequest();
            ViewData["Title"] = "Edit Activity";
            if (!ModelState.IsValid) { await LoadDropdownsAsync(model); return View(model); }

            var result = await _activities.UpdateAsync(model);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();
            if (!result.Succeeded) { AddErrors(result); await LoadDropdownsAsync(model); return View(model); }

            TempData["Success"] = "Activity was updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Delete(int id)
        {

            var activity = await _activities.FindWithRelatedAsync(id);
            if (activity == null) return NotFound();
            if (!_currentUser.CanAccess(activity)) return Forbid();

            ViewData["Title"] = "Delete Activity";
            return View(activity);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _activities.DeleteAsync(id);
            if (result.NotFound) return NotFound();
            if (result.Forbidden) return Forbid();

            TempData["Success"] = "Activity was deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadDropdownsAsync(ActivityFormViewModel model)
        {
            model.Customers = await _lookup.CustomerOptionsAsync(model.CustomerId);
            model.Leads = await _lookup.LeadOptionsAsync(model.LeadId);
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
