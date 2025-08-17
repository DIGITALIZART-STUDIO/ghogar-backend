namespace GestionHogar.Configuration;

public class EmailConfiguration
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public bool UseDefaultCredentials { get; set; } = false;
}

public class BusinessInfo
{
    public string Business { get; set; } = "Gestion Hogar";
    public string Url { get; set; } = "https://gestionhogar-frontend-develop.araozu.dev/";
    public string Phone { get; set; } = "977 759 910";
    public string Address { get; set; } = "Coop. La Alborada D-3, Cerro Colorado, Arequipa, Peru";
    public string Contact { get; set; } = "informes@gestionhogarinmobiliaria.com";
}
