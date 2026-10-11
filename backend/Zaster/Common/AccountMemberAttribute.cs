using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zaster.Database;

namespace Zaster.Common;

/// <summary>
/// Lässt nur Mitglieder des Kontos aus dem Routenparameter <c>accountId</c> durch.
/// Fremde und unbekannte Konten liefern 404, damit niemand fremde Konto-Ids ausprobieren kann.
/// </summary>
public sealed class AccountMemberAttribute() : TypeFilterAttribute(typeof(AccountMemberFilter))
{
    private sealed class AccountMemberFilter(AppDbContext context) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext filterContext, ActionExecutionDelegate next)
        {
            var userId = filterContext.HttpContext.User.GetUserId();
            var isMember = filterContext.RouteData.Values.TryGetValue("accountId", out var value)
                && int.TryParse(value?.ToString(), out var accountId)
                && await context.Accounts.AnyAsync(
                    a => a.Id == accountId && a.Users.Any(u => u.Id == userId),
                    filterContext.HttpContext.RequestAborted);

            if (!isMember)
            {
                var factory = filterContext.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                var problem = factory.CreateProblemDetails(
                    filterContext.HttpContext,
                    StatusCodes.Status404NotFound,
                    detail: "Das Konto gibt es nicht.");
                filterContext.Result = new NotFoundObjectResult(problem);
                return;
            }

            await next();
        }
    }
}
