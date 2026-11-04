using AcxiomCRM.DTOs;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api
{
    [Route("api/followups")]
    public class FollowUpsApiController : ApiControllerBase
    {
        private readonly IFollowUpService _followUps;

        public FollowUpsApiController(IFollowUpService followUps)
        {
            _followUps = followUps;
        }

        /// <summary>List follow-ups. view = all | upcoming | overdue | today.</summary>
        [HttpGet]
        [ProducesResponseType<PagedResponse<FollowUpDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] string view = "all", [FromQuery] FollowUpStatus? status = null,
                                                [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var list = await _followUps.GetListAsync(new FollowUpListViewModel
            {
                View = view,
                Status = status,
                Page = page,
                PageSize = pageSize
            });

            return Ok(new PagedResponse<FollowUpDto>
            {
                Items = list.Items.Select(i => new FollowUpDto
                {
                    Id = i.FollowUpId,
                    FollowUpDate = i.FollowUpDate,
                    Subject = i.Subject,
                    FollowUpType = i.FollowUpType,
                    Status = i.Status,
                    RelatedType = i.RelatedType,
                    RelatedId = i.RelatedId,
                    RelatedName = i.RelatedName,
                    IsOverdue = i.IsOverdue,
                    AssignedToName = i.AssignedToName
                }).ToList(),
                Page = list.Page,
                PageSize = list.PageSize,
                TotalCount = list.TotalCount
            });
        }

        /// <summary>Schedule a follow-up. Date cannot be earlier than today.</summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] FollowUpCreateDto dto)
        {
            if (dto.CustomerId is null && dto.LeadId is null && dto.OpportunityId is null)
            {
                ModelState.AddModelError(nameof(dto.CustomerId), "Select a related customer, lead or opportunity.");
                return ValidationProblem(ModelState);
            }

            var result = await _followUps.CreateAsync(new FollowUpFormViewModel
            {
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                OpportunityId = dto.OpportunityId,
                FollowUpDate = dto.FollowUpDate,
                Subject = dto.Subject,
                FollowUpType = dto.FollowUpType,
                Remarks = dto.Remarks,
                Status = FollowUpStatus.Planned,
                AssignedTo = dto.AssignedToId
            });
            if (!result.Succeeded) return ErrorFrom(result, "Follow-up");

            return StatusCode(StatusCodes.Status201Created, new { id = result.Id, message = "Follow-up scheduled." });
        }
    }
}