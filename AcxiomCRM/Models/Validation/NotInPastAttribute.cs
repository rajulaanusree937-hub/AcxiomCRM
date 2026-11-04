using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AcxiomCRM.Models.Validation;

// "Date cannot be in the past" — validated in the browser AND on the server (5.1, 5.3).
// Optional condition: skip the rule when another field has one of the InactiveValues
// e.g. only for Planned follow-ups, only for open opportunities.
[AttributeUsage(AttributeTargets.Property)]
public class NotInPastAttribute : ValidationAttribute, IClientModelValidator
{
    public string? DependentProperty { get; set; }
    public string? InactiveValues { get; set; }   // comma-separated

    public NotInPastAttribute(string errorMessage)
    {
        ErrorMessage = errorMessage;
    }

    // ---------- Server side ----------
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not DateTime date) return ValidationResult.Success;   // [Required] handles empty
        if (!IsRuleActive(context.ObjectInstance)) return ValidationResult.Success;

        return date.Date < DateTime.Today
            ? new ValidationResult(ErrorMessage, new[] { context.MemberName! })
            : ValidationResult.Success;
    }

    private bool IsRuleActive(object instance)
    {

        if (string.IsNullOrEmpty(DependentProperty)) return true;

        var current = instance.GetType().GetProperty(DependentProperty)?.GetValue(instance)?.ToString() ?? "";
        var inactive = (InactiveValues ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return !inactive.Contains(current, StringComparer.OrdinalIgnoreCase);
    }

    // ---------- Client side: emits data-val-* attributes read by site.js ----------
    public void AddValidation(ClientModelValidationContext context)
    {
        Merge(context.Attributes, "data-val", "true");
        Merge(context.Attributes, "data-val-notinpast", ErrorMessage ?? "Date cannot be in the past.");
        if (!string.IsNullOrEmpty(DependentProperty))
        {
            Merge(context.Attributes, "data-val-notinpast-dependent", DependentProperty);
            Merge(context.Attributes, "data-val-notinpast-inactive", InactiveValues ?? "");
        }
    }

    private static void Merge(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key)) attributes.Add(key, value);
    }
}