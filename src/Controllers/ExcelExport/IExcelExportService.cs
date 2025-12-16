using System.Collections.Generic;

namespace GestionHogar.Services;

public interface IExcelExportService
{
    byte[] GenerateExcel(string title, List<string> headers, List<List<object>> data);
    byte[] GenerateExcel(
        string title,
        List<string> headers,
        List<List<object>> data,
        bool expandComplexDataHorizontally
    );
    byte[] GenerateExcel(
        string title,
        List<string> headers,
        List<List<object>> data,
        bool expandComplexDataHorizontally,
        List<int> complexDataColumnIndexes
    );
}
