using GestionHogar.Model;

public class DashboardService
{
    private readonly DatabaseContext _db;

    public DashboardService(DatabaseContext db)
    {
        _db = db;
    }

    public async Task<DashboardAdminDto> GetDashboardAdminDataAsync(int? year = null)
    {
        var useCase = new GetDashboardAdminDataUseCase(_db);
        return await useCase.ExecuteAsync(year);
    }
}
