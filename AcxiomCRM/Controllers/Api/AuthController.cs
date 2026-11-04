using AcxiomCRM.DTOs;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers.Api
{
    [Route("api/auth")]
    public class AuthController : ApiControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokens;
        private readonly IAuditService _audit;
        private readonly ICurrentUserService _currentUser;

        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
                              ITokenService tokens, IAuditService audit, ICurrentUserService currentUser)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokens = tokens;
            _audit = audit;
            _currentUser = currentUser;
        }

        /// <summary>Authenticate and receive a JWT access token. Rate limited: 5 attempts per minute per IP.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]

        [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);

            if (user == null)
            {
                await _audit.LogAsync(AuditActions.LoginFailed, "User", null,
                    newValue: new { request.Email, Channel = "API", Reason = "Invalid credentials" });
                return InvalidCredentials();
            }

            if (!user.IsActive)
            {
                await _audit.LogAsync(AuditActions.LoginFailed, "User", user.Id,
                    newValue: new { request.Email, Channel = "API", Reason = "Account deactivated" }, userId: user.Id);
                return await _userManager.CheckPasswordAsync(user, request.Password)
                    ? Problem(statusCode: StatusCodes.Status403Forbidden, title: "Account is deactivated.")
                    : InvalidCredentials();
            }

            // Same lockout rules as the web login (6.3)
            var check = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

            if (check.IsLockedOut)
            {
                await _audit.LogAsync(AuditActions.Lockout, "User", user.Id,
                    newValue: new { request.Email, Channel = "API" }, userId: user.Id);

                return Problem(statusCode: StatusCodes.Status403Forbidden,
                               title: "Account is locked due to repeated failed attempts. Try again later.");
            }

            if (!check.Succeeded)
            {
                await _audit.LogAsync(AuditActions.LoginFailed, "User", user.Id,
                    newValue: new { request.Email, Channel = "API", Reason = "Invalid credentials" }, userId: user.Id);
                return InvalidCredentials();
            }

            var (token, expires) = await _tokens.CreateTokenAsync(user);
            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            await _audit.LogAsync(AuditActions.Login, "User", user.Id,
                newValue: new { Channel = "API" }, userId: user.Id);

            return Ok(new LoginResponseDto
            {
                AccessToken = token,
                ExpiresAtUtc = expires,
                User = new AuthUserDto { Id = user.Id, FullName = user.FullName, Email = user.Email ?? "", Role = role }
            });
        }

        /// <summary>Log out. JWTs are stateless — the client must discard its token. The event is audited.</summary>
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            await _audit.LogAsync(AuditActions.Logout, "User", _currentUser.UserId, newValue: new { Channel = "API" });

            return Ok(new { message = "Logged out. Discard the access token on the client." });
        }

        /// <summary>Who am I? Useful to verify a token.</summary>
        [HttpGet("me")]
        [ProducesResponseType<AuthUserDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Me()
        {
            var user = await _userManager.FindByIdAsync(_currentUser.UserId!);
            if (user == null) return NotFoundProblem("User");

            return Ok(new AuthUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault()
            });
        }

        // Same message for "no such user" and "wrong password" — attackers can't probe which emails exist
        private ObjectResult InvalidCredentials() =>
            Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
    }
}