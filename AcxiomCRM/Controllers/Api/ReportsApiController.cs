using AcxiomCRM.DTOs;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api
{
    [Route("api/reports")]
    public class ReportsApiController : ApiControllerBase
    {
        private readonly IOpportunityService _opportunities;

        public ReportsApiController(IOpportunityService opportunities)
        {
            _opportunities = opportunities;
        }

        /// <summary>Stage-wise pipeline totals. Manager/Admin only.</summary>
        [HttpGet("pipeline")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = AppPolicies.ManagerOrAdmin)]
        [ProducesResponseType<PipelineReportDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Pipeline()
        {
            var p = await _opportunities.GetPipelineAsync();

            return Ok(new PipelineReportDto
            {
                Stages = p.Columns.Select(c => new PipelineStageDto
                {
                    Stage = c.Stage,
                    Count = c.Items.Count,
                    TotalAmount = c.TotalAmount,
                    WeightedAmount = c.TotalWeighted
                }).ToList(),
                OpenPipelineAmount = p.OpenPipelineAmount,
                OpenWeightedAmount = p.OpenWeightedAmount
            });
        }
    }
}