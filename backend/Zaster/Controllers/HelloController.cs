using Microsoft.AspNetCore.Mvc;

namespace Zaster.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HelloController : ControllerBase
{
    [HttpGet]
    public IActionResult GetHello()
    {
        return Ok(new { message = "Hello from backend!" });
    }
}
