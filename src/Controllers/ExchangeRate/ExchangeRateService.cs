using System;
using System.Net.Http;
using System.Threading.Tasks;
using GestionHogar.Controllers.Dtos;
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
        /// <returns>DTO con el tipo de cambio y metadatos</returns>
        public async Task<ExchangeRateDto> GetCurrentExchangeRateAsync()
        {
            try
            {
                _logger.LogInformation("Obteniendo tipo de cambio de SUNAT");

                var response = await _httpClient.GetStringAsync(_sunatUrl);
                var data = response.Trim().Split('|');

                if (data.Length < 3)
                {
                    _logger.LogWarning("Formato de respuesta SUNAT inesperado");
                    return new ExchangeRateDto
                    {
                        ExchangeRate = 0,
                        IsSuccess = false,
                        Message = "Formato de respuesta SUNAT inesperado",
                    };
                }

                // data[0] = fecha, data[1] = compra, data[2] = venta
                if (decimal.TryParse(data[2], out decimal exchangeRate))
                {
                    var roundedRate = Math.Round(exchangeRate, 2);
                    _logger.LogInformation(
                        "Tipo de cambio obtenido exitosamente: {ExchangeRate}",
                        roundedRate
                    );

                    return new ExchangeRateDto
                    {
                        ExchangeRate = roundedRate,
                        RetrievedAt = DateTime.UtcNow,
                        Source = "SUNAT",
                        IsSuccess = true,
                        Message = $"Tipo de cambio obtenido: {roundedRate}",
                    };
                }
                else
                {
                    _logger.LogWarning("No se pudo convertir el valor del tipo de cambio");
                    return new ExchangeRateDto
                    {
                        ExchangeRate = 0,
                        IsSuccess = false,
                        Message = "No se pudo convertir el valor del tipo de cambio",
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el tipo de cambio de SUNAT");
                return new ExchangeRateDto
                {
                    ExchangeRate = 0,
                    IsSuccess = false,
                    Message = $"Error al obtener el tipo de cambio: {ex.Message}",
                };
            }
        }
    }

    public interface IExchangeRateService
    {
        Task<ExchangeRateDto> GetCurrentExchangeRateAsync();
    }
}
