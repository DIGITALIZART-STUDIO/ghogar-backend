using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services
{
    public interface IExcelTemplateService
    {
        Task<byte[]> GenerateClientImportTemplateAsync(
            Guid? currentUserId = null,
            IList<string>? currentUserRoles = null
        );
    }

    public class ExcelTemplateService : IExcelTemplateService
    {
        private readonly DatabaseContext _context;
        private readonly ILeadService _leadService;

        public ExcelTemplateService(DatabaseContext context, ILeadService leadService)
        {
            _context = context;
            _leadService = leadService;
        }

        public async Task<byte[]> GenerateClientImportTemplateAsync(
            Guid? currentUserId = null,
            IList<string>? currentUserRoles = null
        )
        {
            // Crear stream en memoria para el documento Excel
            var stream = new MemoryStream();

            // Crear el documento Excel
            using (
                var spreadsheetDocument = SpreadsheetDocument.Create(
                    stream,
                    SpreadsheetDocumentType.Workbook
                )
            )
            {
                // Crear partes del documento
                var workbookPart = spreadsheetDocument.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                // PRIMERO: Crear la hoja de estilos UNA SOLA VEZ antes de agregar cualquier hoja
                CreateAndAddWorkbookStyles(workbookPart);

                // Agregar la parte de SharedStringTable
                var sharedStringTablePart = workbookPart.AddNewPart<SharedStringTablePart>();
                sharedStringTablePart.SharedStringTable = new SharedStringTable();

                // LUEGO: Crear hojas y aplicar estilos a cada una
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var worksheetData = new SheetData();
                worksheetPart.Worksheet = new Worksheet(worksheetData);

                var userWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var userWorksheetData = new SheetData();
                userWorksheetPart.Worksheet = new Worksheet(userWorksheetData);

                var projectWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var projectWorksheetData = new SheetData();
                projectWorksheetPart.Worksheet = new Worksheet(projectWorksheetData);

                var instructionsWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var instructionsWorksheetData = new SheetData();
                instructionsWorksheetPart.Worksheet = new Worksheet(instructionsWorksheetData);

                var userListWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var userListWorksheetData = new SheetData();
                userListWorksheetPart.Worksheet = new Worksheet(userListWorksheetData);

                var captureSourcesWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var captureSourcesWorksheetData = new SheetData();
                captureSourcesWorksheetPart.Worksheet = new Worksheet(captureSourcesWorksheetData);

                var projectListWorksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var projectListWorksheetData = new SheetData();
                projectListWorksheetPart.Worksheet = new Worksheet(projectListWorksheetData);

                // Agregar definiciones de hojas al libro
                var sheets = spreadsheetDocument.WorkbookPart.Workbook.AppendChild(new Sheets());

                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1,
                        Name = "Plantilla Clientes",
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(userWorksheetPart),
                        SheetId = 2,
                        Name = "Usuarios Disponibles",
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(projectWorksheetPart),
                        SheetId = 3,
                        Name = "Proyectos Disponibles",
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(
                            instructionsWorksheetPart
                        ),
                        SheetId = 4,
                        Name = "Instrucciones",
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(userListWorksheetPart),
                        SheetId = 5,
                        Name = "ListasOcultas",
                        State = SheetStateValues.Hidden,
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(
                            captureSourcesWorksheetPart
                        ),
                        SheetId = 6,
                        Name = "MediosCaptacion",
                        State = SheetStateValues.Hidden,
                    }
                );
                sheets.AppendChild(
                    new Sheet()
                    {
                        Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(projectListWorksheetPart),
                        SheetId = 7,
                        Name = "ListaProyectos",
                        State = SheetStateValues.Hidden,
                    }
                );

                // Obtener la lista de usuarios con filtrado basado en roles
                IEnumerable<UserSummaryDto> users;
                if (currentUserRoles != null && currentUserId.HasValue)
                {
                    if (currentUserRoles.Contains("SalesAdvisor"))
                    {
                        // SalesAdvisor solo ve a sí mismo
                        users = await _leadService.GetUsersSummaryAsync();
                        users = users.Where(u => u.Id == currentUserId.Value);
                    }
                    else if (currentUserRoles.Contains("Supervisor"))
                    {
                        // Supervisor ve a sí mismo + sus SalesAdvisors asignados
                        var allUsers = await _leadService.GetUsersSummaryAsync();

                        // Obtener los IDs de los SalesAdvisors asignados a este supervisor
                        var assignedSalesAdvisorIds = await _context
                            .SupervisorSalesAdvisors.Where(ssa =>
                                ssa.SupervisorId == currentUserId.Value && ssa.IsActive
                            )
                            .Select(ssa => ssa.SalesAdvisorId)
                            .ToListAsync();

                        // Incluir también el propio ID del supervisor
                        assignedSalesAdvisorIds.Add(currentUserId.Value);

                        // Filtrar usuarios que están asignados a este supervisor + el supervisor mismo
                        users = allUsers.Where(u => assignedSalesAdvisorIds.Contains(u.Id));
                    }
                    else
                    {
                        // Para otros roles (Admin, Manager, etc.) - ver todos los usuarios
                        users = await _leadService.GetUsersSummaryAsync();
                    }
                }
                else
                {
                    // Si no se proporcionan roles, usar comportamiento por defecto (todos los usuarios)
                    users = await _leadService.GetUsersSummaryAsync();
                }

                // Obtener proyectos activos
                var projects = await _context.Projects.Where(p => p.IsActive).ToListAsync();

                // Crear una tabla de mapeo entre nombres de usuario e IDs en la hoja oculta
                // Agregar encabezados
                var userMappingHeaderRow = new Row { RowIndex = 1 };
                userListWorksheetData.AppendChild(userMappingHeaderRow);

                AddCellWithValue(
                    userMappingHeaderRow,
                    "A1",
                    "Nombre Usuario",
                    sharedStringTablePart
                );
                AddCellWithValue(userMappingHeaderRow, "B1", "ID Usuario", sharedStringTablePart);

                // Agregar usuarios a la hoja oculta
                int mappingRowIndex = 2;
                foreach (var user in users)
                {
                    var userRow = new Row { RowIndex = (uint)mappingRowIndex };
                    userListWorksheetData.AppendChild(userRow);

                    // El nombre del usuario va primero para la lista visual
                    AddCellWithValue(
                        userRow,
                        $"A{mappingRowIndex}",
                        user.UserName,
                        sharedStringTablePart
                    );

                    // El ID va en la segunda columna
                    AddCellWithValue(
                        userRow,
                        $"B{mappingRowIndex}",
                        user.Id.ToString(),
                        sharedStringTablePart
                    );

                    mappingRowIndex++;
                }

                // Crear la lista de medios de captación en la hoja oculta
                var captureSourceHeaderRow = new Row { RowIndex = 1 };
                captureSourcesWorksheetData.AppendChild(captureSourceHeaderRow);

                AddCellWithValue(
                    captureSourceHeaderRow,
                    "A1",
                    "Medio de Captación",
                    sharedStringTablePart
                );

                // Agregar los medios de captación traducidos a español
                var captureSourcesSpanish = new Dictionary<string, string>
                {
                    { "Company", "Empresa" },
                    { "PersonalFacebook", "FB Personal" },
                    { "RealEstateFair", "Feria inmobiliaria" },
                    { "Institutional", "Institucional" },
                    { "Loyalty", "Fidelizado" },
                };

                int captureSourceRowIndex = 2;
                foreach (var source in captureSourcesSpanish)
                {
                    var sourceRow = new Row { RowIndex = (uint)captureSourceRowIndex };
                    captureSourcesWorksheetData.AppendChild(sourceRow);

                    AddCellWithValue(
                        sourceRow,
                        $"A{captureSourceRowIndex}",
                        source.Value,
                        sharedStringTablePart
                    );

                    captureSourceRowIndex++;
                }

                // Crear la lista de proyectos en la hoja oculta
                var projectHeaderRow = new Row { RowIndex = 1 };
                projectListWorksheetData.AppendChild(projectHeaderRow);

                AddCellWithValue(projectHeaderRow, "A1", "Nombre Proyecto", sharedStringTablePart);
                AddCellWithValue(projectHeaderRow, "B1", "ID Proyecto", sharedStringTablePart);

                // Agregar proyectos a la hoja oculta
                int projectRowIndex = 2;
                foreach (var project in projects)
                {
                    var projectRow = new Row { RowIndex = (uint)projectRowIndex };
                    projectListWorksheetData.AppendChild(projectRow);

                    // El nombre del proyecto va primero para la lista visual
                    AddCellWithValue(
                        projectRow,
                        $"A{projectRowIndex}",
                        project.Name,
                        sharedStringTablePart
                    );

                    // El ID va en la segunda columna
                    AddCellWithValue(
                        projectRow,
                        $"B{projectRowIndex}",
                        project.Id.ToString(),
                        sharedStringTablePart
                    );

                    projectRowIndex++;
                }

                // Definir nombres para los rangos
                var definedNames = new DefinedNames();

                // Lista de usuarios
                var userListDefinedName = new DefinedName()
                {
                    Name = "UserList",
                    Text = $"'ListasOcultas'!$A$2:$A${mappingRowIndex - 1}",
                };
                definedNames.Append(userListDefinedName);

                // Lista de medios de captación
                var captureSourcesDefinedName = new DefinedName()
                {
                    Name = "CaptureSources",
                    Text = $"'MediosCaptacion'!$A$2:$A${captureSourceRowIndex - 1}",
                };
                definedNames.Append(captureSourcesDefinedName);

                // Lista de proyectos
                var projectListDefinedName = new DefinedName()
                {
                    Name = "ProjectList",
                    Text = $"'ListaProyectos'!$A$2:$A${projectRowIndex - 1}",
                };
                definedNames.Append(projectListDefinedName);

                workbookPart.Workbook.AppendChild(definedNames);

                // Configurar cada hoja utilizando métodos separados
                ConfigureMainSheet(worksheetPart, sharedStringTablePart, users);
                ConfigureUsersSheet(userWorksheetPart, sharedStringTablePart, users);
                ConfigureProjectsSheet(projectWorksheetPart, sharedStringTablePart, projects);
                ConfigureInstructionsSheet(instructionsWorksheetPart, sharedStringTablePart);

                // Configurar las validaciones de datos para las listas desplegables
                var dataValidations = new DataValidations();

                // Validación para Usuario Asignado (columna J)
                var userValidation = new DataValidation()
                {
                    Type = DataValidationValues.List,
                    AllowBlank = true,
                    Formula1 = new Formula1("UserList"),
                    ShowDropDown = false,
                };
                var userSqrefAttribute = new OpenXmlAttribute("sqref", "", "J2:J1000");
                userValidation.SetAttribute(userSqrefAttribute);
                dataValidations.Append(userValidation);

                // Validación para Medio de Captación (columna I)
                var captureSourceValidation = new DataValidation()
                {
                    Type = DataValidationValues.List,
                    AllowBlank = true,
                    Formula1 = new Formula1("CaptureSources"),
                    ShowDropDown = false,
                };
                var captureSourceSqrefAttribute = new OpenXmlAttribute("sqref", "", "I2:I1000");
                captureSourceValidation.SetAttribute(captureSourceSqrefAttribute);
                dataValidations.Append(captureSourceValidation);

                // Validación para Proyecto (columna K)
                var projectValidation = new DataValidation()
                {
                    Type = DataValidationValues.List,
                    AllowBlank = true,
                    Formula1 = new Formula1("ProjectList"),
                    ShowDropDown = false,
                };
                var projectSqrefAttribute = new OpenXmlAttribute("sqref", "", "K2:K1000");
                projectValidation.SetAttribute(projectSqrefAttribute);
                dataValidations.Append(projectValidation);

                // Añadir las validaciones a la hoja principal
                worksheetPart.Worksheet.AppendChild(dataValidations);

                // Guardar el libro de trabajo
                workbookPart.Workbook.Save();
            }

            // Reposicionar el stream para lectura
            stream.Position = 0;
            return stream.ToArray();
        }

        private void ConfigureProjectsSheet(
            WorksheetPart projectWorksheetPart,
            SharedStringTablePart sharedStringTablePart,
            List<Project> projects
        )
        {
            var worksheetData = projectWorksheetPart.Worksheet.GetFirstChild<SheetData>();

            // 1. Título
            var titleRow = new Row
            {
                RowIndex = 1,
                Height = 25,
                CustomHeight = true,
            };
            worksheetData.AppendChild(titleRow);

            var titleCell = new Cell
            {
                CellReference = "A1",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, "PROYECTOS DISPONIBLES").ToString()
                ),
                StyleIndex = 7, // Estilo amarillo con texto negro
            };
            titleRow.AppendChild(titleCell);

            var titleCell2 = new Cell
            {
                CellReference = "B1",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, "").ToString()
                ),
                StyleIndex = 7, // Estilo amarillo con texto negro
            };
            titleRow.AppendChild(titleCell2);

            // 2. Encabezados
            var headerRow = new Row
            {
                RowIndex = 2,
                Height = 20,
                CustomHeight = true,
            };
            worksheetData.AppendChild(headerRow);

            var idHeaderCell = new Cell
            {
                CellReference = "A2",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, "ID PROYECTO").ToString()
                ),
                StyleIndex = 2, // Estilo de encabezado gris con texto blanco
            };
            headerRow.AppendChild(idHeaderCell);

            var nameHeaderCell = new Cell
            {
                CellReference = "B2",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, "NOMBRE PROYECTO").ToString()
                ),
                StyleIndex = 2, // Estilo de encabezado gris con texto blanco
            };
            headerRow.AppendChild(nameHeaderCell);

            // 3. Datos
            int rowIndex = 3;
            foreach (var project in projects)
            {
                var dataRow = new Row { RowIndex = (uint)rowIndex };
                worksheetData.AppendChild(dataRow);

                // Alternar estilos para filas pares/impares
                uint styleIndex = (rowIndex % 2 == 0) ? 3U : 4U;

                var idCell = new Cell
                {
                    CellReference = $"A{rowIndex}",
                    DataType = CellValues.String,
                    CellValue = new CellValue(project.Id.ToString()),
                    StyleIndex = styleIndex,
                };
                dataRow.AppendChild(idCell);

                var nameCell = new Cell
                {
                    CellReference = $"B{rowIndex}",
                    DataType = CellValues.SharedString,
                    CellValue = new CellValue(
                        AddSharedStringItem(sharedStringTablePart, project.Name).ToString()
                    ),
                    StyleIndex = styleIndex,
                };
                dataRow.AppendChild(nameCell);

                rowIndex++;
            }

            // 4. Anchos de columna
            var columns = new Columns();
            columns.Append(
                new Column
                {
                    Min = 1,
                    Max = 1,
                    Width = 36,
                    CustomWidth = true,
                }
            );
            columns.Append(
                new Column
                {
                    Min = 2,
                    Max = 2,
                    Width = 25,
                    CustomWidth = true,
                }
            );

            var existingColumns = projectWorksheetPart.Worksheet.GetFirstChild<Columns>();
            if (existingColumns != null)
                existingColumns.Remove();
            projectWorksheetPart.Worksheet.InsertAt(columns, 0);

            // 5. Fusionar celdas
            var mergeCells = new MergeCells();
            mergeCells.Append(new MergeCell { Reference = "A1:B1" });

            if (projectWorksheetPart.Worksheet.Elements<MergeCells>().Count() == 0)
            {
                projectWorksheetPart.Worksheet.AppendChild(mergeCells);
            }
            else
            {
                var existing = projectWorksheetPart.Worksheet.GetFirstChild<MergeCells>();
                existing.Remove();
                projectWorksheetPart.Worksheet.AppendChild(mergeCells);
            }
        }

        private void ConfigureMainSheet(
            WorksheetPart worksheetPart,
            SharedStringTablePart sharedStringTablePart,
            IEnumerable<UserSummaryDto> users
        )
        {
            var worksheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

            // Definir encabezados para la hoja principal
            string[] headers = new string[]
            {
                "Nombre",
                "País",
                "DNI",
                "RUC",
                "Nombre Empresa",
                "Teléfono (+51)",
                "Email",
                "Dirección",
                "Medio de Captación", // Cambiado de "Procedencia" a "Medio de Captación"
                "Usuario Asignado",
                "Proyecto", // Nueva columna para proyectos
            };

            // Crear fila de encabezados
            var headerRow = new Row
            {
                RowIndex = 1,
                Height = 20,
                CustomHeight = true,
            };
            worksheetData.AppendChild(headerRow);

            // Agregar las celdas de encabezado con estilo
            for (var i = 0; i < headers.Length; i++)
            {
                var headerIndex = AddSharedStringItem(sharedStringTablePart, headers[i]);

                // Crear celda con el valor del encabezado
                var cell = new Cell
                {
                    CellReference = GetColumnName(i + 1) + "1",
                    DataType = CellValues.SharedString,
                    CellValue = new CellValue(headerIndex.ToString()),
                    StyleIndex = 2, // Estilo de encabezado (gris con texto blanco en negrita y bordes)
                };

                headerRow.AppendChild(cell);
            }

            // Ejemplo 1: Cliente natural con asignación
            var exampleRow1 = new Row { RowIndex = 2 };
            worksheetData.AppendChild(exampleRow1);

            AddCellWithValue(exampleRow1, "A2", "Juan Pérez", sharedStringTablePart);
            AddCellWithValue(exampleRow1, "B2", "Perú", sharedStringTablePart);
            AddCellWithValue(exampleRow1, "C2", "12345678", sharedStringTablePart);
            AddCellWithValue(exampleRow1, "F2", "+51999888777", sharedStringTablePart);
            AddCellWithValue(exampleRow1, "G2", "juan.perez@ejemplo.com", sharedStringTablePart);
            AddCellWithValue(exampleRow1, "H2", "Av. Principal 123", sharedStringTablePart);
            AddCellWithValue(exampleRow1, "I2", "Empresa", sharedStringTablePart); // Medio de Captación: Empresa

            // Mostrar el nombre del usuario por defecto
            var defaultUser = users.FirstOrDefault();
            if (defaultUser != null)
            {
                AddCellWithValue(exampleRow1, "J2", defaultUser.UserName, sharedStringTablePart);
            }

            // Ejemplo 2: Cliente jurídico sin asignación
            var exampleRow2 = new Row { RowIndex = 3 };
            worksheetData.AppendChild(exampleRow2);

            AddCellWithValue(exampleRow2, "A3", "Empresa ABC", sharedStringTablePart);

            // Crear celda de RUC como texto explícitamente para evitar problemas de formato
            var rucCell = new Cell
            {
                CellReference = "D3",
                DataType = CellValues.String,
                CellValue = new CellValue("20123456789"),
            };
            exampleRow2.AppendChild(rucCell);

            AddCellWithValue(exampleRow2, "E3", "ABC Corporación", sharedStringTablePart);
            AddCellWithValue(exampleRow2, "F3", "+51998877665", sharedStringTablePart);
            AddCellWithValue(exampleRow2, "G3", "contacto@empresaabc.com", sharedStringTablePart);
            AddCellWithValue(exampleRow2, "H3", "Jr. Comercial 456", sharedStringTablePart);
            AddCellWithValue(exampleRow2, "I3", "Feria inmobiliaria", sharedStringTablePart); // Medio de Captación: Feria inmobiliaria

            // Configurar anchos de columna adecuados
            var columns = new Columns();
            columns.Append(
                new Column
                {
                    Min = 1,
                    Max = 1,
                    Width = 20,
                    CustomWidth = true,
                }
            ); // Nombre
            columns.Append(
                new Column
                {
                    Min = 2,
                    Max = 2,
                    Width = 20,
                    CustomWidth = true,
                }
            ); // País
            columns.Append(
                new Column
                {
                    Min = 3,
                    Max = 3,
                    Width = 12,
                    CustomWidth = true,
                }
            ); // DNI
            columns.Append(
                new Column
                {
                    Min = 4,
                    Max = 4,
                    Width = 15,
                    CustomWidth = true,
                }
            ); // RUC
            columns.Append(
                new Column
                {
                    Min = 5,
                    Max = 5,
                    Width = 25,
                    CustomWidth = true,
                }
            ); // Nombre Empresa
            columns.Append(
                new Column
                {
                    Min = 6,
                    Max = 6,
                    Width = 15,
                    CustomWidth = true,
                }
            ); // Teléfono
            columns.Append(
                new Column
                {
                    Min = 7,
                    Max = 7,
                    Width = 25,
                    CustomWidth = true,
                }
            ); // Email
            columns.Append(
                new Column
                {
                    Min = 8,
                    Max = 8,
                    Width = 30,
                    CustomWidth = true,
                }
            ); // Dirección
            columns.Append(
                new Column
                {
                    Min = 9,
                    Max = 9,
                    Width = 20,
                    CustomWidth = true,
                }
            ); // Medio de Captación
            columns.Append(
                new Column
                {
                    Min = 10,
                    Max = 10,
                    Width = 20,
                    CustomWidth = true,
                }
            ); // Usuario Asignado
            columns.Append(
                new Column
                {
                    Min = 11,
                    Max = 11,
                    Width = 25,
                    CustomWidth = true,
                }
            ); // Proyecto

            var existingColumns = worksheetPart.Worksheet.GetFirstChild<Columns>();
            if (existingColumns != null)
                existingColumns.Remove();

            worksheetPart.Worksheet.InsertAt(columns, 0);
        }

        private void CreateAndAddWorkbookStyles(WorkbookPart workbookPart)
        {
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new Stylesheet();

            // 1. Crear fuentes
            var fonts = new Fonts { Count = (UInt32Value)5U };

            // Fuente normal
            fonts.Append(
                new Font(
                    new FontSize { Val = 11 },
                    new Color { Rgb = new HexBinaryValue() { Value = "000000" } },
                    new FontName { Val = "Calibri" }
                )
            );

            // Fuente negrita
            fonts.Append(
                new Font(
                    new Bold(),
                    new FontSize { Val = 11 },
                    new Color { Rgb = new HexBinaryValue() { Value = "000000" } },
                    new FontName { Val = "Calibri" }
                )
            );

            // Fuente negrita blanca
            fonts.Append(
                new Font(
                    new Bold(),
                    new FontSize { Val = 11 },
                    new Color { Rgb = new HexBinaryValue() { Value = "FFFFFF" } },
                    new FontName { Val = "Calibri" }
                )
            );

            // Fuente azul (para notas)
            fonts.Append(
                new Font(
                    new Bold(),
                    new FontSize { Val = 11 },
                    new Color { Rgb = new HexBinaryValue() { Value = "0070C0" } },
                    new FontName { Val = "Calibri" }
                )
            );

            // Fuente roja (para advertencias)
            fonts.Append(
                new Font(
                    new Bold(),
                    new FontSize { Val = 11 },
                    new Color { Rgb = new HexBinaryValue() { Value = "FF0000" } },
                    new FontName { Val = "Calibri" }
                )
            );

            // 2. Crear rellenos
            var fills = new Fills { Count = (UInt32Value)5U };

            // Rellenos estándar requeridos
            fills.Append(new Fill(new PatternFill { PatternType = PatternValues.None }));
            fills.Append(new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));

            // Relleno amarillo (color principal)
            fills.Append(
                new Fill(
                    new PatternFill
                    {
                        PatternType = PatternValues.Solid,
                        ForegroundColor = new ForegroundColor
                        {
                            Rgb = new HexBinaryValue() { Value = "FFD038" },
                        },
                        BackgroundColor = new BackgroundColor { Indexed = (UInt32Value)64U },
                    }
                )
            );

            // Relleno amarillo claro (filas alternadas)
            fills.Append(
                new Fill(
                    new PatternFill
                    {
                        PatternType = PatternValues.Solid,
                        ForegroundColor = new ForegroundColor
                        {
                            Rgb = new HexBinaryValue() { Value = "FFF1C9" },
                        },
                        BackgroundColor = new BackgroundColor { Indexed = (UInt32Value)64U },
                    }
                )
            );

            // Relleno gris (para encabezados secundarios)
            fills.Append(
                new Fill(
                    new PatternFill
                    {
                        PatternType = PatternValues.Solid,
                        ForegroundColor = new ForegroundColor
                        {
                            Rgb = new HexBinaryValue() { Value = "393839" },
                        },
                        BackgroundColor = new BackgroundColor { Indexed = (UInt32Value)64U },
                    }
                )
            );

            // 3. Crear bordes
            var borders = new Borders { Count = (UInt32Value)2U };

            // Sin bordes
            borders.Append(
                new Border(
                    new LeftBorder(),
                    new RightBorder(),
                    new TopBorder(),
                    new BottomBorder(),
                    new DiagonalBorder()
                )
            );

            // Con bordes negros
            borders.Append(
                new Border(
                    new LeftBorder
                    {
                        Style = BorderStyleValues.Thin,
                        Color = new Color { Rgb = new HexBinaryValue { Value = "000000" } },
                    },
                    new RightBorder
                    {
                        Style = BorderStyleValues.Thin,
                        Color = new Color { Rgb = new HexBinaryValue { Value = "000000" } },
                    },
                    new TopBorder
                    {
                        Style = BorderStyleValues.Thin,
                        Color = new Color { Rgb = new HexBinaryValue { Value = "000000" } },
                    },
                    new BottomBorder
                    {
                        Style = BorderStyleValues.Thin,
                        Color = new Color { Rgb = new HexBinaryValue { Value = "000000" } },
                    },
                    new DiagonalBorder()
                )
            );

            // 4. Crear formatos de celda
            var cellStyleFormats = new CellStyleFormats { Count = (UInt32Value)1U };
            cellStyleFormats.Append(
                new CellFormat
                {
                    NumberFormatId = (UInt32Value)0U,
                    FontId = (UInt32Value)0U,
                    FillId = (UInt32Value)0U,
                    BorderId = (UInt32Value)0U,
                }
            );

            var cellFormats = new CellFormats { Count = (UInt32Value)8U };

            // 0. Estilo por defecto
            cellFormats.Append(
                new CellFormat
                {
                    NumberFormatId = (UInt32Value)0U,
                    FontId = (UInt32Value)0U,
                    FillId = (UInt32Value)0U,
                    BorderId = (UInt32Value)0U,
                    FormatId = (UInt32Value)0U,
                }
            );

            // 1. Título principal - fondo amarillo, negrita
            cellFormats.Append(
                new CellFormat
                {
                    NumberFormatId = (UInt32Value)0U,
                    FontId = (UInt32Value)1U,
                    FillId = (UInt32Value)2U,
                    BorderId = (UInt32Value)1U,
                    FormatId = (UInt32Value)0U,
                    Alignment = new Alignment
                    {
                        Horizontal = HorizontalAlignmentValues.Center,
                        Vertical = VerticalAlignmentValues.Center,
                    },
                }
            );

            // 2. Encabezados tabla - gris con texto blanco
            cellFormats.Append(
                new CellFormat
                {
                    NumberFormatId = (UInt32Value)0U,
                    FontId = (UInt32Value)2U,
                    FillId = (UInt32Value)4U,
                    BorderId = (UInt32Value)1U,
                    FormatId = (UInt32Value)0U,
                    Alignment = new Alignment
                    {
                        Horizontal = HorizontalAlignmentValues.Center,
                        Vertical = VerticalAlignmentValues.Center,
                    },
                }
            );

            // 3. Filas pares - fondo amarillo claro
            cellFormats.Append(
                new CellFormat
                {
                    NumberFormatId = (UInt32Value)0U,
                    FontId = (UInt32Value)0U,
                    FillId = (UInt32Value)3U,
                    BorderId = (UInt32Value)1U,
                    FormatId = (UInt32Value)0U,
                    Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center },
                }
            );

            // 4. Filas impares - sin relleno
            cellFormats.Append(
                new CellFormat
                {
                    NumberFormatId = (UInt32Value)0U,
                    FontId = (UInt32Value)0U,
                    FillId = (UInt32Value)0U,
                    BorderId = (UInt32Value)1U,
                    FormatId = (UInt32Value)0U,
                    Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center },
                }
            );

            // 5. Notas (azul)
            cellFormats.Append(
                new CellFormat
                {
                    NumberFormatId = (UInt32Value)0U,
                    FontId = (UInt32Value)3U,
                    FillId = (UInt32Value)0U,
                    BorderId = (UInt32Value)0U,
                    FormatId = (UInt32Value)0U,
                }
            );

            // 6. Advertencias (rojo)
            cellFormats.Append(
                new CellFormat
                {
                    NumberFormatId = (UInt32Value)0U,
                    FontId = (UInt32Value)4U,
                    FillId = (UInt32Value)0U,
                    BorderId = (UInt32Value)0U,
                    FormatId = (UInt32Value)0U,
                }
            );

            // 7. Encabezados amarillos con texto negro
            cellFormats.Append(
                new CellFormat
                {
                    NumberFormatId = (UInt32Value)0U,
                    FontId = (UInt32Value)1U,
                    FillId = (UInt32Value)2U,
                    BorderId = (UInt32Value)1U,
                    FormatId = (UInt32Value)0U,
                    Alignment = new Alignment
                    {
                        Horizontal = HorizontalAlignmentValues.Center,
                        Vertical = VerticalAlignmentValues.Center,
                    },
                }
            );

            // Añadir las partes a la hoja de estilos
            stylesPart.Stylesheet.Append(fonts);
            stylesPart.Stylesheet.Append(fills);
            stylesPart.Stylesheet.Append(borders);
            stylesPart.Stylesheet.Append(cellStyleFormats);
            stylesPart.Stylesheet.Append(cellFormats);

            // Guardar la hoja de estilos
            stylesPart.Stylesheet.Save();
        }

        private void ConfigureUsersSheet(
            WorksheetPart userWorksheetPart,
            SharedStringTablePart sharedStringTablePart,
            IEnumerable<UserSummaryDto> users
        )
        {
            var worksheetData = userWorksheetPart.Worksheet.GetFirstChild<SheetData>();

            // 1. Título
            var titleRow = new Row
            {
                RowIndex = 1,
                Height = 25,
                CustomHeight = true,
            };
            worksheetData.AppendChild(titleRow);

            var titleCell = new Cell
            {
                CellReference = "A1",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, "USUARIOS DISPONIBLES").ToString()
                ),
                StyleIndex = 7, // Estilo amarillo con texto negro
            };
            titleRow.AppendChild(titleCell);

            var titleCell2 = new Cell
            {
                CellReference = "B1",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, "").ToString()
                ),
                StyleIndex = 7, // Estilo amarillo con texto negro
            };
            titleRow.AppendChild(titleCell2);

            // 2. Encabezados
            var headerRow = new Row
            {
                RowIndex = 2,
                Height = 20,
                CustomHeight = true,
            };
            worksheetData.AppendChild(headerRow);

            var idHeaderCell = new Cell
            {
                CellReference = "A2",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, "ID USUARIO").ToString()
                ),
                StyleIndex = 2, // Estilo de encabezado gris con texto blanco
            };
            headerRow.AppendChild(idHeaderCell);

            var nameHeaderCell = new Cell
            {
                CellReference = "B2",
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, "NOMBRE USUARIO").ToString()
                ),
                StyleIndex = 2, // Estilo de encabezado gris con texto blanco
            };
            headerRow.AppendChild(nameHeaderCell);

            // 3. Datos
            int rowIndex = 3;
            foreach (var user in users)
            {
                var dataRow = new Row { RowIndex = (uint)rowIndex };
                worksheetData.AppendChild(dataRow);

                // Alternar estilos para filas pares/impares
                uint styleIndex = (rowIndex % 2 == 0) ? 3U : 4U;

                var idCell = new Cell
                {
                    CellReference = $"A{rowIndex}",
                    DataType = CellValues.String,
                    CellValue = new CellValue(user.Id.ToString()),
                    StyleIndex = styleIndex,
                };
                dataRow.AppendChild(idCell);

                var nameCell = new Cell
                {
                    CellReference = $"B{rowIndex}",
                    DataType = CellValues.SharedString,
                    CellValue = new CellValue(
                        AddSharedStringItem(sharedStringTablePart, user.UserName).ToString()
                    ),
                    StyleIndex = styleIndex,
                };
                dataRow.AppendChild(nameCell);

                rowIndex++;
            }

            // 4. Anchos de columna
            var columns = new Columns();
            columns.Append(
                new Column
                {
                    Min = 1,
                    Max = 1,
                    Width = 36,
                    CustomWidth = true,
                }
            );
            columns.Append(
                new Column
                {
                    Min = 2,
                    Max = 2,
                    Width = 25,
                    CustomWidth = true,
                }
            );

            var existingColumns = userWorksheetPart.Worksheet.GetFirstChild<Columns>();
            if (existingColumns != null)
                existingColumns.Remove();
            userWorksheetPart.Worksheet.InsertAt(columns, 0);

            // 5. Fusionar celdas
            var mergeCells = new MergeCells();
            mergeCells.Append(new MergeCell { Reference = "A1:B1" });

            if (userWorksheetPart.Worksheet.Elements<MergeCells>().Count() == 0)
            {
                userWorksheetPart.Worksheet.AppendChild(mergeCells);
            }
            else
            {
                var existing = userWorksheetPart.Worksheet.GetFirstChild<MergeCells>();
                existing.Remove();
                userWorksheetPart.Worksheet.AppendChild(mergeCells);
            }
        }

        private void ConfigureInstructionsSheet(
            WorksheetPart instructionsWorksheetPart,
            SharedStringTablePart sharedStringTablePart
        )
        {
            var worksheetData = instructionsWorksheetPart.Worksheet.GetFirstChild<SheetData>();

            // 2. Agregar título principal
            var titleRow = new Row
            {
                RowIndex = 1,
                Height = 30,
                CustomHeight = true,
            };
            worksheetData.AppendChild(titleRow);

            var titleCell = new Cell
            {
                CellReference = "A1",
                StyleIndex = 1, // Usar estilo existente (título principal)
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(
                            sharedStringTablePart,
                            "INSTRUCCIONES PARA IMPORTAR CLIENTES Y ASIGNAR LEADS"
                        )
                        .ToString()
                ),
            };
            titleRow.AppendChild(titleCell);

            // 3. Agregar encabezados de la tabla de instrucciones con espacio
            var spacerRow = new Row { RowIndex = 2 };
            worksheetData.AppendChild(spacerRow);

            var headerRow = new Row
            {
                RowIndex = 3,
                Height = 20,
                CustomHeight = true,
            };
            worksheetData.AppendChild(headerRow);

            var headers = new[]
            {
                "CAMPO",
                "DESCRIPCIÓN",
                "OBLIGATORIO",
                "FORMATO / VALORES VÁLIDOS",
            };
            for (int i = 0; i < headers.Length; i++)
            {
                var headerCell = new Cell
                {
                    CellReference = GetColumnName(i + 1) + "3",
                    StyleIndex = 2, // Estilo de encabezado
                    DataType = CellValues.SharedString,
                    CellValue = new CellValue(
                        AddSharedStringItem(sharedStringTablePart, headers[i]).ToString()
                    ),
                };
                headerRow.AppendChild(headerCell);
            }

            // 4. Agregar datos para la tabla de instrucciones
            string[][] instructionsData = new string[][]
            {
                new string[] { "Nombre", "Nombre completo del cliente", "No", "" },
                new string[] { "País", "País del cliente", "No", "" },
                new string[]
                {
                    "DNI",
                    "Número de DNI",
                    "No",
                    "8 dígitos, único pero no obligatorio",
                },
                new string[]
                {
                    "RUC",
                    "Número de RUC",
                    "No*",
                    "*Obligatorio para clientes jurídicos, 11 dígitos",
                },
                new string[]
                {
                    "Nombre Empresa",
                    "Nombre de la empresa",
                    "No*",
                    "*Solo para clientes jurídicos. Si está vacío, usa el campo Nombre",
                },
                new string[]
                {
                    "Teléfono",
                    "Número de teléfono",
                    "Sí",
                    "Debe ser único. Recomendado incluir código de país (51), se puede obviar el +",
                },
                new string[]
                {
                    "Email",
                    "Correo electrónico del cliente",
                    "No",
                    "formato@ejemplo.com",
                },
                new string[] { "Dirección", "Dirección completa del cliente", "No", "" },
                new string[]
                {
                    "Medio de Captación",
                    "Fuente por la que se captó el lead",
                    "Si",
                    "Seleccionar de la lista desplegable (Empresa, FB Personal, etc.)",
                },
                new string[]
                {
                    "Usuario Asignado",
                    "Usuario al que se asignará el lead",
                    "No",
                    "Seleccionar de la lista desplegable",
                },
                new string[]
                {
                    "Proyecto",
                    "Proyecto relacionado con el lead",
                    "No",
                    "Seleccionar de la lista desplegable",
                },
            };

            uint rowIndex = 4;
            foreach (var row in instructionsData)
            {
                var dataRow = new Row
                {
                    RowIndex = rowIndex,
                    Height = 18,
                    CustomHeight = true,
                };
                worksheetData.AppendChild(dataRow);

                uint styleIndex = (rowIndex % 2 == 0) ? 3U : 4U; // Alternar estilos para filas pares/impares

                for (int i = 0; i < row.Length; i++)
                {
                    var cell = new Cell
                    {
                        CellReference = GetColumnName(i + 1) + rowIndex.ToString(),
                        StyleIndex = styleIndex,
                        DataType = CellValues.SharedString,
                        CellValue = new CellValue(
                            AddSharedStringItem(sharedStringTablePart, row[i]).ToString()
                        ),
                    };
                    dataRow.AppendChild(cell);
                }

                rowIndex++;
            }

            // 5. Espacio antes de la tabla de tipos de cliente
            rowIndex += 2;

            // 6. Tabla de tipos de cliente - Encabezado
            var clientTypesHeaderRow = new Row
            {
                RowIndex = rowIndex,
                Height = 25,
                CustomHeight = true,
            };
            worksheetData.AppendChild(clientTypesHeaderRow);

            var clientTypesHeaderCell = new Cell
            {
                CellReference = "A" + rowIndex.ToString(),
                StyleIndex = 1, // Estilo de título principal
                DataType = CellValues.SharedString,
                CellValue = new CellValue(
                    AddSharedStringItem(sharedStringTablePart, "TIPOS DE CLIENTE").ToString()
                ),
            };
            clientTypesHeaderRow.AppendChild(clientTypesHeaderCell);

            // 7. Definir encabezados de la tabla de tipos
            rowIndex++;
            var typeTableHeaderRow = new Row
            {
                RowIndex = rowIndex,
                Height = 20,
                CustomHeight = true,
            };
            worksheetData.AppendChild(typeTableHeaderRow);

            var typeHeaders = new[] { "TIPO", "DESCRIPCIÓN", "CAMPOS REQUERIDOS", "OBSERVACIONES" };
            for (int i = 0; i < typeHeaders.Length; i++)
            {
                var headerCell = new Cell
                {
                    CellReference = GetColumnName(i + 1) + rowIndex.ToString(),
                    StyleIndex = 2, // Estilo de encabezado
                    DataType = CellValues.SharedString,
                    CellValue = new CellValue(
                        AddSharedStringItem(sharedStringTablePart, typeHeaders[i]).ToString()
                    ),
                };
                typeTableHeaderRow.AppendChild(headerCell);
            }

            // 8. Datos de tipos de cliente
            rowIndex++;
            string[][] clientTypesData = new string[][]
            {
                new string[]
                {
                    "Natural",
                    "Persona individual",
                    "Nombre, DNI",
                    "DNI obligatorio, RUC y CompanyName deben estar vacíos",
                },
                new string[]
                {
                    "Jurídico",
                    "Empresa o persona jurídica",
                    "Nombre, RUC",
                    "RUC de 11 dígitos obligatorio, si CompanyName está vacío se usa el valor de Nombre",
                },
            };

            foreach (var row in clientTypesData)
            {
                var dataRow = new Row
                {
                    RowIndex = rowIndex,
                    Height = 18,
                    CustomHeight = true,
                };
                worksheetData.AppendChild(dataRow);

                uint styleIndex = (rowIndex % 2 == 0) ? 3U : 4U; // Alternar estilos para filas pares/impares

                for (int i = 0; i < row.Length; i++)
                {
                    var cell = new Cell
                    {
                        CellReference = GetColumnName(i + 1) + rowIndex.ToString(),
                        StyleIndex = styleIndex,
                        DataType = CellValues.SharedString,
                        CellValue = new CellValue(
                            AddSharedStringItem(sharedStringTablePart, row[i]).ToString()
                        ),
                    };
                    dataRow.AppendChild(cell);
                }

                rowIndex++;
            }

            // 9. Espacio antes de las notas importantes
            rowIndex += 2;

            // 10. Notas adicionales
            string[][] notes = new string[][]
            {
                new string[]
                {
                    "NOTA:",
                    "Para asignar un usuario, selecciónelo de la lista desplegable en la columna 'Usuario Asignado'.",
                },
                new string[]
                {
                    "IMPORTANTE:",
                    "El teléfono es obligatorio y debe ser único. Si existe RUC, el cliente será jurídico, sino será cliente natural.",
                },
                new string[]
                {
                    "RECOMENDACIÓN:",
                    "Revise la hoja 'Usuarios Disponibles' para ver la lista completa de usuarios.",
                },
                new string[]
                {
                    "⚠️ ADVERTENCIA:",
                    "No modifique las listas desplegables ni elimine la hoja oculta 'ListasOcultas'.",
                },
            };

            foreach (var note in notes)
            {
                var noteRow = new Row { RowIndex = rowIndex };
                worksheetData.AppendChild(noteRow);

                // Determinar el estilo según el tipo de nota
                uint styleIndex = 5U; // Estilo normal azul para notas
                if (note[0].Contains("ADVERTENCIA"))
                {
                    styleIndex = 6U; // Estilo rojo para advertencias
                }

                var labelCell = new Cell
                {
                    CellReference = "A" + rowIndex.ToString(),
                    StyleIndex = styleIndex,
                    DataType = CellValues.SharedString,
                    CellValue = new CellValue(
                        AddSharedStringItem(sharedStringTablePart, note[0]).ToString()
                    ),
                };
                noteRow.AppendChild(labelCell);

                var textCell = new Cell
                {
                    CellReference = "B" + rowIndex.ToString(),
                    DataType = CellValues.SharedString,
                    CellValue = new CellValue(
                        AddSharedStringItem(sharedStringTablePart, note[1]).ToString()
                    ),
                };
                noteRow.AppendChild(textCell);

                rowIndex++;
            }

            // 11. Configuración de propiedades de columna para anchos ajustados
            var columns = new Columns();
            columns.Append(
                new Column()
                {
                    Min = 1,
                    Max = 1,
                    Width = 20,
                    CustomWidth = true,
                }
            );
            columns.Append(
                new Column()
                {
                    Min = 2,
                    Max = 2,
                    Width = 30,
                    CustomWidth = true,
                }
            );
            columns.Append(
                new Column()
                {
                    Min = 3,
                    Max = 3,
                    Width = 15,
                    CustomWidth = true,
                }
            );
            columns.Append(
                new Column()
                {
                    Min = 4,
                    Max = 4,
                    Width = 40,
                    CustomWidth = true,
                }
            );

            var existingColumns = instructionsWorksheetPart.Worksheet.GetFirstChild<Columns>();
            if (existingColumns != null)
            {
                existingColumns.Remove();
            }

            instructionsWorksheetPart.Worksheet.InsertAt(columns, 0);

            // 12. Fusionar celdas para títulos
            var mergeCells = new MergeCells();

            // Título principal
            mergeCells.Append(new MergeCell() { Reference = "A1:D1" });

            // Título de tipos de cliente
            mergeCells.Append(
                new MergeCell()
                {
                    Reference = $"A{rowIndex - notes.Length - 2}:D{rowIndex - notes.Length - 2}",
                }
            );

            // Si ya existe una colección MergeCells, la reemplazamos
            if (instructionsWorksheetPart.Worksheet.Elements<MergeCells>().Count() == 0)
            {
                instructionsWorksheetPart.Worksheet.AppendChild(mergeCells);
            }
            else
            {
                var existing = instructionsWorksheetPart.Worksheet.GetFirstChild<MergeCells>();
                existing.Remove();
                instructionsWorksheetPart.Worksheet.AppendChild(mergeCells);
            }
        }

        // Método para agregar un elemento a la tabla de cadenas compartidas y devolver su índice
        private int AddSharedStringItem(SharedStringTablePart sharedStringTablePart, string text)
        {
            // Si la tabla de cadenas compartidas está vacía, agregar una
            if (sharedStringTablePart.SharedStringTable == null)
            {
                sharedStringTablePart.SharedStringTable = new SharedStringTable();
            }

            int i = 0;

            // Verificar si el texto ya existe en la tabla
            foreach (
                SharedStringItem item in sharedStringTablePart.SharedStringTable.Elements<SharedStringItem>()
            )
            {
                if (item.InnerText == text)
                {
                    return i;
                }

                i++;
            }

            // El texto no existe, agregarlo
            sharedStringTablePart.SharedStringTable.AppendChild(
                new SharedStringItem(new Text(text))
            );

            sharedStringTablePart.SharedStringTable.Save();

            return i;
        }

        // Método para agregar una celda con un valor específico
        private void AddCellWithValue(
            Row row,
            string cellReference,
            string text,
            SharedStringTablePart sharedStringTablePart
        )
        {
            var index = AddSharedStringItem(sharedStringTablePart, text);

            var cell = new Cell
            {
                CellReference = cellReference,
                DataType = CellValues.SharedString,
                CellValue = new CellValue(index.ToString()),
            };

            row.AppendChild(cell);
        }

        // Método para obtener el nombre de columna (A, B, C, ...) desde un número
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
    }
}
