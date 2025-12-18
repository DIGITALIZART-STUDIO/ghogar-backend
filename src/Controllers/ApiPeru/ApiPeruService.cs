using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using GestionHogar.Configuration;
using GestionHogar.Controllers.Client;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionHogar.Services;

public class ApiPeruService
{
    private readonly ApiPeruConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly DatabaseContext _context;
    private readonly ILogger<ApiPeruService> _logger;

    public ApiPeruService(
        IOptions<ApiPeruConfiguration> config,
        HttpClient httpClient,
        DatabaseContext context,
        ILogger<ApiPeruService> logger
    )
    {
        _config = config.Value;
        _httpClient = httpClient;
        _context = context;
        _logger = logger;
    }

    public async Task<ResponseApiRucFull> GetDataByRucAsync(string ruc)
    {
        // 1. Buscar consulta previa en la base de datos
        var existingConsultation = await _context.ApiPeruConsultations.FirstOrDefaultAsync(c =>
            c.DocumentNumber == ruc && c.DocumentType == "RUC"
        );

        if (existingConsultation != null)
        {
            // Si existe una consulta previa, devolver los datos guardados
            var cachedData = existingConsultation.GetResponseData<ResponseApiRucFull>();
            if (cachedData != null)
            {
                return cachedData;
            }
        }

        // 2. Buscar en la base de datos de clientes
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Ruc == ruc);
        if (client != null)
        {
            var reps = new List<ResponseApiRucRepresentante>();
            if (client.Type == ClientType.Juridico && !string.IsNullOrWhiteSpace(client.Name))
            {
                reps.Add(
                    new ResponseApiRucRepresentante
                    {
                        Nombre = client.Name,
                        // Los demás campos quedan vacíos porque solo tienes el nombre
                    }
                );
            }
            var clientData = new ResponseApiRucFull
            {
                Ruc = client.Ruc!,
                NombreORazonSocial = client.CompanyName!,
                Direccion = client.Address!,
                // Mapea otros campos si los tienes en tu modelo
                Representantes = reps,
            };

            // Guardar consulta en la base de datos
            await SaveConsultationAsync(ruc, "RUC", clientData);
            return clientData;
        }

        // 2. Scraping SUNAT para datos principales
        var sunatData = await this.ScrapSunat(ruc);

        // 3. Obtener representantes legales de SUNAT (prioridad)
        var representantesSunat = await this.GetRepresentantesSunat(ruc);

        // 4. Obtener representantes de API Perú solo si no hay representantes de SUNAT
        var representantesApiPeru =
            new List<ApiPeruRucRepresentantesResponse.RucRepresentanteData>();
        if (representantesSunat.Count == 0 && !string.IsNullOrWhiteSpace(_config.Token))
        {
            try
            {
                var urlRepr =
                    $"{_config.BaseUrl}/ruc_representantes/{ruc}?api_token={_config.Token}";
                var responseRepr = await _httpClient.GetAsync(urlRepr);

                if (responseRepr.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    representantesApiPeru =
                        new List<ApiPeruRucRepresentantesResponse.RucRepresentanteData>();
                }
                else
                {
                    responseRepr.EnsureSuccessStatusCode();
                    var jsonRepr =
                        await responseRepr.Content.ReadFromJsonAsync<ApiPeruRucRepresentantesResponse>();
                    representantesApiPeru =
                        jsonRepr?.data
                        ?? new List<ApiPeruRucRepresentantesResponse.RucRepresentanteData>();
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with SUNAT data
                _logger.LogError(ex, "Error obteniendo representantes de API Perú");
            }
        }

        // 5. Combinar representantes (SUNAT tiene prioridad)
        var todosLosRepresentantes = new List<ResponseApiRucRepresentante>();

        // Agregar representantes de SUNAT
        todosLosRepresentantes.AddRange(representantesSunat);

        // Agregar representantes de API Perú
        todosLosRepresentantes.AddRange(
            representantesApiPeru.Select(x => new ResponseApiRucRepresentante
            {
                TipoDeDocumento = x.tipo_de_documento,
                NumeroDeDocumento = x.numero_de_documento,
                Nombre = x.nombre,
                Cargo = x.cargo,
                FechaDesde = x.fecha_desde,
            })
        );

        // 6. Armar respuesta
        var response = new ResponseApiRucFull
        {
            Ruc = ruc,
            NombreORazonSocial = sunatData.RazonSocial!,
            Direccion = sunatData.FiscalAddress!,
            Representantes = todosLosRepresentantes,
        };

        // 7. Guardar consulta en la base de datos
        await SaveConsultationAsync(ruc, "RUC", response, sunatData);

        return response;
    }

    public async Task<SunatQueryResponse> ScrapSunat(string ruc)
    {
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = true,
        };
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add(
            "User-Agent",
            """Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36"""
        );
        client.DefaultRequestHeaders.Add("Host", "e-consultaruc.sunat.gob.pe");

        // First request, to get valid cookies
        var sunatUrl =
            "https://e-consultaruc.sunat.gob.pe/cl-ti-itmrconsruc/FrameCriterioBusquedaWeb.jsp";
        var firstRequest = await client.GetAsync(sunatUrl);
        firstRequest.EnsureSuccessStatusCode();

        // Second request, actually fetching SUNAT data
        var sunatToken = GenerateSunatToken(52);
        var formData = new Dictionary<string, string>
        {
            { "accion", "consPorRuc" },
            { "razSoc", "" },
            { "nroRuc", ruc },
            { "nrodoc", "" },
            { "token", sunatToken },
            { "contexto", "ti-it" },
            { "modo", "1" },
            { "rbtnTipo", "1" },
            { "search1", ruc },
            { "tipdoc", "1" },
            { "search2", "" },
            { "search3", "" },
            { "codigo", "" },
        };
        var postResponse = await client.PostAsync(
            "https://e-consultaruc.sunat.gob.pe/cl-ti-itmrconsruc/jcrS00Alias",
            new FormUrlEncodedContent(formData)
        );
        postResponse.EnsureSuccessStatusCode();

        var finalHtml = await postResponse.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(finalHtml);

        // .list-group : div containing the values
        var listGroupElements = doc.DocumentNode.SelectNodes(
            "//*[@class='list-group']//*[@class='list-group-item']"
        );
        if (listGroupElements == null)
        {
            throw new Exception("RUC no encontrado");
        }

        var values = listGroupElements.Select(x => ProcessSunatRow(x));

        var returnData = new SunatQueryResponse();
        foreach (var (title, value) in values)
        {
            switch (title)
            {
                case "Número de RUC:":
                {
                    // value = "20493096436 - TAMATAMA S.A.C."
                    var dashIndex = value.IndexOf("-");
                    if (dashIndex != -1)
                    {
                        returnData.RazonSocial = value.Substring(dashIndex + 1).Trim();
                    }
                    break;
                }
                case "Tipo Contribuyente:":
                {
                    returnData.TipoContribuyente = value;
                    break;
                }
                case "Nombre Comercial:":
                {
                    returnData.NombreComercial = value == "-" ? null : value;
                    break;
                }
                case "Fecha de Inscripción:":
                {
                    returnData.FechaInscripcion = value;
                    break;
                }
                case "Fecha de Inicio de Actividades:":
                {
                    returnData.FechaInicioActividades = value;
                    break;
                }
                case "Estado del Contribuyente:":
                {
                    returnData.Estado = value;
                    break;
                }
                case "Condición del Contribuyente:":
                {
                    returnData.Condicion = value;
                    break;
                }
                case "Domicilio Fiscal:":
                {
                    returnData.FiscalAddress = value;
                    break;
                }
                case "Sistema Emisión de Comprobante:":
                {
                    returnData.SistemaEmisionComprobante = value;
                    break;
                }
                case "Actividad Comercio Exterior:":
                {
                    returnData.ActividadComercioExterior = value;
                    break;
                }
                case "Sistema Contabilidad:":
                {
                    returnData.SistemaContabilidad = value;
                    break;
                }
                case "Actividad(es) Económica(s):":
                {
                    // Procesar actividades principal y secundarias
                    var lines = value
                        .Split('\n')
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrEmpty(line))
                        .ToArray();

                    foreach (var line in lines)
                    {
                        if (line.Contains("Principal -"))
                        {
                            var dashIndex = line.IndexOf("-", line.IndexOf("-") + 1);
                            if (dashIndex != -1)
                            {
                                returnData.ActividadPrincipal = line.Substring(dashIndex + 1)
                                    .Trim();
                            }
                        }
                        else if (line.Contains("Secundaria 1 -"))
                        {
                            var dashIndex = line.IndexOf("-", line.IndexOf("-") + 1);
                            if (dashIndex != -1)
                            {
                                returnData.ActividadSecundaria1 = line.Substring(dashIndex + 1)
                                    .Trim();
                            }
                        }
                        else if (line.Contains("Secundaria 2 -"))
                        {
                            var dashIndex = line.IndexOf("-", line.IndexOf("-") + 1);
                            if (dashIndex != -1)
                            {
                                returnData.ActividadSecundaria2 = line.Substring(dashIndex + 1)
                                    .Trim();
                            }
                        }
                    }
                    break;
                }
                case "Comprobantes de Pago c/aut. de impresión (F. 806 u 816):":
                {
                    returnData.ComprobantesAutorizados = value;
                    break;
                }
                case "Sistema de Emisión Electrónica:":
                {
                    returnData.SistemaEmisionElectronica = value;
                    break;
                }
                case "Emisor electrónico desde:":
                {
                    returnData.EmisorElectronicoDesde = value;
                    break;
                }
                case "Comprobantes Electrónicos:":
                {
                    returnData.ComprobantesElectronicos = value;
                    break;
                }
                case "Afiliado al PLE desde:":
                {
                    returnData.AfiliadoPLEDesde = value == "-" ? null : value;
                    break;
                }
                case "Padrones:":
                {
                    returnData.Padrones = value;
                    break;
                }
            }
        }

        return returnData;
    }

    public (string, string) ProcessSunatRow(HtmlNode node)
    {
        var h4Node = node.SelectSingleNode(".//div[@class='row']/div[@class='col-sm-5']/h4");
        if (h4Node == null)
        {
            return ("", "");
        }
        var titleStr = TrimInsideAndAround(h4Node.InnerText);

        // Try to get a child p
        var pNode = node.SelectSingleNode(".//div[@class='row']/div[@class='col-sm-7']/p");
        if (pNode != null)
        {
            return (titleStr, TrimInsideAndAround(pNode.InnerText));
        }

        // Try to get a child h4
        var rightNode = node.SelectSingleNode(".//div[@class='row']/div[@class='col-sm-7']/h4");
        if (rightNode != null)
        {
            return (titleStr, TrimInsideAndAround(rightNode.InnerText));
        }

        // Try to get a child td - for "Actividades"
        var tdNode = node.SelectSingleNode(
            ".//div[@class='row']/div[@class='col-sm-7']/table/tbody/tr/td"
        );
        if (tdNode != null)
        {
            return (titleStr, TrimInsideAndAround(tdNode.InnerText));
        }

        return (titleStr, "");
    }

    public string GenerateSunatToken(int length)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        char[] result = new char[length];
        Random random = new Random();

        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }

        return new string(result);
    }

    /// <summary>
    /// Web scraping de eldni.com para obtener datos de DNI
    /// </summary>
    public async Task<ResponseApiDni> ScrapEldni(string dni)
    {
        try
        {
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AllowAutoRedirect = true,
            };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36"
            );
            client.DefaultRequestHeaders.Add("Host", "eldni.com");

            // Primera request para obtener cookies y token CSRF
            var initialUrl = "https://eldni.com/pe/buscar-datos-por-dni";
            var initialResponse = await client.GetAsync(initialUrl);
            initialResponse.EnsureSuccessStatusCode();

            var initialHtml = await initialResponse.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(initialHtml);

            // Extraer token CSRF
            var tokenNode = doc.DocumentNode.SelectSingleNode("//input[@name='_token']");
            var csrfToken = tokenNode?.GetAttributeValue("value", "") ?? "";

            if (string.IsNullOrEmpty(csrfToken))
            {
                throw new Exception("No se pudo obtener el token CSRF de eldni.com");
            }

            // Preparar datos del formulario
            var formData = new Dictionary<string, string>
            {
                { "_token", csrfToken },
                { "dni", dni },
            };

            // Enviar POST request con el DNI
            var postResponse = await client.PostAsync(
                "https://eldni.com/pe/buscar-datos-por-dni",
                new FormUrlEncodedContent(formData)
            );
            postResponse.EnsureSuccessStatusCode();

            var finalHtml = await postResponse.Content.ReadAsStringAsync();
            var resultDoc = new HtmlDocument();
            resultDoc.LoadHtml(finalHtml);

            // Buscar si hay resultados
            var resultHeader = resultDoc.DocumentNode.SelectSingleNode(
                "//h3[contains(text(), 'Resultados')]"
            );
            if (resultHeader == null)
            {
                throw new Exception("DNI no encontrado en eldni.com");
            }

            // Extraer datos de la tabla
            var tableRows = resultDoc.DocumentNode.SelectNodes(
                "//table[@class='table table-striped table-scroll']//tbody//tr"
            );
            if (tableRows == null || !tableRows.Any())
            {
                throw new Exception("No se encontraron datos en la tabla de resultados");
            }

            var dataRow = tableRows.First();
            var cells = dataRow.SelectNodes(".//td");
            if (cells == null || cells.Count < 4)
            {
                throw new Exception("Estructura de datos inválida en eldni.com");
            }

            var dniResult = TrimInsideAndAround(cells[0].InnerText);
            var nombres = TrimInsideAndAround(cells[1].InnerText);
            var apellidoPaterno = TrimInsideAndAround(cells[2].InnerText);
            var apellidoMaterno = TrimInsideAndAround(cells[3].InnerText);

            // Verificar que los datos no estén vacíos
            if (string.IsNullOrEmpty(nombres) || string.IsNullOrEmpty(apellidoPaterno))
            {
                throw new Exception("Datos incompletos en eldni.com");
            }

            // Construir nombre completo
            var nombreCompleto = $"{nombres} {apellidoPaterno} {apellidoMaterno}".Trim();

            return new ResponseApiDni { Numero = dniResult, NombreCompleto = nombreCompleto };
        }
        catch (Exception ex)
        {
            throw new Exception($"Error obteniendo datos de eldni.com: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtener representantes legales directamente de SUNAT
    /// </summary>
    public async Task<List<ResponseApiRucRepresentante>> GetRepresentantesSunat(string ruc)
    {
        try
        {
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AllowAutoRedirect = true,
            };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add(
                "User-Agent",
                """Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36"""
            );
            client.DefaultRequestHeaders.Add("Host", "e-consultaruc.sunat.gob.pe");

            // Primera request para obtener cookies
            var sunatUrl =
                "https://e-consultaruc.sunat.gob.pe/cl-ti-itmrconsruc/FrameCriterioBusquedaWeb.jsp";
            await client.GetAsync(sunatUrl);

            // Primero hacer la consulta básica del RUC para obtener los datos principales
            var sunatToken1 = GenerateSunatToken(52);
            var formData1 = new Dictionary<string, string>
            {
                { "accion", "consPorRuc" },
                { "razSoc", "" },
                { "nroRuc", ruc },
                { "nrodoc", "" },
                { "token", sunatToken1 },
                { "contexto", "ti-it" },
                { "modo", "1" },
                { "rbtnTipo", "1" },
                { "search1", ruc },
                { "tipdoc", "1" },
                { "search2", "" },
                { "search3", "" },
                { "codigo", "" },
            };

            await client.PostAsync(
                "https://e-consultaruc.sunat.gob.pe/cl-ti-itmrconsruc/jcrS00Alias",
                new FormUrlEncodedContent(formData1)
            );

            // Ahora hacer la consulta específica de representantes legales
            var formDataRepresentantes = new Dictionary<string, string>
            {
                { "accion", "getRepLeg" },
                { "contexto", "ti-it" },
                { "modo", "1" },
                { "desRuc", "" }, // Se llenará automáticamente con el nombre de la empresa
                { "nroRuc", ruc },
            };

            var representantesResponse = await client.PostAsync(
                "https://e-consultaruc.sunat.gob.pe/cl-ti-itmrconsruc/jcrS00Alias",
                new FormUrlEncodedContent(formDataRepresentantes)
            );

            representantesResponse.EnsureSuccessStatusCode();

            var html = await representantesResponse.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var representantes = new List<ResponseApiRucRepresentante>();

            // Buscar todas las tablas en el HTML
            var tables = doc.DocumentNode.SelectNodes("//table");
            if (tables != null)
            {
                foreach (var table in tables)
                {
                    // Verificar si es la tabla de representantes
                    var headers = table.SelectNodes(".//thead//th | .//tr[1]//th");
                    if (headers != null)
                    {
                        var isRepresentantesTable = false;
                        foreach (var header in headers)
                        {
                            var headerText = header.InnerText.Trim().ToLower();
                            if (
                                headerText.Contains("documento")
                                || headerText.Contains("nombre")
                                || headerText.Contains("cargo")
                            )
                            {
                                isRepresentantesTable = true;
                                break;
                            }
                        }

                        if (isRepresentantesTable)
                        {
                            // Procesar filas de la tabla
                            var rows = table.SelectNodes(".//tbody//tr");
                            if (rows != null)
                            {
                                foreach (var row in rows)
                                {
                                    var cells = row.SelectNodes(".//td");
                                    if (cells != null && cells.Count >= 3)
                                    {
                                        var tipoDoc = cells[0].InnerText.Trim();
                                        var numeroDoc = cells[1].InnerText.Trim();
                                        var nombre = cells[2].InnerText.Trim();
                                        var cargo =
                                            cells.Count > 3 ? cells[3].InnerText.Trim() : "";
                                        var fecha =
                                            cells.Count > 4 ? cells[4].InnerText.Trim() : "";

                                        // Verificar que no sean headers y que tengan contenido válido
                                        if (
                                            !string.IsNullOrEmpty(nombre)
                                            && nombre != "Nombre"
                                            && !string.IsNullOrEmpty(numeroDoc)
                                            && numeroDoc != "Nro. Documento"
                                            && tipoDoc != "Documento"
                                        )
                                        {
                                            representantes.Add(
                                                new ResponseApiRucRepresentante
                                                {
                                                    TipoDeDocumento = string.IsNullOrEmpty(tipoDoc)
                                                        ? "DNI"
                                                        : tipoDoc,
                                                    NumeroDeDocumento = numeroDoc,
                                                    Nombre = nombre.Trim(),
                                                    Cargo = string.IsNullOrEmpty(cargo)
                                                        ? "REPRESENTANTE LEGAL"
                                                        : cargo,
                                                    FechaDesde = fecha,
                                                }
                                            );
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return representantes;
        }
        catch (Exception ex)
        {
            return new List<ResponseApiRucRepresentante>();
        }
    }

    private string TrimInsideAndAround(string input)
    {
        input = input.Trim();
        input = Regex.Replace(input, "&aacute;", "á");
        input = Regex.Replace(input, "&eacute;", "é");
        input = Regex.Replace(input, "&iacute;", "í");
        input = Regex.Replace(input, "&oacute;", "ó");
        input = Regex.Replace(input, "&uacute;", "ú");
        input = Regex.Replace(input, @"\s+", " ");
        return input;
    }

    public class SunatQueryResponse
    {
        public string? RazonSocial { get; set; }
        public string? TipoContribuyente { get; set; }
        public string? NombreComercial { get; set; }
        public string? FechaInscripcion { get; set; }
        public string? FechaInicioActividades { get; set; }
        public string? Estado { get; set; }
        public string? Condicion { get; set; }
        public string? FiscalAddress { get; set; }
        public string? SistemaEmisionComprobante { get; set; }
        public string? ActividadComercioExterior { get; set; }
        public string? SistemaContabilidad { get; set; }
        public string? ActividadPrincipal { get; set; }
        public string? ActividadSecundaria1 { get; set; }
        public string? ActividadSecundaria2 { get; set; }
        public string? ComprobantesAutorizados { get; set; }
        public string? SistemaEmisionElectronica { get; set; }
        public string? EmisorElectronicoDesde { get; set; }
        public string? ComprobantesElectronicos { get; set; }
        public string? AfiliadoPLEDesde { get; set; }
        public string? Padrones { get; set; }

        // Campos legacy para compatibilidad
        public string? Name => NombreComercial;
        public string? BusinessType => ActividadPrincipal;
        public string? ContactName { get; set; }
    }

    // Clases internas para deserializar la respuesta
    private class ApiPeruRucResponse
    {
        public RucData data { get; set; }

        public class RucData
        {
            public required string ruc { get; set; }
            public required string nombre_o_razon_social { get; set; }
            public required string direccion { get; set; }
            public required string direccion_completa { get; set; }
            public required string estado { get; set; }
            public required string condicion { get; set; }
            public required string departamento { get; set; }
            public required string provincia { get; set; }
            public required string distrito { get; set; }
            public required string ubigeo_sunat { get; set; }
            public required string[] ubigeo { get; set; }
            public required string es_agente_de_retencion { get; set; }
            public required string es_buen_contribuyente { get; set; }
        }
    }

    private class ApiPeruRucRepresentantesResponse
    {
        public required List<RucRepresentanteData> data { get; set; }

        public class RucRepresentanteData
        {
            public required string tipo_de_documento { get; set; }
            public required string numero_de_documento { get; set; }
            public required string nombre { get; set; }
            public required string cargo { get; set; }
            public required string fecha_desde { get; set; }
        }
    }

    public async Task<ResponseApiDni> GetDataByDniAsync(string dni)
    {
        // 1. Buscar consulta previa en la base de datos (ApiPeruConsultation)
        var existingConsultation = await _context.ApiPeruConsultations.FirstOrDefaultAsync(c =>
            c.DocumentNumber == dni && c.DocumentType == "DNI"
        );

        if (existingConsultation != null)
        {
            // Si existe una consulta previa, devolver los datos guardados
            var cachedData = existingConsultation.GetResponseData<ResponseApiDni>();
            if (cachedData != null)
            {
                return cachedData;
            }
        }

        // 2. Buscar en la base de datos de clientes
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Dni == dni);
        if (client != null && !string.IsNullOrWhiteSpace(client.Name))
        {
            var clientData = new ResponseApiDni
            {
                Numero = client.Dni!,
                NombreCompleto = client.Name,
            };

            // Guardar consulta en la base de datos
            await SaveConsultationAsync(dni, "DNI", clientData);
            return clientData;
        }

        // 3. Web scraping de eldni.com
        try
        {
            var eldniData = await ScrapEldni(dni);

            // Guardar consulta en la base de datos
            await SaveConsultationAsync(dni, "DNI", eldniData);
            return eldniData;
        }
        catch { }

        // 4. Como última opción, consultar API Perú
        if (string.IsNullOrWhiteSpace(_config.Token))
        {
            throw new Exception("API Peru token is not configured");
        }

        try
        {
            var url = $"{_config.BaseUrl}/dni/{dni}?api_token={_config.Token}";

            var httpResponse = await _httpClient.GetAsync(url);
            httpResponse.EnsureSuccessStatusCode();

            var json = await httpResponse.Content.ReadFromJsonAsync<ApiPeruDniResponse>();
            var data = json?.data;

            if (
                data == null
                || string.IsNullOrWhiteSpace(data.numero)
                || string.IsNullOrWhiteSpace(data.nombre_completo)
            )
            {
                throw new Exception("DNI no encontrado en API Perú.");
            }

            var response = new ResponseApiDni
            {
                Numero = data.numero,
                NombreCompleto = data.nombre_completo,
            };

            // Guardar consulta en la base de datos
            await SaveConsultationAsync(dni, "DNI", response);
            return response;
        }
        catch
        {
            throw new Exception($"DNI {dni} no encontrado en ninguna fuente disponible.");
        }
    }

    // Clase interna para deserializar la respuesta del DNI
    private class ApiPeruDniResponse
    {
        public required DniData data { get; set; }

        public class DniData
        {
            public required string numero { get; set; }
            public required string nombre_completo { get; set; }
        }
    }

    /// <summary>
    /// Guarda una consulta de API Perú en la base de datos
    /// </summary>
    private async Task SaveConsultationAsync<T>(
        string documentNumber,
        string documentType,
        T responseData,
        SunatQueryResponse? sunatData = null
    )
    {
        try
        {
            var consultation = new ApiPeruConsultation
            {
                DocumentNumber = documentNumber,
                DocumentType = documentType,
                ConsultedAt = DateTime.UtcNow,
            };

            // Serializar la respuesta
            consultation.SetResponseData(responseData);

            // Extraer información adicional para facilitar búsquedas
            if (responseData is ResponseApiRucFull rucData)
            {
                consultation.CompanyName = rucData.NombreORazonSocial;
                consultation.Address = rucData.Direccion;
                consultation.Status = rucData.Estado;
                consultation.Condition = rucData.Condicion;
            }
            else if (responseData is ResponseApiDni dniData)
            {
                consultation.PersonName = dniData.NombreCompleto;
            }

            // Agregar información adicional de SUNAT si está disponible
            if (sunatData != null)
            {
                if (string.IsNullOrEmpty(consultation.CompanyName))
                    consultation.CompanyName = sunatData.RazonSocial;
                if (string.IsNullOrEmpty(consultation.Address))
                    consultation.Address = sunatData.FiscalAddress;
                if (string.IsNullOrEmpty(consultation.Status))
                    consultation.Status = sunatData.Estado;
                if (string.IsNullOrEmpty(consultation.Condition))
                    consultation.Condition = sunatData.Condicion;
            }

            _context.ApiPeruConsultations.Add(consultation);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error guardando consulta de {DocumentType} {DocumentNumber}",
                documentType,
                documentNumber
            );
            // No lanzar excepción para no interrumpir el flujo principal
        }
    }

    /// <summary>
    /// Obtiene el historial de consultas para un documento
    /// </summary>
    public async Task<List<ApiPeruConsultation>> GetConsultationHistoryAsync(
        string documentNumber,
        string documentType
    )
    {
        return await _context
            .ApiPeruConsultations.Where(c =>
                c.DocumentNumber == documentNumber && c.DocumentType == documentType
            )
            .OrderByDescending(c => c.ConsultedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Limpia consultas antiguas (más de 30 días)
    /// </summary>
    public async Task<int> CleanOldConsultationsAsync(int daysOld = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
        var oldConsultations = await _context
            .ApiPeruConsultations.Where(c => c.ConsultedAt < cutoffDate)
            .ToListAsync();

        if (oldConsultations.Any())
        {
            _context.ApiPeruConsultations.RemoveRange(oldConsultations);
            await _context.SaveChangesAsync();
        }

        return oldConsultations.Count;
    }
}
