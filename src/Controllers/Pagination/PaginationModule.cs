using GestionHogar.Model;
using GestionHogar.Services;
using Microsoft.Extensions.Caching.Memory;

namespace GestionHogar.Controllers;

/// <summary>
/// Módulo de configuración para servicios de paginación optimizados
/// </summary>
public class PaginationModule : IModule
{
    public IServiceCollection SetupModule(IServiceCollection services, IConfiguration configuration)
    {
        // Registrar servicios de paginación básicos
        services.AddScoped<PaginationService>(provider =>
        {
            var logger = provider.GetService<ILogger<PaginationService>>();
            return new PaginationService(logger);
        });

        // Registrar servicios avanzados de paginación
        services.AddScoped<AdvancedPaginationService>(provider =>
        {
            var logger = provider.GetService<ILogger<AdvancedPaginationService>>();
            var cache = provider.GetService<IMemoryCache>();
            return new AdvancedPaginationService(logger, cache);
        });

        // Registrar servicios de proyección
        services.AddScoped<ProjectionService>(provider =>
        {
            var logger = provider.GetService<ILogger<ProjectionService>>();
            return new ProjectionService(logger);
        });

        services.AddScoped<IncludeOptimizationService>(provider =>
        {
            var logger = provider.GetService<ILogger<IncludeOptimizationService>>();
            return new IncludeOptimizationService(logger);
        });

        // Registrar servicio de caché
        services.AddScoped<PaginationCacheService>(provider =>
        {
            var cache = provider.GetRequiredService<IMemoryCache>();
            var logger = provider.GetService<ILogger<PaginationCacheService>>();
            return new PaginationCacheService(cache, logger);
        });

        // Registrar servicios optimizados
        services.AddScoped<OptimizedPaginationService>(provider =>
        {
            var logger = provider.GetService<ILogger<OptimizedPaginationService>>();
            var cache = provider.GetService<IMemoryCache>();
            var config = new PaginationConfiguration();
            return new OptimizedPaginationService(logger, cache, config);
        });

        // Registrar servicio de métricas
        services.AddScoped<PaginationMetricsService>(provider =>
        {
            var logger = provider.GetService<ILogger<PaginationMetricsService>>();
            var cache = provider.GetService<IMemoryCache>();
            return new PaginationMetricsService(logger, cache);
        });

        // Registrar servicios avanzados
        services.AddScoped<StreamingPaginationService>(provider =>
        {
            var logger = provider.GetService<ILogger<StreamingPaginationService>>();
            var config = new PaginationConfiguration();
            return new StreamingPaginationService(logger, config);
        });

        services.AddScoped<ConcurrencyOptimizedPaginationService>(provider =>
        {
            var logger = provider.GetService<ILogger<ConcurrencyOptimizedPaginationService>>();
            var cache = provider.GetService<IMemoryCache>();
            var config = new ConcurrencyPaginationConfiguration();
            return new ConcurrencyOptimizedPaginationService(logger, cache, config);
        });

        services.AddScoped<PaginationAlertService>(provider =>
        {
            var logger = provider.GetService<ILogger<PaginationAlertService>>();
            var cache = provider.GetService<IMemoryCache>();
            var config = new PaginationAlertConfiguration();
            return new PaginationAlertService(logger, cache, config);
        });

        services.AddScoped<CompiledQueryService>(provider =>
        {
            var logger = provider.GetService<ILogger<CompiledQueryService>>();
            return new CompiledQueryService(logger);
        });

        services.AddScoped<PaginationCompiledQueryService>(provider =>
        {
            var compiledQueryService = provider.GetRequiredService<CompiledQueryService>();
            var logger = provider.GetService<ILogger<PaginationCompiledQueryService>>();
            return new PaginationCompiledQueryService(compiledQueryService, logger);
        });

        // Registrar repositorios optimizados para entidades principales
        services.AddScoped<OptimizedPaginationRepository<Lead>>(provider =>
        {
            var context = provider.GetRequiredService<DatabaseContext>();
            var logger = provider.GetService<ILogger<OptimizedPaginationRepository<Lead>>>();
            var cache = provider.GetService<IMemoryCache>();
            var config = new PaginationConfiguration();
            return new OptimizedPaginationRepository<Lead>(context, logger, cache, config);
        });

        services.AddScoped<OptimizedPaginationRepository<Model.Client>>(provider =>
        {
            var context = provider.GetRequiredService<DatabaseContext>();
            var logger = provider.GetService<
                ILogger<OptimizedPaginationRepository<Model.Client>>
            >();
            var cache = provider.GetService<IMemoryCache>();
            var config = new PaginationConfiguration();
            return new OptimizedPaginationRepository<Model.Client>(context, logger, cache, config);
        });

        services.AddScoped<OptimizedPaginationRepository<Reservation>>(provider =>
        {
            var context = provider.GetRequiredService<DatabaseContext>();
            var logger = provider.GetService<ILogger<OptimizedPaginationRepository<Reservation>>>();
            var cache = provider.GetService<IMemoryCache>();
            var config = new PaginationConfiguration();
            return new OptimizedPaginationRepository<Reservation>(context, logger, cache, config);
        });

        services.AddScoped<OptimizedPaginationRepository<Quotation>>(provider =>
        {
            var context = provider.GetRequiredService<DatabaseContext>();
            var logger = provider.GetService<ILogger<OptimizedPaginationRepository<Quotation>>>();
            var cache = provider.GetService<IMemoryCache>();
            var config = new PaginationConfiguration();
            return new OptimizedPaginationRepository<Quotation>(context, logger, cache, config);
        });

        services.AddScoped<OptimizedPaginationRepository<User>>(provider =>
        {
            var context = provider.GetRequiredService<DatabaseContext>();
            var logger = provider.GetService<ILogger<OptimizedPaginationRepository<User>>>();
            var cache = provider.GetService<IMemoryCache>();
            var config = new PaginationConfiguration();
            return new OptimizedPaginationRepository<User>(context, logger, cache, config);
        });

        services.AddScoped<OptimizedPaginationRepository<Project>>(provider =>
        {
            var context = provider.GetRequiredService<DatabaseContext>();
            var logger = provider.GetService<ILogger<OptimizedPaginationRepository<Project>>>();
            var cache = provider.GetService<IMemoryCache>();
            var config = new PaginationConfiguration();
            return new OptimizedPaginationRepository<Project>(context, logger, cache, config);
        });

        // Configurar caché de memoria si no está registrado
        if (!services.Any(s => s.ServiceType == typeof(IMemoryCache)))
        {
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = 1000; // Límite de entradas
                options.CompactionPercentage = 0.25; // Porcentaje de compactación
                options.ExpirationScanFrequency = TimeSpan.FromMinutes(5); // Frecuencia de limpieza
            });
        }

        return services;
    }
}
