using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace GestionHogar.Services;

public class ExcelExportService : IExcelExportService
{
    public byte[] GenerateExcel(string title, List<string> headers, List<List<object>> data)
    {
        return GenerateExcel(title, headers, data, false);
    }

    public byte[] GenerateExcel(
        string title,
        List<string> headers,
        List<List<object>> data,
        bool expandComplexDataHorizontally
    )
    {
        return GenerateExcel(title, headers, data, expandComplexDataHorizontally, new List<int>());
    }

    public byte[] GenerateExcel(
        string title,
        List<string> headers,
        List<List<object>> data,
        bool expandComplexDataHorizontally,
        List<int> complexDataColumnIndexes
    )
    {
        using var stream = new MemoryStream();
        using (
            var spreadsheetDocument = SpreadsheetDocument.Create(
                stream,
                SpreadsheetDocumentType.Workbook
            )
        )
        {
            var workbookPart = spreadsheetDocument.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            CreateAndAddWorkbookStyles(workbookPart);

            var sharedStringTablePart = workbookPart.AddNewPart<SharedStringTablePart>();
            sharedStringTablePart.SharedStringTable = new SharedStringTable();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(
                new Sheet()
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Reporte",
                }
            );

            // Calcular el número total de columnas según el modo
            var totalColumns = expandComplexDataHorizontally
                ? CalculateTotalColumnsWithExpansion(headers, data, complexDataColumnIndexes)
                : headers.Count;

            // 1. TÍTULO PRINCIPAL MEJORADO - Sin logo, solo título elegante
            var titleRow = new Row
            {
                RowIndex = 1,
                Height = 50, // Más alto para impacto visual
                CustomHeight = true,
            };
            sheetData.AppendChild(titleRow);

            // Título centrado en toda la fila (A1 hasta última columna)
            var titleCell = new Cell
            {
                CellReference = "A1",
                StyleIndex = 2, // Estilo de título elegante
                DataType = CellValues.String,
                CellValue = new CellValue(title ?? "Reporte de Datos"),
            };
            titleRow.AppendChild(titleCell);

            // Fusionar TODA la fila para el título
            var mergeCells = new MergeCells();
            string lastCol = GetColumnName(totalColumns);
            mergeCells.Append(new MergeCell { Reference = $"A1:{lastCol}1" });
            worksheetPart.Worksheet.AppendChild(mergeCells);

            // 2. Encabezados de columnas
            var headerRow = new Row
            {
                RowIndex = 2, // Cambió de 3 a 2 (no hay espacio vacío)
                Height = 30,
                CustomHeight = true,
            };
            sheetData.AppendChild(headerRow);

            if (expandComplexDataHorizontally)
            {
                // Generar encabezados dinámicos basándose en los datos reales
                var dynamicHeaders = GetDynamicHeaders(headers, data, complexDataColumnIndexes);

                for (int i = 0; i < dynamicHeaders.Count; i++)
                {
                    var cell = new Cell
                    {
                        CellReference = GetColumnName(i + 1) + "2",
                        DataType = CellValues.String,
                        CellValue = new CellValue(dynamicHeaders[i]),
                        StyleIndex = 3, // Headers con amarillo corporativo
                    };
                    headerRow.AppendChild(cell);
                }
            }
            else
            {
                // Encabezados normales sin expansión
                for (int i = 0; i < headers.Count; i++)
                {
                    var cell = new Cell
                    {
                        CellReference = GetColumnName(i + 1) + "2",
                        DataType = CellValues.String,
                        CellValue = new CellValue(headers[i]),
                        StyleIndex = 3, // Headers con amarillo corporativo
                    };
                    headerRow.AppendChild(cell);
                }
            }

            // 3. Datos con alternancia sutil
            uint currentRowIndex = 3; // Comenzar en fila 3
            for (int i = 0; i < data.Count; i++)
            {
                var processedRows = expandComplexDataHorizontally
                    ? ProcessComplexDataHorizontal(
                        data[i],
                        headers,
                        currentRowIndex,
                        sheetData,
                        i,
                        totalColumns,
                        complexDataColumnIndexes
                    )
                    : ProcessComplexData(data[i], headers, currentRowIndex, sheetData, i);
                currentRowIndex += (uint)processedRows;
            }

            // 4. Ajustar anchos de columna - más generosos
            var columns = new Columns();
            for (int i = 1; i <= totalColumns; i++)
            {
                columns.Append(
                    new Column
                    {
                        Min = (uint)i,
                        Max = (uint)i,
                        Width = 18, // Ancho uniforme para todas las columnas
                        CustomWidth = true,
                    }
                );
            }
            worksheetPart.Worksheet.InsertAt(columns, 0);

            // Aplicar estilos consistentes
            ApplyStylesToWorksheet(worksheetPart.Worksheet);

            workbookPart.Workbook.Save();
        }
        return stream.ToArray();
    }

    // NUEVO: Procesar datos complejos (objetos, arrays, etc.) como filas expandidas
    private int ProcessComplexData(
        List<object> rowData,
        List<string> headers,
        uint startRowIndex,
        SheetData sheetData,
        int originalRowIndex
    )
    {
        var totalRowsAdded = 0;

        // 1. SIEMPRE agregar la fila principal con todos los datos normales
        var mainRow = new Row { RowIndex = startRowIndex };
        sheetData.AppendChild(mainRow);

        uint mainStyleIndex = ((originalRowIndex + 1) % 2 == 0) ? 4U : 5U;

        for (int j = 0; j < headers.Count; j++)
        {
            var value = "";
            if (j < rowData.Count)
            {
                // Solo mostrar valores simples en la fila principal
                value = FormatMainRowValue(rowData[j]);
            }

            var cell = new Cell
            {
                CellReference = GetColumnName(j + 1) + startRowIndex,
                DataType = CellValues.String,
                CellValue = new CellValue(value),
                StyleIndex = mainStyleIndex,
            };
            mainRow.AppendChild(cell);
        }
        totalRowsAdded = 1;

        // 2. OPCIONAL: Agregar filas expandidas solo si hay datos complejos
        var currentExpandedRowIndex = startRowIndex + 1;
        for (int j = 0; j < rowData.Count && j < headers.Count; j++)
        {
            var expandedRowsList = GetExpandedRowsForComplexData(rowData[j], headers[j], headers);
            if (expandedRowsList.Count > 0)
            {
                foreach (var expandedRowData in expandedRowsList)
                {
                    var expandedRow = new Row { RowIndex = currentExpandedRowIndex };
                    sheetData.AppendChild(expandedRow);

                    // Crear celdas para la fila expandida
                    for (int k = 0; k < headers.Count; k++)
                    {
                        var cellValue = k < expandedRowData.Count ? expandedRowData[k] : "";

                        // Determinar el estilo según el contenido
                        uint cellStyleIndex = 6U; // Estilo para filas expandidas
                        if (cellValue.StartsWith("→"))
                        {
                            cellStyleIndex = 7U; // Nuevo estilo para títulos de sección
                        }

                        var cell = new Cell
                        {
                            CellReference = GetColumnName(k + 1) + currentExpandedRowIndex,
                            DataType = CellValues.String,
                            CellValue = new CellValue(cellValue),
                            StyleIndex = cellStyleIndex,
                        };
                        expandedRow.AppendChild(cell);
                    }

                    currentExpandedRowIndex++;
                    totalRowsAdded++;
                }
            }
        }

        return totalRowsAdded;
    }

    // NUEVO: Calcular el total de columnas considerando la expansión horizontal
    private int CalculateTotalColumnsWithExpansion(
        List<string> headers,
        List<List<object>> data,
        List<int> complexDataColumnIndexes
    )
    {
        // Contar campos simples (no complejos)
        var simpleFieldsCount = 0;
        for (int i = 0; i < headers.Count; i++)
        {
            if (!complexDataColumnIndexes.Contains(i))
            {
                simpleFieldsCount++;
            }
        }

        // Contar propiedades máximas de campos complejos
        var maxComplexProperties = 0;

        // Examinar TODAS las filas para obtener el máximo de propiedades
        foreach (var complexIndex in complexDataColumnIndexes)
        {
            var maxPropsForThisField = 0;

            foreach (var row in data)
            {
                if (complexIndex < row.Count)
                {
                    var properties = GetComplexDataProperties(row[complexIndex] ?? "");
                    maxPropsForThisField = Math.Max(maxPropsForThisField, properties.Count);
                }
            }

            maxComplexProperties += maxPropsForThisField;
        }

        var totalColumns = simpleFieldsCount + maxComplexProperties;

        Console.WriteLine(
            $"[DEBUG] Cálculo columnas: Simples={simpleFieldsCount}, Complejas={maxComplexProperties}, Total={totalColumns}"
        );

        return totalColumns; // NO usar Math.Max, usar el cálculo real
    }

    // NUEVO: Generar encabezados dinámicos para expansión horizontal
    private List<string> GetDynamicHeaders(
        List<string> headers,
        List<List<object>> data,
        List<int> complexDataColumnIndexes
    )
    {
        var dynamicHeaders = new List<string>();

        // Agregar encabezados de campos simples
        for (int i = 0; i < headers.Count; i++)
        {
            if (!complexDataColumnIndexes.Contains(i))
            {
                dynamicHeaders.Add(headers[i]);
            }
        }

        // Para campos complejos, agregar encabezados genéricos basados en las propiedades encontradas
        foreach (var complexIndex in complexDataColumnIndexes.OrderBy(x => x))
        {
            var fieldName =
                complexIndex < headers.Count ? headers[complexIndex] : $"Campo{complexIndex}";

            // Encontrar el máximo número de propiedades para este campo
            var maxProperties = 0;
            foreach (var row in data)
            {
                if (complexIndex < row.Count)
                {
                    var properties = GetComplexDataProperties(row[complexIndex] ?? "");
                    maxProperties = Math.Max(maxProperties, properties.Count);
                }
            }

            // Agregar encabezados genéricos para este campo complejo
            for (int propIndex = 1; propIndex <= maxProperties; propIndex++)
            {
                dynamicHeaders.Add($"{fieldName} {propIndex}");
            }
        }

        Console.WriteLine(
            $"[DEBUG] Encabezados dinámicos generados: {string.Join(", ", dynamicHeaders)}"
        );

        return dynamicHeaders;
    }

    // NUEVO: Procesar datos con expansión horizontal
    private int ProcessComplexDataHorizontal(
        List<object> rowData,
        List<string> headers,
        uint startRowIndex,
        SheetData sheetData,
        int originalRowIndex,
        int totalColumns,
        List<int> complexDataColumnIndexes
    )
    {
        var mainRow = new Row { RowIndex = startRowIndex };
        sheetData.AppendChild(mainRow);

        uint mainStyleIndex = ((originalRowIndex + 1) % 2 == 0) ? 4U : 5U;
        var currentColumn = 1;

        Console.WriteLine(
            $"[DEBUG] Procesando fila {originalRowIndex + 1}, datos: {rowData.Count}"
        );

        // Procesar campos simples primero
        for (int i = 0; i < rowData.Count && i < headers.Count; i++)
        {
            if (!complexDataColumnIndexes.Contains(i))
            {
                var value = rowData[i];

                Console.WriteLine(
                    $"[DEBUG] Campo simple {i}: {headers[i]} = {value?.GetType().Name} en columna {currentColumn}"
                );

                if (currentColumn <= totalColumns)
                {
                    var cell = new Cell
                    {
                        CellReference = GetColumnName(currentColumn) + startRowIndex,
                        DataType = CellValues.String,
                        CellValue = new CellValue(FormatMainRowValue(value)),
                        StyleIndex = mainStyleIndex,
                    };
                    mainRow.AppendChild(cell);
                    currentColumn++;
                }
            }
        }

        // Procesar campos complejos expandidos horizontalmente
        foreach (var complexIndex in complexDataColumnIndexes.OrderBy(x => x))
        {
            if (complexIndex < rowData.Count)
            {
                var value = rowData[complexIndex];
                var headerName =
                    complexIndex < headers.Count ? headers[complexIndex] : $"Campo{complexIndex}";

                Console.WriteLine(
                    $"[DEBUG] Campo complejo {complexIndex}: {headerName} = {value?.GetType().Name}, Valor = {value}"
                );

                var properties = GetComplexDataProperties(value ?? "");

                Console.WriteLine($"[DEBUG] Propiedades encontradas: {properties.Count}");

                if (properties.Count > 0)
                {
                    foreach (var property in properties)
                    {
                        Console.WriteLine($"[DEBUG] Propiedad: {property.Key} = {property.Value}");
                    }
                }

                foreach (var property in properties)
                {
                    if (currentColumn <= totalColumns)
                    {
                        Console.WriteLine(
                            $"[DEBUG] Añadiendo propiedad: {property.Key} = {property.Value} en columna {currentColumn}"
                        );

                        var cell = new Cell
                        {
                            CellReference = GetColumnName(currentColumn) + startRowIndex,
                            DataType = CellValues.String,
                            CellValue = new CellValue(property.Value),
                            StyleIndex = mainStyleIndex,
                        };
                        mainRow.AppendChild(cell);
                        currentColumn++;
                    }
                }
            }
        }

        // Rellenar celdas vacías hasta completar totalColumns
        while (currentColumn <= totalColumns)
        {
            var cell = new Cell
            {
                CellReference = GetColumnName(currentColumn) + startRowIndex,
                DataType = CellValues.String,
                CellValue = new CellValue(""),
                StyleIndex = mainStyleIndex,
            };
            mainRow.AppendChild(cell);
            currentColumn++;
        }

        return 1; // Solo una fila principal
    } // NUEVO: Verificar si un valor es complejo

    // NUEVO: Obtener propiedades de datos complejos
    private List<KeyValuePair<string, string>> GetComplexDataProperties(object value)
    {
        var properties = new List<KeyValuePair<string, string>>();

        if (value == null)
            return properties;

        try
        {
            if (value is JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var prop in jsonElement.EnumerateObject())
                        {
                            properties.Add(
                                new KeyValuePair<string, string>(
                                    prop.Name,
                                    FormatJsonElementValue(prop.Value)
                                )
                            );
                        }
                        break;

                    case JsonValueKind.Array:
                        var arrayItems = jsonElement.EnumerateArray().ToList();
                        for (int i = 0; i < arrayItems.Count; i++)
                        {
                            if (arrayItems[i].ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in arrayItems[i].EnumerateObject())
                                {
                                    properties.Add(
                                        new KeyValuePair<string, string>(
                                            $"{prop.Name}[{i + 1}]",
                                            FormatJsonElementValue(prop.Value)
                                        )
                                    );
                                }
                            }
                            else
                            {
                                properties.Add(
                                    new KeyValuePair<string, string>(
                                        $"Item[{i + 1}]",
                                        FormatJsonElementValue(arrayItems[i])
                                    )
                                );
                            }
                        }
                        break;
                }
            }
            // Para otros tipos IEnumerable con contenido
            else if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var items = enumerable.Cast<object>().ToList();
                for (int i = 0; i < items.Count; i++)
                {
                    properties.Add(
                        new KeyValuePair<string, string>(
                            $"Item[{i + 1}]",
                            items[i]?.ToString() ?? ""
                        )
                    );
                }
            }
        }
        catch
        {
            // Si hay error, retornar lista vacía
        }

        return properties;
    }

    // NUEVO: Formatear valores para la fila principal MEJORADO - Manejo de nulls
    private string FormatMainRowValue(object? value)
    {
        // Manejo explícito de null y valores especiales
        if (value == null)
            return "";

        try
        {
            // String - manejo de nulls y strings especiales
            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str))
                    return "";

                // Limpiar strings con caracteres especiales Unicode
                str = str.Replace("\u002B", "+").Replace("\u00ED", "í").Replace("\u00CD", "Í");
                return str.Trim();
            }

            // Números - formato consistente
            if (value is int || value is decimal || value is double || value is float)
            {
                return value.ToString() ?? "";
            }

            // Boolean - formato consistente
            if (value is bool boolValue)
            {
                return boolValue ? "Sí" : "No";
            }

            // JsonElement - mostrar mensajes amigables en fila principal
            if (value is JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.String:
                        var jsonStr = jsonElement.GetString() ?? "";
                        if (string.IsNullOrWhiteSpace(jsonStr))
                            return "";
                        // Limpiar caracteres especiales
                        jsonStr = jsonStr.Replace("\u002B", "+").Replace("\u00ED", "í");
                        return jsonStr.Trim();
                    case JsonValueKind.Number:
                        return jsonElement.ToString();
                    case JsonValueKind.True:
                        return "Sí";
                    case JsonValueKind.False:
                        return "No";
                    case JsonValueKind.Null:
                        return "";
                    case JsonValueKind.Object:
                        var propCount = jsonElement.EnumerateObject().Count();
                        return propCount > 0 ? "✓ Ver detalles abajo" : "";
                    case JsonValueKind.Array:
                        var arrayCount = jsonElement.GetArrayLength();
                        return arrayCount > 0 ? "✓ Ver lista abajo" : "";
                    default:
                        return jsonElement.ToString();
                }
            }

            // Arrays o Lists - mostrar mensajes amigables en fila principal
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var count = enumerable.Cast<object>().Count();
                return count > 0 ? "✓ Ver lista abajo" : "";
            }

            // Otros objetos - conversión segura
            var result = value.ToString();
            if (string.IsNullOrWhiteSpace(result))
                return "";

            // Limpiar caracteres especiales de cualquier otro tipo
            result = result.Replace("\u002B", "+").Replace("\u00ED", "í").Replace("\u00CD", "Í");
            return result.Trim();
        }
        catch
        {
            // En caso de error, retornar string vacío en lugar de valor crudo
            return "";
        }
    }

    // NUEVO: Obtener filas expandidas para datos complejos (MEJORADO)
    private List<List<string>> GetExpandedRowsForComplexData(
        object value,
        string headerName,
        List<string> headers
    )
    {
        var expandedRows = new List<List<string>>();

        if (value == null)
            return expandedRows;

        try
        {
            // Solo expandir JsonElement objetos/arrays
            if (value is JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.Object:
                        var properties = jsonElement.EnumerateObject().ToList();
                        if (properties.Count > 0)
                        {
                            // Fila de título
                            var titleRow = new List<string>();
                            titleRow.Add(""); // Columna A vacía
                            titleRow.Add($"→ {headerName}:"); // Columna B con título
                            for (int i = 2; i < headers.Count; i++)
                            {
                                titleRow.Add(""); // Resto de columnas vacías
                            }
                            expandedRows.Add(titleRow);

                            // Fila con datos del objeto - CADA PROPIEDAD EN UNA COLUMNA SEPARADA
                            var dataRow = new List<string>();
                            dataRow.Add(""); // Columna A vacía

                            // Empezar desde la columna B (índice 1) y poner cada propiedad en una columna
                            var columnIndex = 1;
                            foreach (var prop in properties)
                            {
                                if (columnIndex < headers.Count)
                                {
                                    dataRow.Add(
                                        $"{prop.Name}: {FormatJsonElementValue(prop.Value)}"
                                    );
                                    columnIndex++;
                                }
                            }

                            // Completar con celdas vacías hasta el final
                            while (dataRow.Count < headers.Count)
                            {
                                dataRow.Add("");
                            }

                            expandedRows.Add(dataRow);
                        }
                        break;

                    case JsonValueKind.Array:
                        var arrayItems = jsonElement.EnumerateArray().ToList();
                        if (arrayItems.Count > 0)
                        {
                            // Fila de título
                            var titleRow = new List<string>();
                            titleRow.Add(""); // Columna A vacía
                            titleRow.Add($"→ {headerName}:"); // Columna B con título
                            for (int i = 2; i < headers.Count; i++)
                            {
                                titleRow.Add(""); // Resto de columnas vacías
                            }
                            expandedRows.Add(titleRow);

                            // Una fila por cada elemento del array - CADA PROPIEDAD EN UNA COLUMNA SEPARADA
                            for (int itemIndex = 0; itemIndex < arrayItems.Count; itemIndex++)
                            {
                                var itemRow = new List<string>();
                                itemRow.Add(""); // Columna A vacía

                                if (arrayItems[itemIndex].ValueKind == JsonValueKind.Object)
                                {
                                    // Para objetos en el array, expandir propiedades en columnas
                                    var objProps = arrayItems[itemIndex].EnumerateObject().ToList();
                                    var columnIndex = 1;

                                    foreach (var prop in objProps)
                                    {
                                        if (columnIndex < headers.Count)
                                        {
                                            itemRow.Add(
                                                $"{prop.Name}: {FormatJsonElementValue(prop.Value)}"
                                            );
                                            columnIndex++;
                                        }
                                    }

                                    // Completar con celdas vacías hasta el final
                                    while (itemRow.Count < headers.Count)
                                    {
                                        itemRow.Add("");
                                    }
                                }
                                else
                                {
                                    // Para valores simples en el array
                                    itemRow.Add(
                                        $"[{itemIndex + 1}] {FormatJsonElementValue(arrayItems[itemIndex])}"
                                    );

                                    // Completar con celdas vacías hasta el final
                                    while (itemRow.Count < headers.Count)
                                    {
                                        itemRow.Add("");
                                    }
                                }

                                expandedRows.Add(itemRow);
                            }
                        }
                        break;
                }
            }
            // Arrays o Lists normales
            else if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var items = enumerable.Cast<object>().ToList();
                if (items.Count > 0)
                {
                    // Fila de título
                    var titleRow = new List<string>();
                    titleRow.Add(""); // Columna A vacía
                    titleRow.Add($"→ {headerName}:"); // Columna B con título
                    for (int i = 2; i < headers.Count; i++)
                    {
                        titleRow.Add(""); // Resto de columnas vacías
                    }
                    expandedRows.Add(titleRow);

                    // Una fila por cada elemento - distribuir en columnas
                    for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                    {
                        var itemRow = new List<string>();
                        itemRow.Add(""); // Columna A vacía
                        itemRow.Add($"[{itemIndex + 1}] {items[itemIndex]?.ToString() ?? ""}"); // Columna B con datos

                        // Completar con celdas vacías hasta el final
                        while (itemRow.Count < headers.Count)
                        {
                            itemRow.Add("");
                        }

                        expandedRows.Add(itemRow);
                    }
                }
            }
        }
        catch
        {
            // Si hay error, no expandir
        }

        return expandedRows;
    }

    // NUEVO: Formatear valores de JsonElement para filas expandidas
    private string FormatJsonElementValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString() ?? "";
            case JsonValueKind.Number:
                return element.ToString();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean().ToString();
            case JsonValueKind.Object:
                var props = element.EnumerateObject().ToList();
                if (props.Count == 0)
                    return "{}";
                if (props.Count == 1)
                    return $"{props[0].Name}: {FormatJsonElementValue(props[0].Value)}";
                return $"Objeto con {props.Count} propiedades";
            case JsonValueKind.Array:
                var items = element.EnumerateArray().ToList();
                return items.Count > 0 ? $"Array con {items.Count} elementos" : "[]";
            default:
                return element.ToString();
        }
    }

    // Métodos auxiliares
    private string GetColumnName(int columnIndex)
    {
        int dividend = columnIndex;
        string columnName = string.Empty;
        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar(65 + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }
        return columnName;
    }

    // Estilos mejorados y profesionales con colores corporativos - VERSIÓN PREMIUM
    private void CreateAndAddWorkbookStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet();

        // FUENTES CON PALETA DEL FRONTEND
        var fonts = new Fonts { Count = 4U };

        // 0. Fuente base - foreground (oklch(0.25 0.01 300))
        fonts.Append(
            new Font(
                new FontSize { Val = 11 },
                new Color { Rgb = new HexBinaryValue() { Value = "404040" } }, // Foreground del frontend
                new FontName { Val = "Montserrat" } // Usar la fuente del frontend
            )
        );

        // 1. Título principal - Logo color (oklch(0.212 0.021 27.7))
        fonts.Append(
            new Font(
                new Bold(),
                new FontSize { Val = 28 },
                new Color { Rgb = new HexBinaryValue() { Value = "2D2D2D" } }, // Logo color más suave
                new FontName { Val = "Montserrat" }
            )
        );

        // 2. Encabezados - primary-foreground (oklch(0.25 0.01 300)) con negrita
        fonts.Append(
            new Font(
                new Bold(),
                new FontSize { Val = 11 },
                new Color { Rgb = new HexBinaryValue() { Value = "404040" } }, // Primary-foreground
                new FontName { Val = "Montserrat" }
            )
        );

        // 3. Datos complejos - muted-foreground (oklch(0.45 0.01 300))
        fonts.Append(
            new Font(
                new FontSize { Val = 11 },
                new Color { Rgb = new HexBinaryValue() { Value = "737373" } }, // Muted-foreground más suave
                new FontName { Val = "Montserrat" }
            )
        );

        // RELLENOS CON PALETA CORRECTA DEL FRONTEND
        var fills = new Fills { Count = 6U }; // +1 fill adicional
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.None }));
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));

        // 2. PRIMARY - Amarillo corporativo para headers (oklch(0.86 0.18 85))
        fills.Append(
            new Fill(
                new PatternFill
                {
                    PatternType = PatternValues.Solid,
                    ForegroundColor = new ForegroundColor
                    {
                        Rgb = new HexBinaryValue() { Value = "F4D03F" }, // Primary corporativo
                    },
                    BackgroundColor = new BackgroundColor { Indexed = 64U },
                }
            )
        );

        // 3. ACCENT - Filas alternas sutiles (oklch(0.94 0.05 85))
        fills.Append(
            new Fill(
                new PatternFill
                {
                    PatternType = PatternValues.Solid,
                    ForegroundColor = new ForegroundColor
                    {
                        Rgb = new HexBinaryValue() { Value = "F9F6E8" }, // Accent suave
                    },
                    BackgroundColor = new BackgroundColor { Indexed = 64U },
                }
            )
        );

        // 4. Blanco puro para filas principales
        fills.Append(
            new Fill(
                new PatternFill
                {
                    PatternType = PatternValues.Solid,
                    ForegroundColor = new ForegroundColor
                    {
                        Rgb = new HexBinaryValue() { Value = "FFFFFF" },
                    },
                    BackgroundColor = new BackgroundColor { Indexed = 64U },
                }
            )
        );

        // 5. MUTED - Para datos complejos (oklch(0.45 0.01 300) variant)
        fills.Append(
            new Fill(
                new PatternFill
                {
                    PatternType = PatternValues.Solid,
                    ForegroundColor = new ForegroundColor
                    {
                        Rgb = new HexBinaryValue() { Value = "F5F5F5" }, // Muted muy sutil para datos complejos
                    },
                    BackgroundColor = new BackgroundColor { Indexed = 64U },
                }
            )
        );

        // BORDES CON COLORES DEL FRONTEND
        var borders = new Borders { Count = 3U };

        // 0. Sin borde
        borders.Append(new Border());

        // 1. Bordes para datos - secondary-foreground (oklch(0.25 0.01 300))
        borders.Append(
            new Border(
                new LeftBorder
                {
                    Style = BorderStyleValues.Thin,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "404040" } }, // Secondary-foreground
                },
                new RightBorder
                {
                    Style = BorderStyleValues.Thin,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "404040" } },
                },
                new TopBorder
                {
                    Style = BorderStyleValues.Thin,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "404040" } },
                },
                new BottomBorder
                {
                    Style = BorderStyleValues.Thin,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "404040" } },
                },
                new DiagonalBorder()
            )
        );

        // 2. Bordes para headers - primary con más contraste
        borders.Append(
            new Border(
                new LeftBorder
                {
                    Style = BorderStyleValues.Medium,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "D4AC0D" } }, // Primary más oscuro para contraste
                },
                new RightBorder
                {
                    Style = BorderStyleValues.Medium,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "D4AC0D" } },
                },
                new TopBorder
                {
                    Style = BorderStyleValues.Medium,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "D4AC0D" } },
                },
                new BottomBorder
                {
                    Style = BorderStyleValues.Medium,
                    Color = new Color { Rgb = new HexBinaryValue { Value = "D4AC0D" } },
                },
                new DiagonalBorder()
            )
        );

        var cellStyleFormats = new CellStyleFormats { Count = 1U };
        cellStyleFormats.Append(
            new CellFormat
            {
                NumberFormatId = 0U,
                FontId = 0U,
                FillId = 0U,
                BorderId = 0U,
            }
        );

        // FORMATOS DE CELDA CON PALETA DEL FRONTEND
        var cellFormats = new CellFormats { Count = 7U }; // +1 para datos complejos

        // 0. Default
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = 0U,
                FontId = 0U,
                FillId = 0U,
                BorderId = 0U,
                FormatId = 0U,
            }
        );

        // 1. Área del logo/vacía (A1)
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = 0U,
                FontId = 0U,
                FillId = 4U, // Blanco puro
                BorderId = 0U,
                FormatId = 0U,
            }
        );

        // 2. Título principal
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = 0U,
                FontId = 1U,
                FillId = 4U, // Fondo blanco
                BorderId = 0U,
                FormatId = 0U,
                Alignment = new Alignment
                {
                    Horizontal = HorizontalAlignmentValues.Center,
                    Vertical = VerticalAlignmentValues.Center,
                },
            }
        );

        // 3. HEADERS - Primary corporativo
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = 0U,
                FontId = 2U,
                FillId = 2U, // PRIMARY corporativo
                BorderId = 2U, // Bordes primary
                FormatId = 0U,
                Alignment = new Alignment
                {
                    Horizontal = HorizontalAlignmentValues.Center,
                    Vertical = VerticalAlignmentValues.Center,
                },
            }
        );

        // 4. Filas pares - ACCENT suave
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = 0U,
                FontId = 0U,
                FillId = 3U, // ACCENT del frontend
                BorderId = 1U,
                FormatId = 0U,
                Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center },
            }
        );

        // 5. Filas impares - blanco
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = 0U,
                FontId = 0U,
                FillId = 4U, // Blanco puro
                BorderId = 1U,
                FormatId = 0U,
                Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center },
            }
        );

        // 6. NUEVO: Datos complejos/expandidos - MUTED
        cellFormats.Append(
            new CellFormat
            {
                NumberFormatId = 0U,
                FontId = 3U, // Fuente muted-foreground
                FillId = 5U, // Fondo muted sutil
                BorderId = 1U,
                FormatId = 0U,
                Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center },
            }
        );

        stylesPart.Stylesheet.Append(fonts);
        stylesPart.Stylesheet.Append(fills);
        stylesPart.Stylesheet.Append(borders);
        stylesPart.Stylesheet.Append(cellStyleFormats);
        stylesPart.Stylesheet.Append(cellFormats);
        stylesPart.Stylesheet.Save();
    }

    // Método para aplicar estilos con paleta del frontend
    private void ApplyStylesToWorksheet(Worksheet worksheet)
    {
        var sheetData = worksheet.GetFirstChild<SheetData>();
        if (sheetData == null)
            return;

        var allRows = sheetData.Elements<Row>().ToList();
        if (allRows.Count == 0)
            return;

        // LÓGICA CON PALETA CORRECTA DEL FRONTEND
        foreach (var row in allRows)
        {
            if (row.RowIndex == null)
                continue;
            var rowIndex = (int)row.RowIndex.Value;
            var cells = row.Elements<Cell>().ToList();

            foreach (var cell in cells)
            {
                if (cell.CellReference?.Value == null)
                    continue;

                var columnIndex = GetColumnIndex(cell.CellReference.Value);
                uint styleIndex = 0;

                // LÓGICA CON ESTILOS DEL FRONTEND
                if (rowIndex == 1) // Fila de título
                {
                    styleIndex = 2u; // Estilo de título
                }
                else if (rowIndex == 2) // HEADERS - Primary corporativo
                {
                    styleIndex = 3u; // PRIMARY corporativo amarillo
                }
                else // DATOS - con paleta del frontend
                {
                    // Verificar si es fila expandida/compleja
                    if (
                        IsExpandedRow(cell, cells, allRows, rowIndex)
                        || IsSectionTitle(cell)
                        || HasComplexDataIndicator(cell)
                    )
                    {
                        styleIndex = 6u; // NUEVO: Estilo muted para datos complejos
                    }
                    else
                    {
                        // Alternancia normal con accent/blanco
                        bool isEvenRow = (rowIndex - 3) % 2 == 0;
                        styleIndex = isEvenRow ? 4u : 5u; // Accent vs Blanco
                    }
                }

                // Aplicar estilo
                cell.StyleIndex = styleIndex;
            }
        }
    }

    // Métodos auxiliares GENÉRICOS para mejor detección de tipos de filas
    private bool IsExpandedRow(
        Cell cell,
        List<Cell> rowCells,
        List<Row> allRows,
        int currentRowIndex
    )
    {
        // Una fila expandida típicamente:
        // 1. Tiene contenido indentado o marcado especialmente
        // 2. Sigue inmediatamente a una fila principal
        // 3. Tiene menos columnas llenas que una fila principal

        var cellValue = GetCellValue(cell);

        // Buscar indicadores GENÉRICOS de filas expandidas
        if (!string.IsNullOrEmpty(cellValue))
        {
            // Indicadores universales de filas expandidas (símbolos y patrones comunes)
            string[] genericExpandedIndicators =
            {
                "→",
                "•",
                "-",
                "▪",
                "◦",
                "≫",
                "►",
                "▶",
                "⮚", // Símbolos de lista/indentación
                "├",
                "└",
                "│",
                "┌",
                "┐", // Símbolos de árbol
                "○",
                "●",
                "◆",
                "■",
                "□", // Bullets alternativos
            };

            // Verificar símbolos de indentación
            if (
                genericExpandedIndicators.Any(symbol =>
                    cellValue.StartsWith(symbol) || cellValue.Contains($" {symbol} ")
                )
            )
            {
                return true;
            }

            // Verificar patrones de datos expandidos (cualquier campo con ":")
            if (cellValue.Contains(":") && cellValue.Length < 100) // Evitar texto largo que casualmente tenga ":"
            {
                // Patrón típico: "Campo: Valor" o "Propiedad: Dato"
                var colonIndex = cellValue.IndexOf(':');
                if (colonIndex > 0 && colonIndex < cellValue.Length - 1)
                {
                    var beforeColon = cellValue.Substring(0, colonIndex).Trim();
                    // Si antes de los dos puntos hay una palabra corta (nombre de campo), probablemente es fila expandida
                    if (
                        beforeColon.Length > 0
                        && beforeColon.Length <= 30
                        && !beforeColon.Contains(' ')
                    )
                    {
                        return true;
                    }
                }
            }

            // Verificar si empieza con indentación (espacios al inicio)
            if (cellValue.StartsWith("  ") || cellValue.StartsWith("\t"))
            {
                return true;
            }

            // Verificar patrones de listas numeradas o con corchetes [1], [2], etc.
            if (
                System.Text.RegularExpressions.Regex.IsMatch(cellValue, @"^\[\d+\]")
                || System.Text.RegularExpressions.Regex.IsMatch(cellValue, @"^\d+\.")
                || System.Text.RegularExpressions.Regex.IsMatch(cellValue, @"^\d+\)")
            )
            {
                return true;
            }
        }

        // Verificar si esta fila tiene significativamente menos celdas llenas que la anterior
        if (currentRowIndex > 3) // Después de los encabezados
        {
            var previousRow = allRows.FirstOrDefault(r => r.RowIndex.Value == currentRowIndex - 1);
            if (previousRow != null)
            {
                var currentRowFilledCells = rowCells.Count(c =>
                    !string.IsNullOrEmpty(GetCellValue(c))
                );
                var previousRowCells = previousRow.Elements<Cell>().ToList();
                var previousRowFilledCells = previousRowCells.Count(c =>
                    !string.IsNullOrEmpty(GetCellValue(c))
                );

                // Si la fila actual tiene menos de la mitad de celdas llenas que la anterior,
                // probablemente es una fila expandida
                if (
                    currentRowFilledCells > 0
                    && currentRowFilledCells < previousRowFilledCells * 0.6
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsSectionTitle(Cell cell)
    {
        var cellValue = GetCellValue(cell);
        if (string.IsNullOrEmpty(cellValue))
            return false;

        // Patrones GENÉRICOS para títulos de sección (no específicos de clientes)

        // 1. Termina en dos puntos (patrón universal de título)
        if (cellValue.EndsWith(":"))
        {
            return true;
        }

        // 2. Empieza con símbolo de flecha o similar
        string[] sectionSymbols = { "→", "►", "▶", "⮚", "■", "●", "◆" };
        if (sectionSymbols.Any(symbol => cellValue.StartsWith(symbol)))
        {
            return true;
        }

        // 3. Patrones típicos de títulos de sección
        if (
            cellValue.ToLower().StartsWith("lista de")
            || cellValue.ToLower().StartsWith("datos de")
            || cellValue.ToLower().StartsWith("detalles de")
            || cellValue.ToLower().StartsWith("información de")
            || cellValue.ToLower().Contains("adicionales")
            || cellValue.ToLower().Contains("expandida")
            || cellValue.ToLower().Contains("expandidos")
        )
        {
            return true;
        }

        // 4. Texto corto seguido de dos puntos (máximo 50 caracteres para evitar falsos positivos)
        if (cellValue.Length <= 50 && cellValue.Contains(":"))
        {
            var parts = cellValue.Split(':');
            if (
                parts.Length == 2
                && !string.IsNullOrWhiteSpace(parts[0])
                && string.IsNullOrWhiteSpace(parts[1])
            )
            {
                // Formato "Título:" (sin contenido después de los dos puntos)
                return true;
            }
        }

        return false;
    }

    private bool HasComplexDataIndicator(Cell cell)
    {
        var cellValue = GetCellValue(cell);
        if (string.IsNullOrEmpty(cellValue))
            return false;

        // Buscar indicadores GENÉRICOS amigables para datos complejos
        // Estos son los mensajes que generamos en FormatMainRowValue()
        return cellValue.Contains("✓ Ver lista abajo")
            || cellValue.Contains("✓ Ver detalles abajo")
            || cellValue.Contains("Ver lista")
            || cellValue.Contains("Ver detalles")
            || cellValue.Contains("Sin elementos")
            || cellValue.Contains("Sin información")
            // También detectar otros patrones genéricos
            || cellValue.Equals("[]")
            || cellValue.Equals("{}")
            || cellValue.StartsWith("Array con ")
            || cellValue.StartsWith("Objeto con ")
            || cellValue.Contains(" elementos")
            || cellValue.Contains(" propiedades");
    }

    // Método auxiliar para obtener el valor de una celda
    private string GetCellValue(Cell cell)
    {
        if (cell?.CellValue == null)
            return string.Empty;

        var value = cell.CellValue.Text;

        // Si es un texto compartido, necesitamos obtenerlo de la tabla de strings compartidos
        if (cell.DataType != null && cell.DataType == CellValues.SharedString)
        {
            return string.Empty; // Por simplicidad, para este análisis de estilos
        }

        return value ?? string.Empty;
    }

    // Método auxiliar para obtener el índice de columna desde la referencia (A1, B2, etc.)
    private int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
            return 1;

        int columnIndex = 0;

        // Extraer solo las letras de la referencia (A, B, AA, etc.)
        foreach (char c in cellReference)
        {
            if (char.IsLetter(c))
            {
                columnIndex = columnIndex * 26 + (char.ToUpper(c) - 'A' + 1);
            }
            else
            {
                break; // Parar cuando llegamos a los números
            }
        }

        return Math.Max(columnIndex, 1);
    }
}
