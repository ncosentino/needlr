namespace NexusLabs.Needlr.Logging.Generators.Models;

/// <summary>
/// Classifies how a parameter of a <c>[NeedlrLoggerMessage]</c> method participates in the generated body.
/// </summary>
internal enum ParameterRole
{
    /// <summary>
    /// A message template argument bound by position to the structured message.
    /// </summary>
    Message = 0,

    /// <summary>
    /// The <c>ILogger</c> supplied to a static logging method.
    /// </summary>
    Logger = 1,

    /// <summary>
    /// The exception argument that drives the cancellation guard and the trailing exception slot.
    /// </summary>
    Exception = 2,
}
