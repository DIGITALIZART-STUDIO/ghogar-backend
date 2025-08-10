using System.Collections.Generic;

namespace GestionHogar.Controllers.ExcelExport.Dto;

public class ExcelExportRequestDto
{
    public required string Title { get; set; }
    public required List<string> Headers { get; set; }
    public required List<List<object>> Data { get; set; }
}
