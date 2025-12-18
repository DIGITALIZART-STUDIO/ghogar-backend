using GestionHogar.Controllers.ApiPeru;
using GestionHogar.Controllers.Client;
using GestionHogar.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/apiperu")]
public class ApiPeruController : ControllerBase
{
    private readonly ApiPeruService _apiPeruService;

    public ApiPeruController(ApiPeruService apiPeruService)
    {
        _apiPeruService = apiPeruService;
    }

    [HttpGet("ruc/{ruc}/info")]
    public async Task<ActionResult<ResponseApiRucFull>> GetRucFullInfo(string ruc)
    {
        try
        {
            var result = await _apiPeruService.GetDataByRucAsync(ruc);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("dni/{dni}/info")]
    public async Task<ActionResult<ResponseApiDni>> GetDniInfo(string dni)
    {
        try
        {
            var result = await _apiPeruService.GetDataByDniAsync(dni);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
