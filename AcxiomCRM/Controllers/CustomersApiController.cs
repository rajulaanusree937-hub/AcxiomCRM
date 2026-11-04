using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api
{
    [Route("api/customers")]
    public class CustomersApiController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ICustomerService _customers;
        private readonly ICurrentUserService _currentUser;

        public CustomersApiController(ApplicationDbContext db, ICustomerService customers, ICurrentUserService currentUser)
        {
            _db = db;
            _customers = customers;
            _currentUser = currentUser;
        }

        /// <summary>List / search customers visible to the caller (Admin/Manager: all, SalesExecutive: assigned).</summary>
        [HttpGet]
        [ProducesResponseType<PagedResponse<CustomerDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] CustomerStatus? status,
                                                [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);

            var query = _db.Customers.AsNoTracking().VisibleTo(_currentUser);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(c => c.CustomerName.Contains(s) || c.Email.Contains(s) || c.Phone.Contains(s) ||
                                         (c.CompanyName != null && c.CompanyName.Contains(s)));
            }
            if (status.HasValue) query = query.Where(c => c.Status == status.Value);

            var total = await query.CountAsync();
            var items = await query.OrderByDescending(c => c.CreatedDate)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => ToDto(c))
                .ToListAsync();

            return Ok(new PagedResponse<CustomerDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
        }

        /// <summary>Get one customer.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var customer = await _customers.FindAsync(id);
            if (customer == null) return NotFoundProblem("Customer");
            if (!_currentUser.CanAccess(customer)) return ForbiddenProblem();
            return Ok(ToDto(customer));
        }

        /// <summary>Create a customer. 409 if email/phone already exists.</summary>
        [HttpPost]
        [ProducesResponseType<CustomerDto>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CustomerSaveDto dto)
        {
            var result = await _customers.CreateAsync(ToForm(dto, 0));
            if (!result.Succeeded) return ErrorFrom(result, "Customer");

            var created = await _customers.FindAsync(result.Id!.Value);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, ToDto(created!));
        }

        /// <summary>Update a customer.</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(int id, [FromBody] CustomerSaveDto dto)
        {
            var result = await _customers.UpdateAsync(ToForm(dto, id));
            if (!result.Succeeded) return ErrorFrom(result, "Customer");

            var updated = await _customers.FindAsync(id);
            return Ok(ToDto(updated!));
        }

        /// <summary>Delete a customer. 409 if it has related records (deactivate it instead).</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _customers.DeleteAsync(id);
            if (result.NotFound) return NotFoundProblem("Customer");
            if (result.Forbidden) return ForbiddenProblem();
            if (!result.Succeeded)
                return Problem(statusCode: StatusCodes.Status409Conflict, title: result.Errors.First().Message);

            return NoContent();
        }

        private static CustomerFormViewModel ToForm(CustomerSaveDto d, int id) => new()
        {
            CustomerId = id,
            CustomerName = d.CustomerName,
            Email = d.Email,
            Phone = d.Phone,
            CompanyName = d.CompanyName,
            Address = d.Address,
            City = d.City,
            State = d.State,
            Status = d.Status,
            Notes = d.Notes,
            AssignedTo = d.AssignedToId
        };

        private static CustomerDto ToDto(Customer c) => new()
        {
            Id = c.CustomerId,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Status = c.Status,
            AssignedToId = c.AssignedTo,
            CreatedDate = c.CreatedDate,
            ModifiedDate = c.ModifiedDate
        };
    }
}