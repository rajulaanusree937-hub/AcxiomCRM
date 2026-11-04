using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCRM.Controllers.Api
{
    // Base for every REST API controller (4.10, 10.1):
    //  - [ApiController]: automatic 400 with validation details when input is invalid
    //  - JWT bearer only: the browser login cookie is NOT accepted by the API
    //  - AllCrmUsers policy + per-record ownership inside each action
    //  - No anti-forgery: bearer tokens are never sent automatically by browsers, so CSRF doesn't apply
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = AppPolicies.AllCrmUsers)]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public abstract class ApiControllerBase : ControllerBase
    {
        // Consistent error objects — never stack traces or DB details (10.1)
        protected ObjectResult NotFoundProblem(string what = "Record") =>
            Problem(statusCode: StatusCodes.Status404NotFound, title: $"{what} not found.");

        protected ObjectResult ForbiddenProblem() =>
            Problem(statusCode: StatusCodes.Status403Forbidden, title: "You do not have access to this record.");

        // Map a service result to 404 / 403 / 409 / 400
        protected IActionResult ErrorFrom(ServiceResult result, string what = "Record")
        {
            if (result.NotFound) return NotFoundProblem(what);
            if (result.Forbidden) return ForbiddenProblem();


            var modelState = new ModelStateDictionary();
            foreach (var (field, message) in result.Errors)
                modelState.AddModelError(string.IsNullOrEmpty(field) ? "general" : field, message);

            // Duplicates are a conflict with existing data -> 409
            var isConflict = result.Errors.Any(e => e.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase));
            return ValidationProblem(
                statusCode: isConflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
                modelStateDictionary: modelState);
        }
    }
}