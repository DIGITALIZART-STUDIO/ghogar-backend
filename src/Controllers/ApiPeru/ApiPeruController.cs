using GestionHogar.Controllers.ApiPeru;
using GestionHogar.Controllers.Client;
using GestionHogar.Model;
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

    [HttpGet("consultations/{documentType}/{documentNumber}/history")]
    public async Task<ActionResult<List<ApiPeruConsultation>>> GetConsultationHistory(
        string documentType,
        string documentNumber
    )
    {
        try
        {
            var result = await _apiPeruService.GetConsultationHistoryAsync(
                documentNumber,
                documentType
            );
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("consultations/cleanup")]
    public async Task<ActionResult<int>> CleanOldConsultations([FromQuery] int daysOld = 30)
    {
        try
        {
            var deletedCount = await _apiPeruService.CleanOldConsultationsAsync(daysOld);
            return Ok(
                new { deletedCount, message = $"Se eliminaron {deletedCount} consultas antiguas" }
            );
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
