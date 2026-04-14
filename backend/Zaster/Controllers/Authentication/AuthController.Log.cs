using Microsoft.Extensions.Logging;
using Zaster.Models;

namespace Zaster.Controllers.Authentication;

public partial class AuthController
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Token creation failed for user {UserName}.")]
    static partial void LogTokenCreationFailed(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registration failed: user {UserName} already exists.")]
    static partial void LogUserAlreadyExists(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorization failed: user {UserName} does not exist.")]
    static partial void LogUserNotFound(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorization failed: invalid password for user {UserName}.")]
    static partial void LogInvalidPassword(ILogger logger, string userName);
}
