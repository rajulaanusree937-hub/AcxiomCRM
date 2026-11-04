using System.Text.Json;
using System.Text.Json.Serialization;
using AcxiomCRM.Data;

namespace AcxiomCRM.Services;

// Standard action names (17.16)
public static class AuditActions
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Lockout = "Lockout";
    public const string Logout = "Logout";
    public const string Register = "Register";
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Convert = "Convert";
    public const string RoleChange = "RoleChange";
    public const string UserStatusChange = "UserStatusChange";
    public const string PasswordReset = "PasswordReset";
    public const string Unlock = "Unlock";

    public static readonly string[] All =
        { Login, LoginFailed, Lockout, Logout, Register, Create, Update, Delete, Convert,
          RoleChange, UserStatusChange, PasswordReset, Unlock };
}

public interface IAuditService
{
    // For security/business events that aren't a simple create/update/delete.
    // NEVER pass passwords, hashes or tokens in oldValue/newValue.

    Task LogAsync(string action, string entityName, string? recordId = null,
                  object? oldValue = null, object? newValue = null, string? userId = null);
}

public class AuditService : IAuditService
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }   // "Qualified" instead of 2
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AuditService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task LogAsync(string action, string entityName, string? recordId = null,
                               object? oldValue = null, object? newValue = null, string? userId = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? _currentUser.UserId,
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            OldValue = oldValue == null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            NewValue = newValue == null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
            CreatedDate = DateTime.UtcNow,

            IpAddress = _currentUser.IpAddress
        });
        await _db.SaveChangesAsync();
    }
}