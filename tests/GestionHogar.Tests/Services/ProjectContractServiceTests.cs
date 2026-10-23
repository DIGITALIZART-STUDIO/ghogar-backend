using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionHogar.Tests.Services;

public class ProjectContractServiceTests
{
    [Fact]
    public void GetContractVariables_HasUniqueKeysAndExpectedGroups()
    {
        var service = CreateService(out _);

        var variables = service.GetContractVariables();
        var keys = variables.Select(v => v.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains(variables, v => v.Key == "{nro_contrato}" && v.Group == "Contrato");
        Assert.Contains(variables, v => v.Key == "{nombre_cliente}" && v.Group == "Cliente");
        Assert.Contains(variables, v => v.Key == "{nro_lote}" && v.Group == "Proyecto");
        Assert.Contains(
            variables,
            v => v.Key == "{precio_total_dolares_letras}" && v.Group == "Precio"
        );
        Assert.All(variables, v => Assert.StartsWith("{", v.Key));
        Assert.All(variables, v => Assert.EndsWith("}", v.Key));

        string[] reservationPlaceholderKeys =
        [
            "{nro_contrato}",
            "{honorifico_cliente}",
            "{nombre_cliente}",
            "{dni_cliente}",
            "{estado_civil_cliente}",
            "{ocupacion_cliente}",
            "{domicilio_cliente}",
            "{distrito_cliente}",
            "{provincia_cliente}",
            "{departamento_cliente}",
            "{nombre_proyecto}",
            "{nro_lote}",
            "{manzana}",
            "{precio_dolares_metro_cuadrado}",
            "{area_terreno}",
            "{precio_departamento_dolares}",
            "{precio_departamento_dolares_letras}",
            "{precio_cochera_dolares}",
            "{precio_cochera_dolares_letras}",
            "{area_cochera}",
            "{nro_signada_cochera}",
            "{precio_total_dolares}",
            "{precio_total_dolares_letras}",
            "{precio_inicial_dolares}",
            "{precio_inicial_dolares_letras}",
            "{fecha_suscripcion_contrato_letras}",
            "{representantes_legales}",
        ];

        var catalog = keys.ToHashSet();
        Assert.All(reservationPlaceholderKeys, key => Assert.Contains(key, catalog));
    }

    [Fact]
    public async Task GetByProjectIdAsync_WhenMissingTemplate_ReturnsExistsFalse()
    {
        var service = CreateService(out var context);
        var projectId = await SeedProjectAsync(context);

        var result = await service.GetByProjectIdAsync(projectId);

        Assert.False(result.Exists);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Null(result.HtmlContent);
    }

    [Fact]
    public async Task GetByProjectIdAsync_WhenProjectDoesNotExist_ThrowsKeyNotFound()
    {
        var service = CreateService(out _);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetByProjectIdAsync(Guid.NewGuid())
        );
    }

    [Fact]
    public async Task SaveAsync_CreatesAndThenUpdatesTemplate()
    {
        var service = CreateService(out var context);
        var projectId = await SeedProjectAsync(context);
        var userId = Guid.NewGuid();

        var created = await service.SaveAsync(
            projectId,
            "<p>Contrato {nro_contrato}</p>",
            userId
        );

        Assert.True(created.Exists);
        Assert.Equal(projectId, created.ProjectId);
        Assert.Contains("{nro_contrato}", created.HtmlContent);
        Assert.DoesNotContain("<script", created.HtmlContent ?? string.Empty);

        var updated = await service.SaveAsync(
            projectId,
            "<p>Actualizado</p><script>alert(1)</script>",
            Guid.NewGuid()
        );

        Assert.Equal(created.Id, updated.Id);
        Assert.Contains("Actualizado", updated.HtmlContent);
        Assert.DoesNotContain("<script", updated.HtmlContent ?? string.Empty);
        Assert.Equal(1, await context.ProjectContractTemplates.CountAsync());
    }

    [Fact]
    public async Task SaveAsync_WhenHtmlIsEmpty_ThrowsInvalidOperation()
    {
        var service = CreateService(out var context);
        var projectId = await SeedProjectAsync(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(projectId, "   ", Guid.NewGuid())
        );

        Assert.Equal("El contrato no puede estar vacío", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingTemplate()
    {
        var service = CreateService(out var context);
        var projectId = await SeedProjectAsync(context);
        await service.SaveAsync(projectId, "<p>Contrato</p>", Guid.NewGuid());

        var deleted = await service.DeleteAsync(projectId);
        var remaining = await service.GetByProjectIdAsync(projectId);

        Assert.True(deleted);
        Assert.False(remaining.Exists);
        Assert.False(await context.ProjectContractTemplates.AnyAsync());
    }

    [Fact]
    public async Task DeleteAsync_WhenNoTemplate_ReturnsFalse()
    {
        var service = CreateService(out var context);
        var projectId = await SeedProjectAsync(context);

        var deleted = await service.DeleteAsync(projectId);

        Assert.False(deleted);
    }

    [Fact]
    public async Task ImportFromDocxAsync_WhenFileIsMissing_ThrowsInvalidOperation()
    {
        var service = CreateService(out var context);
        var projectId = await SeedProjectAsync(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ImportFromDocxAsync(projectId, null!, Guid.NewGuid())
        );

        Assert.Equal("Debe seleccionar un archivo Word (.docx)", ex.Message);
    }

    [Fact]
    public async Task ImportFromDocxAsync_WhenExtensionIsNotDocx_ThrowsInvalidOperation()
    {
        var service = CreateService(out var context);
        var projectId = await SeedProjectAsync(context);
        var file = CreateFormFile("contrato.pdf", [1, 2, 3]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ImportFromDocxAsync(projectId, file, Guid.NewGuid())
        );

        Assert.Equal("Solo se permiten archivos .docx", ex.Message);
    }

    [Fact]
    public async Task RenderFilledDocxAsync_WhenProjectHasNoTemplate_ThrowsArgumentException()
    {
        var service = CreateService(out var context);
        var projectId = await SeedProjectAsync(context);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.RenderFilledDocxAsync(projectId, new Dictionary<string, string>())
        );

        Assert.Equal("Este proyecto no tiene contrato", ex.Message);
    }

    private static ProjectContractService CreateService(out DatabaseContext context)
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new DatabaseContext(options);
        var htmlService = new ContractHtmlService();
        var soffice = new SofficeConverterService(NullLogger<SofficeConverterService>.Instance);

        return new ProjectContractService(context, htmlService, soffice);
    }

    private static async Task<Guid> SeedProjectAsync(DatabaseContext context)
    {
        var project = new Project
        {
            Name = "Proyecto Test",
            Location = "Lima",
            Currency = "USD",
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    private static IFormFile CreateFormFile(string fileName, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream",
        };
    }
}
