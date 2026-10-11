using Microsoft.Extensions.Logging;

namespace Zaster.Auth;

public sealed partial class AuthController
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Registrierung abgelehnt: {UserName} gibt es schon.")]
    static partial void LogUserAlreadyExists(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Anmeldung fehlgeschlagen für {UserName}.")]
    static partial void LogLoginFailed(ILogger logger, string userName);
}
