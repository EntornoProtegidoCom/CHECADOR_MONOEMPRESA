using Microsoft.AspNetCore.Mvc;
using ChecadorAPI.Models.DTOs;
using ChecadorAPI.Services;

namespace ChecadorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("worker-login")]
    public async Task<ActionResult<ApiResponseDto<WorkerLoginResponseDto>>> WorkerLogin([FromBody] WorkerLoginRequestDto req)
    {
        var result = await _authService.LoginTrabajadorAsync(req);
        return result.Exito switch
        {
            1 => Ok(result),
            2 => BadRequest(result),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result)
        };
    }

    [HttpPost("admin-login")]
    public async Task<ActionResult<ApiResponseDto<AdminLoginResponseDto>>> AdminLogin([FromBody] AdminLoginRequestDto req)
    {
        var result = await _authService.LoginAdminAsync(req);
        return result.Exito switch
        {
            1 => Ok(result),
            2 => BadRequest(result),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result)
        };
    }
}
