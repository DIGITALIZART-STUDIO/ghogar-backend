using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class PaginationService
{
    public async Task<PaginatedResponseV2<T>> PaginateAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize
    )
    {
        var total = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var meta = new PaginationMetadata
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasNext = page < totalPages,
            HasPrevious = page > 1,
        };

        return new PaginatedResponseV2<T> { Data = data, Meta = meta };
    }
}
