namespace GestionHogar.Model;

public class PaginatedResponse<T>
{
    public required List<T> Items { get; set; }
    public required int Page { get; set; }
    public required int PageSize { get; set; }
    public required int TotalCount { get; set; }
    public required int TotalPages { get; set; }
}
