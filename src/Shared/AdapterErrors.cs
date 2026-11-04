using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace Datapoint.Mediator;

// The rules every host adapter follows to report an error without leaking details (ADR 0034). Linked as source into
// each adapter, which has no package in common with the others to share it from.
internal static partial class AdapterErrors
{
    public const string AuthenticationRequired = "Authentication is required.";

    public const string AccessDenied = "Access is denied.";

    public const string UnexpectedError = "An unexpected error occurred.";

    private const int MaxFieldLength = 128;

    // Matched by full name, so the adapters don't depend on the Authorization package (ADR 0013).
    public static bool IsAuthenticationRequired(Exception exception) =>
        exception.GetType().FullName == "Datapoint.Mediator.Authorization.AuthenticationRequiredException";

    public static bool IsAccessDenied(Exception exception) =>
        exception.GetType().FullName == "Datapoint.Mediator.Authorization.AccessDeniedException";

    // Only the caller's own cancellation counts as one. A handler's timeout also throws an OperationCanceledException,
    // and is an error like any other.
    public static bool IsCallerCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    // The argument that failed, or null when it isn't known or doesn't look like a path of property names, which an
    // app's own exception could carry. Never the exception's message.
    public static string? SafeField(InvalidActionArgumentsException exception) =>
        exception.Field is { Length: <= MaxFieldLength } field && FieldPattern().IsMatch(field) ? field : null;

    // A missing or null argument is reported as required.
    public static bool IsRequired(InvalidActionArgumentsException exception) =>
        exception.Reason is InvalidActionArgumentReason.Missing or InvalidActionArgumentReason.Null;

    [GeneratedRegex(@"^[A-Za-z0-9_$-]+(\[[0-9]+\])*(\.[A-Za-z0-9_$-]+(\[[0-9]+\])*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldPattern();
}
