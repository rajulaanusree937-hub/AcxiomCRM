namespace AcxiomCRM.Services;

// Standard result returned by every service method
public class ServiceResult
{
    public List<(string Field, string Message)> Errors { get; } = new();
    public bool NotFound { get; private set; }
    public bool Forbidden { get; private set; }
    public int? Id { get; private set; }

    public bool Succeeded => Errors.Count == 0 && !NotFound && !Forbidden;

    public static ServiceResult Ok(int? id = null) => new() { Id = id };
    public static ServiceResult NotFoundResult() => new() { NotFound = true };
    public static ServiceResult ForbiddenResult() => new() { Forbidden = true };

    // Field = property name (e.g. "Email") so the error shows next to that input;
    // use "" for a general message at the top of the form.
    public ServiceResult AddError(string field, string message)
    {
        Errors.Add((field, message));
        return this;
    }
}