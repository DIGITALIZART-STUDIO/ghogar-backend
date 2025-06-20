using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GestionHogar.Services
{
    public class ExchangeRateService : IExchangeRateService
    {
        private readonly ILogger<ExchangeRateService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _sunatUrl;

        public ExchangeRateService(
            ILogger<ExchangeRateService> logger,
            IConfiguration configuration,
            HttpClient httpClient
        )
        {
            _logger = logger;
            _httpClient = httpClient;

            // URL directa para obtener el tipo de cambio SUNAT
            _sunatUrl = "https://www.sunat.gob.pe/a/txt/tipoCambio.txt";
        }

        /// <summary>
        /// Obtiene el tipo de cambio actual de la SUNAT
        /// </summary>
        /// <returns>Valor de tipo de cambio con 2 decimales</returns>
        public async Task<decimal> GetCurrentExchangeRateAsync()
        {
            try
            {
                _logger.LogInformation("Obteniendo tipo de cambio de SUNAT");

                var response = await _httpClient.GetStringAsync(_sunatUrl);
                var data = response.Trim().Split('|');

                if (data.Length < 3)
                {
                    _logger.LogWarning("Formato de respuesta SUNAT inesperado");
                    return 0;
                }

                // data[0] = fecha, data[1] = compra, data[2] = venta
                if (decimal.TryParse(data[2], out decimal exchangeRate))
                {
                    return Math.Round(exchangeRate, 2);
                }
                else
                {
                    _logger.LogWarning("No se pudo convertir el valor del tipo de cambio");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el tipo de cambio de SUNAT");
                return 0;
            }
        }
    }

    public interface IExchangeRateService
    {
        Task<decimal> GetCurrentExchangeRateAsync();
    }
}
