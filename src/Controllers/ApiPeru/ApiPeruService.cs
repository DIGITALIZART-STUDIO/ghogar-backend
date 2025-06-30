using System.Net.Http.Json;
using GestionHogar.Configuration;
using GestionHogar.Controllers.Client;
using Microsoft.Extensions.Options;

namespace GestionHogar.Services;

public class ApiPeruService
{
    private readonly ApiPeruConfiguration _config;
    private readonly HttpClient _httpClient;

    public ApiPeruService(IOptions<ApiPeruConfiguration> config, HttpClient httpClient)
    {
        _config = config.Value;
        _httpClient = httpClient;
    }

    public async Task<ResponseApiRucFull> GetDataByRucAsync(string ruc)
    {
        if (string.IsNullOrWhiteSpace(_config.Token))
            throw new Exception("API Peru token is not configured");

        // 1. Datos del RUC
        var urlRuc = $"{_config.BaseUrl}/ruc/{ruc}?api_token={_config.Token}";
        var responseRuc = await _httpClient.GetAsync(urlRuc);
        responseRuc.EnsureSuccessStatusCode();
        var jsonRuc = await responseRuc.Content.ReadFromJsonAsync<ApiPeruRucResponse>();
        var dataRuc = jsonRuc?.data;

        if (dataRuc == null || string.IsNullOrWhiteSpace(dataRuc.ruc))
            throw new Exception("RUC no encontrado o inválido.");

        // 2. Representantes legales
        var urlRepr = $"{_config.BaseUrl}/ruc_representantes/{ruc}?api_token={_config.Token}";
        var responseRepr = await _httpClient.GetAsync(urlRepr);
        responseRepr.EnsureSuccessStatusCode();
        var jsonRepr =
            await responseRepr.Content.ReadFromJsonAsync<ApiPeruRucRepresentantesResponse>();
        var dataRepr =
            jsonRepr?.data ?? new List<ApiPeruRucRepresentantesResponse.RucRepresentanteData>();

        // 3. Armar respuesta
        return new ResponseApiRucFull
        {
            Ruc = dataRuc.ruc,
            NombreORazonSocial = dataRuc.nombre_o_razon_social,
            Direccion = dataRuc.direccion,
            DireccionCompleta = dataRuc.direccion_completa,
            Estado = dataRuc.estado,
            Condicion = dataRuc.condicion,
            Departamento = dataRuc.departamento,
            Provincia = dataRuc.provincia,
            Distrito = dataRuc.distrito,
            UbigeoSunat = dataRuc.ubigeo_sunat,
            Ubigeo = dataRuc.ubigeo,
            EsAgenteDeRetencion = dataRuc.es_agente_de_retencion,
            EsBuenContribuyente = dataRuc.es_buen_contribuyente,
            Representantes = dataRepr
                .Select(x => new ResponseApiRucRepresentante
                {
                    TipoDeDocumento = x.tipo_de_documento,
                    NumeroDeDocumento = x.numero_de_documento,
                    Nombre = x.nombre,
                    Cargo = x.cargo,
                    FechaDesde = x.fecha_desde,
                })
                .ToList(),
        };
    }

    // Clases internas para deserializar la respuesta
    private class ApiPeruRucResponse
    {
        public RucData data { get; set; }

        public class RucData
        {
            public string ruc { get; set; }
            public string nombre_o_razon_social { get; set; }
            public string direccion { get; set; }
            public string direccion_completa { get; set; }
            public string estado { get; set; }
            public string condicion { get; set; }
            public string departamento { get; set; }
            public string provincia { get; set; }
            public string distrito { get; set; }
            public string ubigeo_sunat { get; set; }
            public string[] ubigeo { get; set; }
            public string es_agente_de_retencion { get; set; }
            public string es_buen_contribuyente { get; set; }
        }
    }

    private class ApiPeruRucRepresentantesResponse
    {
        public List<RucRepresentanteData> data { get; set; }

        public class RucRepresentanteData
        {
            public string tipo_de_documento { get; set; }
            public string numero_de_documento { get; set; }
            public string nombre { get; set; }
            public string cargo { get; set; }
            public string fecha_desde { get; set; }
        }
    }

    public async Task<ResponseApiDni> GetDataByDniAsync(string dni)
    {
        if (string.IsNullOrWhiteSpace(_config.Token))
            throw new Exception("API Peru token is not configured");

        var url = $"{_config.BaseUrl}/dni/{dni}?api_token={_config.Token}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<ApiPeruDniResponse>();
        var data = json?.data;

        if (
            data == null
            || string.IsNullOrWhiteSpace(data.numero)
            || string.IsNullOrWhiteSpace(data.nombre_completo)
        )
            throw new Exception("DNI no encontrado o inválido.");

        return new ResponseApiDni { Numero = data.numero, NombreCompleto = data.nombre_completo };
    }

    // Clase interna para deserializar la respuesta del DNI
    private class ApiPeruDniResponse
    {
        public DniData data { get; set; }

        public class DniData
        {
            public string numero { get; set; }
            public string nombre_completo { get; set; }
        }
    }
}
