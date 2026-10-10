using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Zaster.FinTs;

namespace Zaster.Controllers;

[ApiController]
[Route("api/fints")]
public sealed class FinTsController(FinTsService finTsService) : ControllerBase
{
    private readonly FinTsService _finTsService = finTsService;

    /// <summary>
    /// Ruft Buchungen per FinTS ab, ohne sie zu speichern.
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<FinTsTestResult>> Test(
        FinTsTestRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Iban) || string.IsNullOrWhiteSpace(request.Pin))
        {
            return BadRequest("IBAN und PIN sind erforderlich.");
        }

        var result = await _finTsService.FetchTransactionsAsync(request, cancellationToken);
        return Ok(result);
    }
}
