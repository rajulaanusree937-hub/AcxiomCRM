using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api
{
    [Route("api/leads")]
    public class LeadsApiController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILeadService _leads;
        private readonly ICurrentUserService _currentUser;

        public LeadsApiController(ApplicationDbContext db, ILeadService leads, ICurrentUserService currentUser)
        {
            _db = db;
            _leads = leads;
            _currentUser = currentUser;
        }

        /// <summary>List / search leads visible to the caller.</summary>
        [HttpGet]
        [ProducesResponseType<PagedResponse<LeadDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] LeadStatus? status,
                                                [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);

            var query = _db.Leads.AsNoTracking().VisibleTo(_currentUser);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(l => l.LeadName.Contains(s) || (l.CompanyName != null && l.CompanyName.Contains(s)));
            }
            if (status.HasValue) query = query.Where(l => l.Status == status.Value);

            var total = await query.CountAsync();
            var items = await query.OrderByDescending(l => l.CreatedDate)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(l => ToDto(l))
                .ToListAsync();

            return Ok(new PagedResponse<LeadDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
        }

        /// <summary>Get one lead.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType<LeadDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var lead = await _leads.FindAsync(id);
            if (lead == null) return NotFoundProblem("Lead");
            if (!_currentUser.CanAccess(lead)) return ForbiddenProblem();
            return Ok(ToDto(lead));
        }

        /// <summary>Create a lead. New leads may start as New, Contacted or Qualified.</summary>
        [HttpPost]
        [ProducesResponseType<LeadDto>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] LeadCreateDto dto)
        {
            var result = await _leads.CreateAsync(new LeadFormViewModel
            {
                LeadName = dto.LeadName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Source = dto.Source,
                Status = dto.Status,
                Priority = dto.Priority,
                ExpectedValue = dto.ExpectedValue,
                Notes = dto.Notes,
                AssignedTo = dto.AssignedToId
            });
            if (!result.Succeeded) return ErrorFrom(result, "Lead");

            var created = await _leads.FindAsync(result.Id!.Value);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, ToDto(created!));
        }

        private static LeadDto ToDto(Lead l) => new()
        {
            Id = l.LeadId,
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            CompanyName = l.CompanyName,
            Source = l.Source,
            Status = l.Status,
            Priority = l.Priority,
            ExpectedValue = l.ExpectedValue,
            AssignedToId = l.AssignedTo,
            ConvertedCustomerId = l.ConvertedCustomerId,
            CreatedDate = l.CreatedDate
        };
    }
}