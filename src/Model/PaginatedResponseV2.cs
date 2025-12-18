using System.Collections.Generic;

namespace GestionHogar.Model;

public class PaginatedResponseV2<T>
{
    public List<T> Data { get; set; } = new();
    public PaginationMetadata Meta { get; set; } = new();
}
