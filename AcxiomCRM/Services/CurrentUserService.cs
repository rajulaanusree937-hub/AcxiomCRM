using System.Security.Claims;

namespace AcxiomCRM.Services;

// Who is making the current request, and what are they allowed to see?
public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }

    bool IsAdmin { get; }
    bool IsManager { get; }
    bool IsSalesExecutive { get; }

    // Admin = Full, Manager = Team (all sales records) -> see everything.
    // SalesExecutive = Own/Assigned only (7.1).
    bool CanSeeAllRecords { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? Email => User?.Identity?.Name;
    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public bool IsAdmin => User?.IsInRole(AppRoles.Admin) == true;
    public bool IsManager => User?.IsInRole(AppRoles.Manager) == true;
    public bool IsSalesExecutive => User?.IsInRole(AppRoles.SalesExecutive) == true;

    public bool CanSeeAllRecords => IsAdmin || IsManager;
}
