using System.Globalization;
using System.Text;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    [Authorize(Policy = AppPolicies.AllCrmUsers)]
    public class ReportsController : Controller
    {
        private readonly IReportService _reports;

        public ReportsController(IReportService reports)
        {
            _reports = reports;
        }

        // GET /Reports — list of reports this user may open
        public IActionResult Index()
        {
            ViewData["Title"] = "Reports";
            return View(_reports.AvailableReports());
        }

        // GET /Reports/Show/leads?dateFrom=&dateTo=&owner=&status=&search=&sortBy=&desc=&page=
        public async Task<IActionResult> Show(string id, [FromQuery] ReportViewModel filter)
        {
            var def = _reports.Find(id);
            if (def == null) return NotFound();
            if (!_reports.CanView(def)) return Forbid();                       // server-side role check (Section 11)

            if (def.ExternalController != null) return RedirectToAction("Index", def.ExternalController);

            filter.Key = id;
            ViewData["Title"] = def.Title;
            return View(await _reports.BuildAsync(filter));
        }

        // GET /Reports/Export/leads?... — same filters, all rows, CSV file (4.11 "export")
        public async Task<IActionResult> Export(string id, [FromQuery] ReportViewModel filter)
        {
            var def = _reports.Find(id);
            if (def == null || def.ExternalController != null) return NotFound();
            if (!_reports.CanView(def)) return Forbid();

            filter.Key = id;
            var report = await _reports.BuildAsync(filter, paged: false);

            var csv = new StringBuilder();
            csv.AppendLine(string.Join(",", report.Columns.Select(c => Csv(c.Header))));
            foreach (var row in report.Rows)
                csv.AppendLine(string.Join(",", row.Select(v => Csv(Format(v)))));

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"{id}-report-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        private static string Format(object? v) => v switch
        {
            null => "",
            DateTime d => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            decimal m => m.ToString("0.##", CultureInfo.InvariantCulture),
            _ => v.ToString() ?? ""

        };

        // Quote values, and neutralise spreadsheet formulas (CSV injection protection)
        private static string Csv(string value)
        {
            if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}