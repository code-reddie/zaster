using Microsoft.AspNetCore.Mvc;

namespace Zaster.Controllers;

public sealed class HelloController : ControllerBase
{
    [HttpGet("/hello")]
    public IActionResult GetHello()
    {
        return Ok(new { message = "Hello from backend!" });
    }
}
