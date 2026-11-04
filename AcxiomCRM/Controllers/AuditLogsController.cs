using System.Text.Json;
using AcxiomCRM.Data;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    // Admin only (7.1: Audit Log = Admin Full, others No). Read-only: no create/edit/delete actions exist.
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AuditLogsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /AuditLogs?userId=&entityName=&action=&dateFrom=&dateTo=&page=
        public async Task<IActionResult> Index([FromQuery] AuditLogListViewModel filter)
        {
            ViewData["Title"] = "Audit Log";

            var query = _db.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(filter.UserId)) query = query.Where(a => a.UserId == filter.UserId);
            if (!string.IsNullOrEmpty(filter.EntityName)) query = query.Where(a => a.EntityName == filter.EntityName);
            if (!string.IsNullOrEmpty(filter.Action)) query = query.Where(a => a.Action == filter.Action);

            if (filter.DateFrom.HasValue) query = query.Where(a => a.CreatedDate >= filter.DateFrom.Value.Date.ToUniversalTime());
            if (filter.DateTo.HasValue) query = query.Where(a => a.CreatedDate < filter.DateTo.Value.Date.AddDays(1).ToUniversalTime());

            filter.PageSize = Math.Clamp(filter.PageSize, 10, 100);
            filter.TotalCount = await query.CountAsync();
            filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, filter.TotalPages));

            var rows = await query
                .OrderByDescending(a => a.AuditLogId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var userIds = rows.Where(r => r.UserId != null).Select(r => r.UserId!).Distinct().ToList();
            var users = await _db.Users.Where(u => userIds.Contains(u.Id))
                                       .ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.Email });

            filter.Items = rows.Select(r => new AuditLogListItem
            {
                AuditLogId = r.AuditLogId,
                CreatedDate = r.CreatedDate,
                UserName = r.UserId != null && users.TryGetValue(r.UserId, out var u) ? u.FullName : null,
                UserEmail = r.UserId != null && users.TryGetValue(r.UserId, out var u2) ? u2.Email : null,
                Action = r.Action,
                EntityName = r.EntityName,
                RecordId = r.RecordId,
                OldValue = Pretty(r.OldValue),
                NewValue = Pretty(r.NewValue),
                IpAddress = r.IpAddress
            }).ToList();

            // Filter dropdowns

            filter.Users = await _db.Users.OrderBy(u => u.FullName)
                .Select(u => new SelectListItem(u.FullName + " (" + u.Email + ")", u.Id))
                .ToListAsync();
            filter.Modules = await _db.AuditLogs.Select(a => a.EntityName).Distinct().OrderBy(n => n).ToListAsync();
            filter.Actions = AuditActions.All;

            return View(filter);
        }

        // Indent JSON so old/new values are readable
        private static string? Pretty(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (JsonException)
            {
                return json;
            }
        }
    }
}