namespace GestionHogar.Controllers.Client;

public class ResponseApiRucFull
{
    // Datos del RUC
    public string Ruc { get; set; }
    public string NombreORazonSocial { get; set; }
    public string Direccion { get; set; }
    public string DireccionCompleta { get; set; }
    public string Estado { get; set; }
    public string Condicion { get; set; }
    public string Departamento { get; set; }
    public string Provincia { get; set; }
    public string Distrito { get; set; }
    public string UbigeoSunat { get; set; }
    public string[] Ubigeo { get; set; }
    public string EsAgenteDeRetencion { get; set; }
    public string EsBuenContribuyente { get; set; }

    // Representantes legales
    public List<ResponseApiRucRepresentante> Representantes { get; set; }
}

public class ResponseApiRucRepresentante
{
    public string TipoDeDocumento { get; set; }
    public string NumeroDeDocumento { get; set; }
    public string Nombre { get; set; }
    public string Cargo { get; set; }
    public string FechaDesde { get; set; }
}

public class ResponseApiDni
{
    public string Numero { get; set; }
    public string NombreCompleto { get; set; }
}
