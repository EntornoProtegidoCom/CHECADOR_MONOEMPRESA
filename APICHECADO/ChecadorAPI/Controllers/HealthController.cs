using Microsoft.AspNetCore.Mvc;

namespace ChecadorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Check()
    {
        return Ok(new
        {
            status = "UP",
            service = "ChecadorAPI",
            timestamp = DateTime.UtcNow
        });
    }
}
