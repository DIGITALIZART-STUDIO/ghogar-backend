using System.Threading.Tasks;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Services;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExchangeRateController : ControllerBase
    {
        private readonly IExchangeRateService _exchangeRateService;

        public ExchangeRateController(IExchangeRateService exchangeRateService)
        {
            _exchangeRateService = exchangeRateService;
        }

        /// <summary>
        /// Obtiene el tipo de cambio actual de SUNAT
        /// </summary>
        /// <returns>DTO con el tipo de cambio y metadatos</returns>
        /// <response code="200">Tipo de cambio obtenido exitosamente</response>
        /// <response code="404">No se pudo obtener el tipo de cambio</response>
        /// <response code="500">Error interno del servidor</response>
        [HttpGet]
        [ProducesResponseType(typeof(ExchangeRateDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ExchangeRateDto>> GetCurrentExchangeRate()
        {
            try
            {
                var exchangeRateDto = await _exchangeRateService.GetCurrentExchangeRateAsync();

                if (!exchangeRateDto.IsSuccess || exchangeRateDto.ExchangeRate == 0)
                {
                    return NotFound(exchangeRateDto.Message);
                }

                return Ok(exchangeRateDto);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }
    }
}
