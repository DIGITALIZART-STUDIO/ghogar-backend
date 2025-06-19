using System.Threading.Tasks;
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

        [HttpGet]
        public async Task<IActionResult> GetCurrentExchangeRate()
        {
            var exchangeRate = await _exchangeRateService.GetCurrentExchangeRateAsync();

            if (exchangeRate == 0)
            {
                return NotFound("No se pudo obtener el tipo de cambio");
            }

            return Ok(new { exchangeRate });
        }
    }
}
