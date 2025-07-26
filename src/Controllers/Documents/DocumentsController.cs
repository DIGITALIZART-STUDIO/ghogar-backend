using GestionHogar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionHogar.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController(
    WordTemplateService wordTemplateService,
    SofficeConverterService sofficeConverterService
) : ControllerBase
{
    [HttpGet("{id:guid}/pdf")]
    public ActionResult GenerateContractPdf(Guid id)
    {
        // Load template bytes
        var templatePath = "Templates/plantilla_contrato_gestion_hogar.docx";
        using var inputFileStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read);

        var placeholders = new Dictionary<string, string>()
        {
            { "{nro_contrato}", "" },
            { "{honorifico_cliente}", "" },
            { "{nombre_cliente}", "" },
            { "{dni_cliente}", "" },
            { "{estado_civil_cliente}", "" },
            { "{ocupacion_cliente}", "" },
            { "{domicilio_cliente}", "" },
            { "{distrito_cliente}", "" },
            { "{provincia_cliente}", "" },
            { "{departamento_cliente}", "" },
            { "{nombre_proyecto}", "" },
            { "{precio_dolares_metro_cuadrado}", "" },
            { "{area_terreno}", "" },
            { "{precio_departamento_dolares}", "" },
            { "{precio_departamento_dolares_letras}", "" },
            { "{precio_cochera_dolares}", "" },
            { "{precio_cochera_dolares_letras}", "" },
            { "{area_cochera}", "" },
            { "{nro_signada_cochera}", "" },
            { "{precio_total_dolares}", "" },
            { "{precio_total_dolares_letras}", "" },
            { "{precio_inicial_dolares}", "" },
            { "{precio_inicial_dolares_letras}", "" },
            { "{fecha_suscripcion_contrato_letras}", "" },
        };

        // Fill template
        var (filledBytes, fillError) = wordTemplateService.ReplacePlaceholders(
            inputFileStream,
            placeholders
        );
        if (fillError != null)
            return BadRequest(fillError);

        // Convert to PDF
        var (pdfBytes, pdfError) = sofficeConverterService.ConvertToPdf(filledBytes, "docx");
        if (pdfError != null)
            return BadRequest(fillError);

        // Profit
        return File(pdfBytes, "application/pdf", $"contrato-{id}.pdf");
    }
}
