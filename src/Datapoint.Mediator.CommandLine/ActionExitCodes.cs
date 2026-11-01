namespace Datapoint.Mediator.CommandLine;

/// <summary>
/// The exit codes of commands added for actions.
/// </summary>
public static class ActionExitCodes
{
    /// <summary>
    /// The action succeeded.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// The command line is not valid, such as a missing required option or a value of the wrong type. This is
    /// System.CommandLine's own code, and the action does not run.
    /// </summary>
    public const int InvalidCommandLine = 1;

    /// <summary>
    /// The arguments are not valid, or the action failed with a message from
    /// <see cref="MediatorCommandLineOptions.ErrorMessage"/>, such as a validation error.
    /// </summary>
    public const int InvalidArguments = 2;

    /// <summary>
    /// The action requires a signed-in user, and there is none.
    /// </summary>
    public const int AuthenticationRequired = 3;

    /// <summary>
    /// The user may not run the action.
    /// </summary>
    public const int AccessDenied = 4;

    /// <summary>
    /// The action failed unexpectedly. No details are printed. The code is <c>EX_SOFTWARE</c> from BSD
    /// <c>sysexits.h</c>.
    /// </summary>
    public const int UnexpectedError = 70;
}
