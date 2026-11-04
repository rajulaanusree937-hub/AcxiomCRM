using AcxiomCRM.DTOs;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api
{
    [Route("api/opportunities")]
    public class OpportunitiesApiController : ApiControllerBase
    {
        private readonly IOpportunityService _opportunities;
        private readonly ICurrentUserService _currentUser;

        public OpportunitiesApiController(IOpportunityService opportunities, ICurrentUserService currentUser)
        {
            _opportunities = opportunities;
            _currentUser = currentUser;
        }

        /// <summary>List / search opportunities visible to the caller.</summary>
        [HttpGet]
        [ProducesResponseType<PagedResponse<OpportunityDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] OpportunityStage? stage,
                                                [FromQuery] OpportunityStatus? status,
                                                [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var list = await _opportunities.GetListAsync(new OpportunityListViewModel
            {
                Search = search,
                Stage = stage,
                Status = status,
                Page = page,
                PageSize = pageSize
            });

            return Ok(new PagedResponse<OpportunityDto>
            {
                Items = list.Items.Select(ToDto).ToList(),
                Page = list.Page,
                PageSize = list.PageSize,
                TotalCount = list.TotalCount
            });
        }

        /// <summary>Get one opportunity.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType<OpportunityDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var o = await _opportunities.FindWithRelatedAsync(id);
            if (o == null) return NotFoundProblem("Opportunity");
            if (!_currentUser.CanAccess(o)) return ForbiddenProblem();

            return Ok(new OpportunityDto
            {
                Id = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer?.CustomerName,
                Stage = o.Stage,
                Status = o.Status,
                Amount = o.Amount,
                Probability = o.Probability,
                WeightedAmount = o.Amount * o.Probability / 100m,
                ExpectedCloseDate = o.ExpectedCloseDate
            });
        }

        /// <summary>Create an opportunity. Amount > 0, Probability 0–100, close date not in the past.</summary>
        [HttpPost]
        [ProducesResponseType<OpportunityDto>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] OpportunityCreateDto dto)
        {
            // The service re-checks every business rule (17.7) — the API can't bypass them
            var result = await _opportunities.CreateAsync(new OpportunityFormViewModel
            {
                OpportunityName = dto.OpportunityName,
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                Amount = dto.Amount,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Stage = dto.Stage,
                Source = dto.Source,
                Notes = dto.Notes,
                AssignedTo = dto.AssignedToId
            });
            if (!result.Succeeded) return ErrorFrom(result, "Opportunity");

            return await GetById(result.Id!.Value) is OkObjectResult ok
                ? CreatedAtAction(nameof(GetById), new { id = result.Id }, ok.Value)
                : StatusCode(StatusCodes.Status201Created);
        }

        private static OpportunityDto ToDto(OpportunityListItem i) => new()
        {
            Id = i.OpportunityId,
            OpportunityName = i.OpportunityName,
            CustomerId = i.CustomerId,
            CustomerName = i.CustomerName,
            Stage = i.Stage,
            Status = i.Status,
            Amount = i.Amount,
            Probability = i.Probability,
            WeightedAmount = i.WeightedAmount,
            ExpectedCloseDate = i.ExpectedCloseDate,
            AssignedToName = i.AssignedToName
        };
    }
}