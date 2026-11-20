using Microsoft.AspNetCore.Mvc;
using ChecadorAPI.Models.DTOs;
using ChecadorAPI.Services;

namespace ChecadorAPI.Controllers;

[ApiController]
[Route("api/bindings")]
public class BindingController : ControllerBase
{
    private readonly IBindingService _bindingService;

    public BindingController(IBindingService bindingService)
    {
        _bindingService = bindingService;
    }

    [HttpGet("{machineName}")]
    public async Task<ActionResult<ApiResponseDto<EquipmentBindingResponseDto>>> GetBinding(string machineName)
    {
        var result = await _bindingService.ObtenerVinculoAsync(machineName);
        return result.Exito == 1 ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<bool>>> CreateBinding([FromBody] CreateBindingRequestDto req)
    {
        var result = await _bindingService.CrearVinculoAsync(req);
        return result.Exito == 1 ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{machineName}")]
    public async Task<ActionResult<ApiResponseDto<bool>>> DeleteBinding(string machineName, [FromBody] DeleteBindingRequestDto req)
    {
        var result = await _bindingService.DesvincularAsync(machineName, req);
        return result.Exito == 1 ? Ok(result) : BadRequest(result);
    }
}
