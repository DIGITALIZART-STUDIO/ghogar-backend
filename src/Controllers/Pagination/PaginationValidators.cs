using GestionHogar.Model;

namespace GestionHogar.Controllers;

/// <summary>
/// Validador personalizado para parámetros de paginación
/// </summary>
public static class PaginationValidators
{
    /// <summary>
    /// Resultado de validación simple
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }

        public static ValidationResult Success => new() { IsValid = true };

        public static ValidationResult Error(string message) =>
            new() { IsValid = false, ErrorMessage = message };
    }

    /// <summary>
    /// Valida una solicitud de paginación optimizada
    /// </summary>
    public static ValidationResult ValidateOptimizedPaginationRequest(
        OptimizedPaginationRequest request
    )
    {
        var errors = new List<string>();

        // Validar página
        if (request.Page < 1)
        {
            errors.Add("La página debe ser mayor o igual a 1");
        }

        // Validar pageSize
        if (request.PageSize < 1)
        {
            errors.Add("El tamaño de página debe ser mayor a 0");
        }

        if (request.PageSize > 1000)
        {
            errors.Add("El tamaño de página no puede exceder 1000");
        }

        // Validar tipo de entidad
        if (string.IsNullOrWhiteSpace(request.EntityType))
        {
            errors.Add("El tipo de entidad es requerido");
        }

        // Validar filtros
        if (request.Filters != null)
        {
            foreach (var filter in request.Filters)
            {
                if (string.IsNullOrWhiteSpace(filter.Field))
                {
                    errors.Add("El campo del filtro no puede estar vacío");
                }

                if (string.IsNullOrWhiteSpace(filter.Operator))
                {
                    errors.Add("El operador del filtro no puede estar vacío");
                }
            }
        }

        // Validar ordenamiento
        if (request.OrderBy != null)
        {
            foreach (var order in request.OrderBy)
            {
                if (string.IsNullOrWhiteSpace(order.Field))
                {
                    errors.Add("El campo de ordenamiento no puede estar vacío");
                }

                if (order.Direction != "asc" && order.Direction != "desc")
                {
                    errors.Add("La dirección de ordenamiento debe ser 'asc' o 'desc'");
                }
            }
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Error(string.Join("; ", errors));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Valida un pageSize
    /// </summary>
    public static ValidationResult ValidatePageSize(int pageSize, int maxPageSize = 1000)
    {
        if (pageSize < 1)
        {
            return ValidationResult.Error("El tamaño de página debe ser mayor a 0");
        }

        if (pageSize > maxPageSize)
        {
            return ValidationResult.Error($"El tamaño de página no puede exceder {maxPageSize}");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Valida un número de página
    /// </summary>
    public static ValidationResult ValidatePageNumber(int page, int minPage = 1)
    {
        if (page < minPage)
        {
            return ValidationResult.Error($"La página debe ser mayor o igual a {minPage}");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Valida un tipo de entidad
    /// </summary>
    public static ValidationResult ValidateEntityType(string entityType)
    {
        var allowedTypes = new[]
        {
            "Lead",
            "Client",
            "Reservation",
            "Quotation",
            "User",
            "Project",
            "Block",
            "Lot",
            "Payment",
            "PaymentTransaction",
            "LeadTask",
        };

        if (string.IsNullOrWhiteSpace(entityType))
        {
            return ValidationResult.Error("El tipo de entidad es requerido");
        }

        if (!allowedTypes.Contains(entityType))
        {
            return ValidationResult.Error(
                $"Tipo de entidad '{entityType}' no es válido. Tipos permitidos: {string.Join(", ", allowedTypes)}"
            );
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Valida filtros de paginación
    /// </summary>
    public static ValidationResult ValidateFilters(List<PaginationFilter>? filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errors = new List<string>();

        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field))
            {
                errors.Add("El campo del filtro no puede estar vacío");
            }

            if (string.IsNullOrWhiteSpace(filter.Operator))
            {
                errors.Add("El operador del filtro no puede estar vacío");
            }

            var validOperators = new[]
            {
                "eq",
                "ne",
                "gt",
                "gte",
                "lt",
                "lte",
                "contains",
                "startswith",
                "endswith",
            };
            if (!validOperators.Contains(filter.Operator.ToLower()))
            {
                errors.Add(
                    $"Operador '{filter.Operator}' no es válido. Operadores permitidos: {string.Join(", ", validOperators)}"
                );
            }
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Error(string.Join("; ", errors));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Valida ordenamiento de paginación
    /// </summary>
    public static ValidationResult ValidateOrderBy(List<PaginationOrderBy>? orderBy)
    {
        if (orderBy == null || orderBy.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errors = new List<string>();

        foreach (var order in orderBy)
        {
            if (string.IsNullOrWhiteSpace(order.Field))
            {
                errors.Add("El campo de ordenamiento no puede estar vacío");
            }

            if (order.Direction != "asc" && order.Direction != "desc")
            {
                errors.Add("La dirección de ordenamiento debe ser 'asc' o 'desc'");
            }
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Error(string.Join("; ", errors));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Valida includes de paginación
    /// </summary>
    public static ValidationResult ValidateIncludes(List<string>? includes)
    {
        if (includes == null || includes.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errors = new List<string>();

        foreach (var include in includes)
        {
            if (string.IsNullOrWhiteSpace(include))
            {
                errors.Add("El include no puede estar vacío");
            }
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Error(string.Join("; ", errors));
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Valida TTL de caché
    /// </summary>
    public static ValidationResult ValidateCacheTTL(int? cacheTTLMinutes)
    {
        if (cacheTTLMinutes.HasValue)
        {
            if (cacheTTLMinutes.Value < 1)
            {
                return ValidationResult.Error("El TTL de caché debe ser mayor a 0");
            }

            if (cacheTTLMinutes.Value > 1440) // 24 horas
            {
                return ValidationResult.Error(
                    "El TTL de caché no puede exceder 1440 minutos (24 horas)"
                );
            }
        }

        return ValidationResult.Success;
    }
}
