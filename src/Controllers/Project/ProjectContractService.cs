using GestionHogar.Dtos;
using GestionHogar.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GestionHogar.Services;

public class ProjectContractService : IProjectContractService
{
    private readonly DatabaseContext _context;
    private readonly ContractHtmlService _htmlService;
    private readonly SofficeConverterService _sofficeConverterService;

    public ProjectContractService(
        DatabaseContext context,
        ContractHtmlService htmlService,
        SofficeConverterService sofficeConverterService
    )
    {
        _context = context;
        _htmlService = htmlService;
        _sofficeConverterService = sofficeConverterService;
    }

    public IReadOnlyList<ContractVariableDto> GetContractVariables()
    {
        return
        [
            new()
            {
                Key = "{nro_contrato}",
                Label = "Número de contrato",
                Group = "Contrato",
            },
            new()
            {
                Key = "{fecha_suscripcion_contrato_letras}",
                Label = "Fecha de suscripción (en letras)",
                Group = "Contrato",
            },
            new()
            {
                Key = "{honorifico_cliente}",
                Label = "Honorífico del cliente",
                Group = "Cliente",
            },
            new()
            {
                Key = "{nombre_cliente}",
                Label = "Nombre o razón social",
                Group = "Cliente",
            },
            new()
            {
                Key = "{dni_cliente}",
                Label = "DNI o RUC",
                Group = "Cliente",
            },
            new()
            {
                Key = "{estado_civil_cliente}",
                Label = "Estado civil",
                Group = "Cliente",
            },
            new()
            {
                Key = "{ocupacion_cliente}",
                Label = "Ocupación",
                Group = "Cliente",
            },
            new()
            {
                Key = "{domicilio_cliente}",
                Label = "Domicilio",
                Group = "Cliente",
            },
            new()
            {
                Key = "{distrito_cliente}",
                Label = "Distrito",
                Group = "Cliente",
            },
            new()
            {
                Key = "{provincia_cliente}",
                Label = "Provincia",
                Group = "Cliente",
            },
            new()
            {
                Key = "{departamento_cliente}",
                Label = "Departamento",
                Group = "Cliente",
            },
            new()
            {
                Key = "{representantes_legales}",
                Label = "Representantes legales / copropietarios",
                Group = "Cliente",
            },
            new()
            {
                Key = "{nombre_proyecto}",
                Label = "Nombre del proyecto",
                Group = "Proyecto",
            },
            new()
            {
                Key = "{nro_lote}",
                Label = "Número de lote",
                Group = "Proyecto",
            },
            new()
            {
                Key = "{manzana}",
                Label = "Manzana",
                Group = "Proyecto",
            },
            new()
            {
                Key = "{area_terreno}",
                Label = "Área del terreno",
                Group = "Proyecto",
            },
            new()
            {
                Key = "{precio_dolares_metro_cuadrado}",
                Label = "Precio por m²",
                Group = "Precio",
            },
            new()
            {
                Key = "{precio_departamento_dolares}",
                Label = "Precio del inmueble",
                Group = "Precio",
            },
            new()
            {
                Key = "{precio_departamento_dolares_letras}",
                Label = "Precio del inmueble (en letras)",
                Group = "Precio",
            },
            new()
            {
                Key = "{precio_cochera_dolares}",
                Label = "Precio de cochera",
                Group = "Precio",
            },
            new()
            {
                Key = "{precio_cochera_dolares_letras}",
                Label = "Precio de cochera (en letras)",
                Group = "Precio",
            },
            new()
            {
                Key = "{area_cochera}",
                Label = "Área de cochera",
                Group = "Precio",
            },
            new()
            {
                Key = "{nro_signada_cochera}",
                Label = "Número de cochera",
                Group = "Precio",
            },
            new()
            {
                Key = "{precio_total_dolares}",
                Label = "Precio total",
                Group = "Precio",
            },
            new()
            {
                Key = "{precio_total_dolares_letras}",
                Label = "Precio total (en letras)",
                Group = "Precio",
            },
            new()
            {
                Key = "{precio_inicial_dolares}",
                Label = "Cuota inicial",
                Group = "Precio",
            },
            new()
            {
                Key = "{precio_inicial_dolares_letras}",
                Label = "Cuota inicial (en letras)",
                Group = "Precio",
            },
        ];
    }

    public async Task<ProjectContractTemplateDto> GetByProjectIdAsync(Guid projectId)
    {
        await EnsureProjectExistsAsync(projectId);

        var template = await _context.ProjectContractTemplates.FirstOrDefaultAsync(t =>
            t.ProjectId == projectId
        );

        return MapToDto(projectId, template);
    }

    public async Task<ProjectContractTemplateDto> SaveAsync(
        Guid projectId,
        string htmlContent,
        Guid userId
    )
    {
        await EnsureProjectExistsAsync(projectId);

        var sanitized = _htmlService.Sanitize(htmlContent);
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new InvalidOperationException("El contrato no puede estar vacío");

        var template = await _context.ProjectContractTemplates.FirstOrDefaultAsync(t =>
            t.ProjectId == projectId
        );

        if (template == null)
        {
            template = new ProjectContractTemplate
            {
                ProjectId = projectId,
                HtmlContent = sanitized,
                CreatedByUserId = userId,
                ModifiedByUserId = userId,
            };
            _context.ProjectContractTemplates.Add(template);
        }
        else
        {
            template.HtmlContent = sanitized;
            template.ModifiedByUserId = userId;
            template.ModifiedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return MapToDto(projectId, template);
    }

    public async Task<ProjectContractTemplateDto> ImportFromDocxAsync(
        Guid projectId,
        IFormFile file,
        Guid userId
    )
    {
        await EnsureProjectExistsAsync(projectId);

        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Debe seleccionar un archivo Word (.docx)");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".docx")
            throw new InvalidOperationException("Solo se permiten archivos .docx");

        if (file.Length > ContractHtmlService.MaxDocxBytes)
            throw new InvalidOperationException("El archivo Word supera el tamaño máximo de 10 MB");

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var docxBytes = memoryStream.ToArray();

        var (htmlBytes, convertError) = _sofficeConverterService.convertTo(
            docxBytes,
            "docx",
            "html"
        );
        if (convertError != null)
            throw new InvalidOperationException($"Error al convertir el Word: {convertError}");

        var html = System.Text.Encoding.UTF8.GetString(htmlBytes);
        var body = _htmlService.ExtractBody(html);
        return await SaveAsync(projectId, body, userId);
    }

    public async Task<bool> DeleteAsync(Guid projectId)
    {
        await EnsureProjectExistsAsync(projectId);

        var template = await _context.ProjectContractTemplates.FirstOrDefaultAsync(t =>
            t.ProjectId == projectId
        );
        if (template == null)
            return false;

        _context.ProjectContractTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<byte[]> RenderFilledDocxAsync(
        Guid projectId,
        Dictionary<string, string> placeholders
    )
    {
        var template = await _context.ProjectContractTemplates.FirstOrDefaultAsync(t =>
            t.ProjectId == projectId
        );
        if (template == null || string.IsNullOrWhiteSpace(template.HtmlContent))
            throw new ArgumentException("Este proyecto no tiene contrato");

        var filledHtml = _htmlService.ReplaceVariables(template.HtmlContent, placeholders);
        var wrappedHtml = _htmlService.WrapForConversion(filledHtml);
        var htmlBytes = System.Text.Encoding.UTF8.GetBytes(wrappedHtml);

        var (docxBytes, convertError) = _sofficeConverterService.convertTo(
            htmlBytes,
            "html",
            "docx"
        );
        if (convertError != null)
            throw new ArgumentException($"Error al generar el contrato Word: {convertError}");

        return docxBytes;
    }

    private async Task EnsureProjectExistsAsync(Guid projectId)
    {
        var exists = await _context.Projects.AnyAsync(p => p.Id == projectId);
        if (!exists)
            throw new KeyNotFoundException("Proyecto no encontrado");
    }

    private static ProjectContractTemplateDto MapToDto(
        Guid projectId,
        ProjectContractTemplate? template
    )
    {
        if (template == null)
        {
            return new ProjectContractTemplateDto { Exists = false, ProjectId = projectId };
        }

        return new ProjectContractTemplateDto
        {
            Exists = true,
            Id = template.Id,
            ProjectId = template.ProjectId,
            HtmlContent = template.HtmlContent,
            CreatedAt = template.CreatedAt,
            ModifiedAt = template.ModifiedAt,
        };
    }
}
