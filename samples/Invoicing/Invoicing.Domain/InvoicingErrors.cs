using FluentValidation;

namespace Invoicing.Domain;

// The wording every host uses for the application's own exceptions, so the API, the CLI and the MCP server agree.
public static class InvoicingErrors
{
    public const string InvoiceNotFound = "The invoice doesn't exist.";

    // A safe message for the exceptions the application knows, or null for any other, which the host then reports
    // without details.
    public static string? Describe(Exception exception) =>
        exception switch
        {
            ValidationException validation => Describe(validation),
            InvoiceNotFoundException => InvoiceNotFound,
            _ => null,
        };

    // Validation failures as "Property: message" lines, without the attempted values, which could be anything the
    // caller sent and must never be echoed back.
    public static string Describe(ValidationException exception) =>
        string.Join("; ", exception.Errors.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
}
