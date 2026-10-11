using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Zaster.Common;

/// <summary>
/// Basis aller Controller: Nutzer-ID aus dem JWT und Fehler als ProblemDetails mit deutscher Meldung.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Id des angemeldeten Nutzers. Alle Endpunkte außer Login und Registrierung verlangen eine Anmeldung.
    /// </summary>
    protected int UserId => User.GetUserId();

    protected ObjectResult BadRequestProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);

    protected ObjectResult NotFoundProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status404NotFound);

    protected ObjectResult ConflictProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status409Conflict);

    /// <summary>
    /// Verstoß gegen einen eindeutigen Index, z. B. wenn zwei Anfragen gleichzeitig denselben Namen anlegen.
    /// </summary>
    protected static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueConstraint };

    private const int SqliteUniqueConstraint = 2067;
}

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal principal)
    {
        public int GetUserId() =>
            int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
    }
}
