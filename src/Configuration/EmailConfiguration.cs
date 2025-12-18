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
    public string LogoUrl { get; set; } =
        "https://scontent.faqp3-1.fna.fbcdn.net/v/t39.30808-6/485004919_1870724740411949_5302595946039607473_n.jpg?_nc_cat=100&ccb=1-7&_nc_sid=6ee11a&_nc_eui2=AeE517JJG7mLPZuWl3nRHky_glo8zxE5LuSCWjzPETku5FzDXhnUQ5Crbz6zf8VoJOG9zANQa7h5z7-NAqBX7cTT&_nc_ohc=BSUFXyuADGAQ7kNvwGMbOiF&_nc_oc=Adlcupl3dLIImP7FRUvcxDp-trtgOQlPNJ9tt1dO1lIsEYkAPW1Cmq1VIc1jJ1t1F_XIRGTEp7x2wzVt5bMCo_4s&_nc_zt=23&_nc_ht=scontent.faqp3-1.fna&_nc_gid=ys4cPe7wyLqB5Ac0iGnTLQ&oh=00_AfUuTHt0-RQXEE_83JFyPip0xGLCx6ObnxrmIX8Giy_Uyw&oe=68A7DE56";
}
