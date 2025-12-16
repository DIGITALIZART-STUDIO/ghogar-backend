using GestionHogar.Controllers.Dtos;
using GestionHogar.Model;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionHogar.Services;

public class ReservationService : IReservationService
{
    private readonly DatabaseContext _context;
    private readonly PaginationService _paginationService;
    private readonly OdsTemplateService _odsTemplateService;
    private readonly SofficeConverterService _sofficeConverterService;
    private readonly WordTemplateService _wordTemplateService;

    public ReservationService(
        DatabaseContext context,
        PaginationService paginationService,
        OdsTemplateService odsTemplateService,
        SofficeConverterService sofficeConverterService,
        WordTemplateService wordTemplateService
    )
    {
        _context = context;
        _paginationService = paginationService;
        _odsTemplateService = odsTemplateService;
        _sofficeConverterService = sofficeConverterService;
        _wordTemplateService = wordTemplateService;
    }

    public async Task<IEnumerable<ReservationDto>> GetAllReservationsAsync()
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                TotalAmountRequired = r.TotalAmountRequired,
                RemainingAmount = r.RemainingAmount,
                PaymentHistory = r.PaymentHistory,
                Currency = r.Currency,
                Status = r.Status,
                ContractValidationStatus = r.ContractValidationStatus,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CoOwners = r.CoOwners,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .ToListAsync();
    }

    public async Task<PaginatedResponseV2<ReservationDto>> GetAllReservationsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        ReservationStatus[]? status = null,
        PaymentMethod[]? paymentMethod = null,
        Guid? projectId = null,
        string? orderBy = null
    )
    {
        var query = _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(r => r.IsActive)
            .AsQueryable();

        // Aplicar filtro de búsqueda
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(r =>
                (r.Client.Name != null && r.Client.Name.ToLower().Contains(searchTerm))
                || (r.Client.Email != null && r.Client.Email.ToLower().Contains(searchTerm))
                || (r.Client.PhoneNumber != null && r.Client.PhoneNumber.Contains(searchTerm))
                || (r.Client.Dni != null && r.Client.Dni.Contains(searchTerm))
                || (r.Client.Ruc != null && r.Client.Ruc.Contains(searchTerm))
                || (
                    r.Client.CompanyName != null
                    && r.Client.CompanyName.ToLower().Contains(searchTerm)
                )
                || (r.Quotation.Code != null && r.Quotation.Code.ToLower().Contains(searchTerm))
            );
        }

        // Aplicar filtro por estado
        if (status != null && status.Length > 0)
        {
            query = query.Where(r => status.Contains(r.Status));
        }

        // Aplicar filtro por método de pago
        if (paymentMethod != null && paymentMethod.Length > 0)
        {
            query = query.Where(r => paymentMethod.Contains(r.PaymentMethod));
        }

        // Aplicar filtro por proyecto
        if (projectId.HasValue)
        {
            query = query.Where(r => r.Quotation.Lot.Block.Project.Id == projectId.Value);
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var orderParts = orderBy.Split(' ');
            var field = orderParts[0].ToLower();
            var direction =
                orderParts.Length > 1 && orderParts[1].ToLower() == "desc" ? "desc" : "asc";

            query = field switch
            {
                "reservationdate" => direction == "desc"
                    ? query.OrderByDescending(r => r.ReservationDate)
                    : query.OrderBy(r => r.ReservationDate),
                "amountpaid" => direction == "desc"
                    ? query.OrderByDescending(r => r.AmountPaid)
                    : query.OrderBy(r => r.AmountPaid),
                "status" => direction == "desc"
                    ? query.OrderByDescending(r => r.Status)
                    : query.OrderBy(r => r.Status),
                "paymentmethod" => direction == "desc"
                    ? query.OrderByDescending(r => r.PaymentMethod)
                    : query.OrderBy(r => r.PaymentMethod),
                "client.name" => direction == "desc"
                    ? query.OrderByDescending(r => r.Client.Name)
                    : query.OrderBy(r => r.Client.Name),
                _ => query.OrderByDescending(r => r.CreatedAt),
            };
        }
        else
        {
            query = query.OrderByDescending(r => r.CreatedAt);
        }

        // Convertir a DTOs antes de paginar
        var reservationDtos = query.Select(r => new ReservationDto
        {
            Id = r.Id,
            ClientId = r.ClientId,
            ClientName = r.Client.DisplayName,
            QuotationId = r.QuotationId,
            QuotationCode = r.Quotation.Code,
            ReservationDate = r.ReservationDate,
            AmountPaid = r.AmountPaid,
            TotalAmountRequired = r.TotalAmountRequired,
            RemainingAmount = r.RemainingAmount,
            PaymentHistory = r.PaymentHistory,
            Currency = r.Currency,
            Status = r.Status,
            ContractValidationStatus = r.ContractValidationStatus,
            PaymentMethod = r.PaymentMethod,
            BankName = r.BankName,
            ExchangeRate = r.ExchangeRate,
            ExpiresAt = r.ExpiresAt,
            Notified = r.Notified,
            Schedule = r.Schedule,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt,
        });

        return await paginationService.PaginateAsync(reservationDtos, page, pageSize);
    }

    public async Task<PaginatedResponseV2<ReservationDto>> GetReservationsByAdvisorIdPaginatedAsync(
        Guid advisorId,
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        ReservationStatus[]? status = null,
        PaymentMethod[]? paymentMethod = null,
        Guid? projectId = null,
        string? orderBy = null
    )
    {
        var query = _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Lead)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(r => r.IsActive && r.Quotation.Lead.AssignedToId == advisorId)
            .AsQueryable();

        // Aplicar filtro de búsqueda
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(r =>
                (r.Client.Name != null && r.Client.Name.ToLower().Contains(searchTerm))
                || (r.Client.Email != null && r.Client.Email.ToLower().Contains(searchTerm))
                || (r.Client.PhoneNumber != null && r.Client.PhoneNumber.Contains(searchTerm))
                || (r.Client.Dni != null && r.Client.Dni.Contains(searchTerm))
                || (r.Client.Ruc != null && r.Client.Ruc.Contains(searchTerm))
                || (
                    r.Client.CompanyName != null
                    && r.Client.CompanyName.ToLower().Contains(searchTerm)
                )
                || (r.Quotation.Code != null && r.Quotation.Code.ToLower().Contains(searchTerm))
            );
        }

        // Aplicar filtro por estado
        if (status != null && status.Length > 0)
        {
            query = query.Where(r => status.Contains(r.Status));
        }

        // Aplicar filtro por método de pago
        if (paymentMethod != null && paymentMethod.Length > 0)
        {
            query = query.Where(r => paymentMethod.Contains(r.PaymentMethod));
        }

        // Aplicar filtro por proyecto
        if (projectId.HasValue)
        {
            query = query.Where(r => r.Quotation.Lot.Block.Project.Id == projectId.Value);
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var orderParts = orderBy.Split(' ');
            var field = orderParts[0].ToLower();
            var direction =
                orderParts.Length > 1 && orderParts[1].ToLower() == "desc" ? "desc" : "asc";

            query = field switch
            {
                "reservationdate" => direction == "desc"
                    ? query.OrderByDescending(r => r.ReservationDate)
                    : query.OrderBy(r => r.ReservationDate),
                "amountpaid" => direction == "desc"
                    ? query.OrderByDescending(r => r.AmountPaid)
                    : query.OrderBy(r => r.AmountPaid),
                "status" => direction == "desc"
                    ? query.OrderByDescending(r => r.Status)
                    : query.OrderBy(r => r.Status),
                "paymentmethod" => direction == "desc"
                    ? query.OrderByDescending(r => r.PaymentMethod)
                    : query.OrderBy(r => r.PaymentMethod),
                "client.name" => direction == "desc"
                    ? query.OrderByDescending(r => r.Client.Name)
                    : query.OrderBy(r => r.Client.Name),
                _ => query.OrderByDescending(r => r.CreatedAt),
            };
        }
        else
        {
            query = query.OrderByDescending(r => r.CreatedAt);
        }

        // Convertir a DTOs antes de paginar
        var reservationDtos = query.Select(r => new ReservationDto
        {
            Id = r.Id,
            ClientId = r.ClientId,
            ClientName = r.Client.DisplayName,
            QuotationId = r.QuotationId,
            QuotationCode = r.Quotation.Code,
            ReservationDate = r.ReservationDate,
            AmountPaid = r.AmountPaid,
            TotalAmountRequired = r.TotalAmountRequired,
            RemainingAmount = r.RemainingAmount,
            PaymentHistory = r.PaymentHistory,
            Currency = r.Currency,
            Status = r.Status,
            ContractValidationStatus = r.ContractValidationStatus,
            PaymentMethod = r.PaymentMethod,
            BankName = r.BankName,
            ExchangeRate = r.ExchangeRate,
            ExpiresAt = r.ExpiresAt,
            Notified = r.Notified,
            Schedule = r.Schedule,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt,
        });

        return await paginationService.PaginateAsync(reservationDtos, page, pageSize);
    }

    public async Task<
        PaginatedResponseV2<ReservationPendingValidationDto>
    > GetAllCanceledPendingValidationReservationsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        string? search = null,
        ReservationStatus[]? status = null,
        PaymentMethod[]? paymentMethod = null,
        ContractValidationStatus[]? contractValidationStatus = null,
        Guid? projectId = null,
        string? orderBy = null
    )
    {
        var query = _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Where(r =>
                r.IsActive
                && r.Status == ReservationStatus.CANCELED
                && (
                    r.ContractValidationStatus == ContractValidationStatus.PendingValidation
                    || r.ContractValidationStatus == ContractValidationStatus.Validated
                )
            )
            .AsQueryable();

        // Aplicar filtro de búsqueda
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(r =>
                (r.Client.Name != null && r.Client.Name.ToLower().Contains(searchTerm))
                || (r.Client.Email != null && r.Client.Email.ToLower().Contains(searchTerm))
                || (r.Client.PhoneNumber != null && r.Client.PhoneNumber.Contains(searchTerm))
                || (r.Client.Dni != null && r.Client.Dni.Contains(searchTerm))
                || (r.Client.Ruc != null && r.Client.Ruc.Contains(searchTerm))
                || (
                    r.Client.CompanyName != null
                    && r.Client.CompanyName.ToLower().Contains(searchTerm)
                )
                || (r.Quotation.Code != null && r.Quotation.Code.ToLower().Contains(searchTerm))
            );
        }

        // Aplicar filtro por estado
        if (status != null && status.Length > 0)
        {
            query = query.Where(r => status.Contains(r.Status));
        }

        // Aplicar filtro por método de pago
        if (paymentMethod != null && paymentMethod.Length > 0)
        {
            query = query.Where(r => paymentMethod.Contains(r.PaymentMethod));
        }

        // Aplicar filtro por estado de validación de contrato
        if (contractValidationStatus != null && contractValidationStatus.Length > 0)
        {
            query = query.Where(r => contractValidationStatus.Contains(r.ContractValidationStatus));
        }

        // Aplicar filtro por proyecto
        if (projectId.HasValue)
        {
            query = query.Where(r => r.Quotation.Lot.Block.Project.Id == projectId.Value);
        }

        // Aplicar ordenamiento
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var orderParts = orderBy.Split(' ');
            var field = orderParts[0].ToLower();
            var direction =
                orderParts.Length > 1 && orderParts[1].ToLower() == "desc" ? "desc" : "asc";

            query = field switch
            {
                "reservationdate" => direction == "desc"
                    ? query.OrderByDescending(r => r.ReservationDate)
                    : query.OrderBy(r => r.ReservationDate),
                "amountpaid" => direction == "desc"
                    ? query.OrderByDescending(r => r.AmountPaid)
                    : query.OrderBy(r => r.AmountPaid),
                "status" => direction == "desc"
                    ? query.OrderByDescending(r => r.Status)
                    : query.OrderBy(r => r.Status),
                "paymentmethod" => direction == "desc"
                    ? query.OrderByDescending(r => r.PaymentMethod)
                    : query.OrderBy(r => r.PaymentMethod),
                "contractvalidationstatus" => direction == "desc"
                    ? query.OrderByDescending(r => r.ContractValidationStatus)
                    : query.OrderBy(r => r.ContractValidationStatus),
                "createdat" => direction == "desc"
                    ? query.OrderByDescending(r => r.CreatedAt)
                    : query.OrderBy(r => r.CreatedAt),
                _ => query.OrderByDescending(r => r.CreatedAt),
            };
        }
        else
        {
            query = query.OrderByDescending(r => r.CreatedAt);
        }

        // Convertir a DTOs antes de paginar (mantener como IQueryable de EF)
        var reservationDtos = query.Select(r => new ReservationPendingValidationDto
        {
            Id = r.Id,
            ClientId = r.ClientId,
            ClientName = r.Client.DisplayName,
            QuotationId = r.QuotationId,
            QuotationCode = r.Quotation.Code,
            ReservationDate = r.ReservationDate,
            AmountPaid = r.AmountPaid,
            TotalAmountRequired = r.TotalAmountRequired,
            RemainingAmount = r.RemainingAmount,
            PaymentHistory = r.PaymentHistory, // Mantener como string, se deserializará en el frontend
            Currency = r.Currency,
            Status = r.Status,
            ContractValidationStatus = r.ContractValidationStatus,
            PaymentMethod = r.PaymentMethod,
            BankName = r.BankName,
            ExchangeRate = r.ExchangeRate,
            ExpiresAt = r.ExpiresAt,
            Notified = r.Notified,
            Schedule = r.Schedule,
            CoOwners = r.CoOwners,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt,
        });

        return await paginationService.PaginateAsync(reservationDtos, page, pageSize);
    }

    public async Task<IEnumerable<ReservationWithPaymentsDto>> GetAllCanceledReservationsAsync()
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Include(r => r.Payments)
            .Where(r => r.IsActive && r.Status == ReservationStatus.CANCELED)
            .Select(r => new ReservationWithPaymentsDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                TotalAmountRequired = r.TotalAmountRequired,
                RemainingAmount = r.RemainingAmount,
                PaymentHistory = r.PaymentHistory,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CoOwners = r.CoOwners,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
                PaymentCount = r.Payments.Count(p => p.Paid), // Solo pagos realizados
                NextPaymentDueDate = r
                    .Payments.Where(p => !p.Paid)
                    .OrderBy(p => p.DueDate)
                    .Select(p => (DateTime?)p.DueDate)
                    .FirstOrDefault(),
            })
            .ToListAsync();
    }

    public async Task<
        PaginatedResponseV2<ReservationWithPaymentsDto>
    > GetAllCanceledReservationsPaginatedAsync(int page, int pageSize, Guid? projectId = null)
    {
        var query = _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Lot)
            .ThenInclude(l => l.Block)
            .ThenInclude(b => b.Project)
            .Include(r => r.Payments)
            .Where(r =>
                r.IsActive
                && r.Status == ReservationStatus.CANCELED
                && r.ContractValidationStatus == ContractValidationStatus.Validated
            );

        // Aplicar filtro por proyecto si se proporciona
        if (projectId.HasValue)
        {
            query = query.Where(r => r.Quotation.Lot.Block.ProjectId == projectId.Value);
        }

        var projectedQuery = query.Select(r => new ReservationWithPaymentsDto
        {
            Id = r.Id,
            ClientId = r.ClientId,
            ClientName = r.Client.DisplayName,
            QuotationId = r.QuotationId,
            QuotationCode = r.Quotation.Code,
            ReservationDate = r.ReservationDate,
            AmountPaid = r.AmountPaid,
            TotalAmountRequired = r.TotalAmountRequired,
            RemainingAmount = r.RemainingAmount,
            PaymentHistory = r.PaymentHistory,
            Currency = r.Currency,
            Status = r.Status,
            PaymentMethod = r.PaymentMethod,
            BankName = r.BankName,
            ExchangeRate = r.ExchangeRate,
            ExpiresAt = r.ExpiresAt,
            Notified = r.Notified,
            Schedule = r.Schedule,
            CoOwners = r.CoOwners,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt,
            PaymentCount = r.Payments.Count(p => p.Paid),
            NextPaymentDueDate = r
                .Payments.Where(p => !p.Paid)
                .OrderBy(p => p.DueDate)
                .Select(p => (DateTime?)p.DueDate)
                .FirstOrDefault(),
        });

        return await _paginationService.PaginateAsync(projectedQuery, page, pageSize);
    }

    public async Task<ReservationDto?> GetReservationByIdAsync(Guid id)
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.Id == id && r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CoOwners = r.CoOwners,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<Reservation> CreateReservationAsync(ReservationCreateDto reservationDto)
    {
        // Verificar que la cotización existe y obtener el lead asociado
        var quotation =
            await _context
                .Quotations.Include(q => q.Lead)
                .ThenInclude(l => l.Client)
                .FirstOrDefaultAsync(q => q.Id == reservationDto.QuotationId)
            ?? throw new ArgumentException("La cotización especificada no existe");

        // Verificar que el lead existe
        if (quotation.Lead == null)
        {
            throw new ArgumentException("La cotización no tiene un lead asociado");
        }

        // Verificar que el cliente del lead existe y está activo
        if (quotation.Lead.Client == null || !quotation.Lead.Client.IsActive)
        {
            throw new ArgumentException("El cliente asociado al lead no existe o está inactivo");
        }

        var clientId = quotation.Lead.ClientId!.Value;
        var client = quotation.Lead.Client;

        // Verificar que no exista ya una reserva activa para esta cotización
        var existingReservation = await _context.Reservations.FirstOrDefaultAsync(r =>
            r.QuotationId == reservationDto.QuotationId && r.IsActive
        );
        if (existingReservation != null)
        {
            throw new ArgumentException("Ya existe una reserva activa para esta cotización");
        }

        var reservation = new Reservation
        {
            ClientId = clientId,
            QuotationId = reservationDto.QuotationId,
            ReservationDate = reservationDto.ReservationDate,
            AmountPaid = 0, // Inicializar en 0 porque aún no se ha pagado
            TotalAmountRequired = reservationDto.AmountPaid, // El monto del DTO es lo que debe pagar
            RemainingAmount = reservationDto.AmountPaid, // Al inicio, todo está pendiente
            Currency = reservationDto.Currency,
            PaymentMethod = reservationDto.PaymentMethod,
            BankName = reservationDto.BankName,
            ExchangeRate = reservationDto.ExchangeRate,
            ExpiresAt = reservationDto.ExpiresAt,
            Schedule = reservationDto.Schedule,
            CoOwners = reservationDto.CoOwners,
            Status = ReservationStatus.ISSUED,
            Notified = false,
            Client = client,
            Quotation = quotation,
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task<bool> ToggleContractValidationStatusAsync(Guid reservationId)
    {
        var reservation = await _context.Reservations.FindAsync(reservationId);
        if (reservation == null)
            return false;

        reservation.ContractValidationStatus =
            reservation.ContractValidationStatus == ContractValidationStatus.Validated
                ? ContractValidationStatus.None
                : ContractValidationStatus.Validated;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<
        PaginatedResponseV2<ReservationWithPendingPaymentsDto>
    > GetAllReservationsWithPendingPaymentsPaginatedAsync(
        int page,
        int pageSize,
        PaginationService paginationService,
        Guid currentUserId,
        List<string> currentUserRoles,
        string? search = null,
        ReservationStatus[]? status = null,
        PaymentMethod[]? paymentMethod = null,
        ContractValidationStatus[]? contractValidationStatus = null,
        Guid? projectId = null,
        string? orderBy = null
    )
    {
        // Verificar si es Supervisor
        var isSupervisor = currentUserRoles.Contains("Supervisor");

        var query = _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Include(r => r.Quotation.Lot)
            .Include(r => r.Quotation.Lot.Block)
            .Include(r => r.Quotation.Lot.Block.Project)
            .Include(r => r.Payments)
            .Where(r =>
                (r.Status == ReservationStatus.ISSUED || r.Status == ReservationStatus.CANCELED)
                && r.IsActive
            );

        // FILTRO ESPECIAL PARA SUPERVISORES: Solo mostrar reservas que tienen leads asignados a sus SalesAdvisors o al propio supervisor
        if (isSupervisor)
        {
            // Obtener los IDs de los SalesAdvisors asignados a este supervisor
            var assignedSalesAdvisorIds = await _context
                .SupervisorSalesAdvisors.Where(ssa =>
                    ssa.SupervisorId == currentUserId && ssa.IsActive
                )
                .Select(ssa => ssa.SalesAdvisorId)
                .ToListAsync();

            // Incluir también el propio ID del supervisor para que vea sus propios clientes
            assignedSalesAdvisorIds.Add(currentUserId);

            // Filtrar reservas que tienen leads asignados a estos usuarios
            query = query.Where(r =>
                _context.Leads.Any(l =>
                    l.ClientId == r.ClientId
                    && l.IsActive
                    && (
                        l.AssignedToId.HasValue
                        && (
                            assignedSalesAdvisorIds.Contains(l.AssignedToId.Value)
                            || l.AssignedToId.Value == currentUserId
                        )
                    )
                )
            );
        }

        // Aplicar filtro por proyecto si se proporciona
        if (projectId.HasValue)
        {
            query = query.Where(r => r.Quotation.Lot.Block.ProjectId == projectId.Value);
        }

        // Aplicar filtros de status si se proporcionan
        if (status != null && status.Length > 0)
        {
            query = query.Where(r => status.Contains(r.Status));
        }

        // Aplicar filtros de paymentMethod si se proporcionan
        if (paymentMethod != null && paymentMethod.Length > 0)
        {
            query = query.Where(r => paymentMethod.Contains(r.PaymentMethod));
        }

        // Aplicar filtros de contractValidationStatus si se proporcionan
        if (contractValidationStatus != null && contractValidationStatus.Length > 0)
        {
            query = query.Where(r => contractValidationStatus.Contains(r.ContractValidationStatus));
        }

        // Aplicar filtro de búsqueda si se proporciona
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLower();
            query = query.Where(r =>
                (r.Client.Name != null && r.Client.Name.ToLower().Contains(searchTerm))
                || (r.Client.Email != null && r.Client.Email.ToLower().Contains(searchTerm))
                || (r.Client.PhoneNumber != null && r.Client.PhoneNumber.Contains(searchTerm))
                || (r.Client.Dni != null && r.Client.Dni.Contains(searchTerm))
                || (r.Client.Ruc != null && r.Client.Ruc.Contains(searchTerm))
                || (
                    r.Client.CompanyName != null
                    && r.Client.CompanyName.ToLower().Contains(searchTerm)
                )
                || (
                    r.Quotation.Lot.Block.Project.Name != null
                    && r.Quotation.Lot.Block.Project.Name.ToLower().Contains(searchTerm)
                )
                || (
                    r.Quotation.Lot.LotNumber != null
                    && r.Quotation.Lot.LotNumber.Contains(searchTerm)
                )
            );
        }

        // Ejecutar paginación optimizada
        var paginatedResult = await _paginationService.PaginateAsync(query, page, pageSize);

        // Convertir el resultado paginado a ReservationWithPendingPaymentsDto
        var dtoResult = new List<ReservationWithPendingPaymentsDto>();

        foreach (var reservation in paginatedResult.Data)
        {
            // Obtener cuotas pendientes para esta reserva
            var pendingPayments = await GetPendingPaymentsForReservationAsync(reservation.Id);

            // Calcular totales
            var totalAmountDue = pendingPayments.Sum(p => p.AmountDue);
            var totalAmountPaid = pendingPayments.Sum(p => p.AmountPaid);
            var totalRemainingAmount = pendingPayments.Sum(p => p.RemainingAmount);
            var nextPaymentDueDate = pendingPayments
                .Where(p => p.RemainingAmount > 0)
                .OrderBy(p => p.DueDate)
                .FirstOrDefault()
                ?.DueDate;

            dtoResult.Add(
                new ReservationWithPendingPaymentsDto
                {
                    Id = reservation.Id,
                    ReservationDate = reservation.ReservationDate.ToDateTime(TimeOnly.MinValue),
                    AmountPaid = reservation.AmountPaid,
                    PaymentMethod = reservation.PaymentMethod,
                    Status = reservation.Status,
                    ContractValidationStatus = reservation.ContractValidationStatus,
                    Currency = reservation.Currency,
                    ExchangeRate = reservation.ExchangeRate,
                    ExpiresAt = reservation.ExpiresAt,
                    CreatedAt = reservation.CreatedAt,
                    ModifiedAt = reservation.ModifiedAt,

                    Client = new ClientDto
                    {
                        Id = reservation.Client.Id,
                        Name = reservation.Client.Name ?? string.Empty,
                        Dni = reservation.Client.Dni ?? string.Empty,
                        Ruc = reservation.Client.Ruc,
                        Email = reservation.Client.Email,
                        PhoneNumber = reservation.Client.PhoneNumber,
                    },

                    Lot = new LotDto
                    {
                        Id = reservation.Quotation.Lot.Id,
                        LotNumber = reservation.Quotation.Lot.LotNumber,
                        Area = reservation.Quotation.Lot.Area,
                        Price = reservation.Quotation.Lot.Price,
                    },

                    Project = new ProjectDto
                    {
                        Id = reservation.Quotation.Lot.Block.Project.Id,
                        Name = reservation.Quotation.Lot.Block.Project.Name,
                        Location = reservation.Quotation.Lot.Block.Project.Location,
                    },

                    Quotation = new QuotationDto
                    {
                        Id = reservation.Quotation.Id,
                        Code = reservation.Quotation.Code,
                        FinalPrice = reservation.Quotation.FinalPrice,
                        MonthsFinanced = reservation.Quotation.MonthsFinanced,
                        QuotaAmount =
                            reservation.Quotation.FinalPrice / reservation.Quotation.MonthsFinanced,
                    },

                    PendingPayments = pendingPayments,

                    TotalAmountDue = totalAmountDue,
                    TotalAmountPaid = totalAmountPaid,
                    TotalRemainingAmount = totalRemainingAmount,
                    TotalPendingQuotas = pendingPayments.Count(p => p.RemainingAmount > 0),
                    NextPaymentDueDate = nextPaymentDueDate,
                }
            );
        }

        return new PaginatedResponseV2<ReservationWithPendingPaymentsDto>
        {
            Data = dtoResult,
            Meta = paginatedResult.Meta,
        };
    }

    private async Task<List<PendingPaymentDto>> GetPendingPaymentsForReservationAsync(
        Guid reservationId
    )
    {
        var payments = await _context
            .Payments.Where(p => p.ReservationId == reservationId && p.IsActive)
            .OrderBy(p => p.DueDate)
            .ToListAsync();

        var paymentIds = payments.Select(p => p.Id).ToList();
        var paymentDetails = await _context
            .PaymentTransactionPayments.Where(ptp => paymentIds.Contains(ptp.PaymentId))
            .ToListAsync();

        var paymentsByPaymentId = paymentDetails
            .GroupBy(ptp => ptp.PaymentId)
            .ToDictionary(g => g.Key, g => g.Sum(ptp => ptp.AmountPaid));

        var result = new List<PendingPaymentDto>();

        foreach (var payment in payments)
        {
            var totalPaidForThisPayment = paymentsByPaymentId.GetValueOrDefault(payment.Id, 0);
            var remainingAmount = payment.AmountDue - totalPaidForThisPayment;
            var isOverdue = payment.DueDate < DateTime.UtcNow && remainingAmount > 0;

            result.Add(
                new PendingPaymentDto
                {
                    Id = payment.Id,
                    DueDate = payment.DueDate,
                    AmountDue = payment.AmountDue,
                    AmountPaid = totalPaidForThisPayment,
                    RemainingAmount = Math.Max(0, remainingAmount),
                    IsOverdue = isOverdue,
                }
            );
        }

        return result;
    }

    public async Task<ReservationDto?> UpdateReservationAsync(
        Guid id,
        ReservationUpdateDto reservationDto
    )
    {
        var reservation = await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

        if (reservation == null)
            return null;

        // Update the reservation properties
        reservation.ReservationDate = reservationDto.ReservationDate;
        reservation.TotalAmountRequired = reservationDto.AmountPaid; // El monto del DTO es lo que debe pagar
        reservation.RemainingAmount = reservationDto.AmountPaid - reservation.AmountPaid; // Recalcular pendiente
        reservation.Currency = reservationDto.Currency;
        reservation.Status = reservationDto.Status;
        reservation.PaymentMethod = reservationDto.PaymentMethod;
        reservation.BankName = reservationDto.BankName;
        reservation.ExchangeRate = reservationDto.ExchangeRate;
        reservation.ExpiresAt =
            reservationDto.ExpiresAt.Kind == DateTimeKind.Utc
                ? reservationDto.ExpiresAt
                : DateTime.SpecifyKind(reservationDto.ExpiresAt, DateTimeKind.Utc);
        reservation.Notified = reservationDto.Notified;
        reservation.Schedule = reservationDto.Schedule;
        reservation.CoOwners = reservationDto.CoOwners;
        reservation.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Return the updated reservation as DTO
        return new ReservationDto
        {
            Id = reservation.Id,
            ClientId = reservation.ClientId,
            ClientName = reservation.Client.DisplayName,
            QuotationId = reservation.QuotationId,
            QuotationCode = reservation.Quotation.Code,
            ReservationDate = reservation.ReservationDate,
            AmountPaid = reservation.AmountPaid,
            TotalAmountRequired = reservation.TotalAmountRequired,
            RemainingAmount = reservation.RemainingAmount,
            PaymentHistory = reservation.PaymentHistory,
            Currency = reservation.Currency,
            Status = reservation.Status,
            PaymentMethod = reservation.PaymentMethod,
            BankName = reservation.BankName,
            ExchangeRate = reservation.ExchangeRate,
            ExpiresAt = reservation.ExpiresAt,
            Notified = reservation.Notified,
            Schedule = reservation.Schedule,
            CoOwners = reservation.CoOwners,
            CreatedAt = reservation.CreatedAt,
            ModifiedAt = reservation.ModifiedAt,
        };
    }

    public async Task<bool> DeleteReservationAsync(Guid id)
    {
        var reservation = await _context.Reservations.FirstOrDefaultAsync(r =>
            r.Id == id && r.IsActive
        );
        if (reservation == null)
            return false;

        // Borrado lógico
        reservation.IsActive = false;
        reservation.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByClientIdAsync(Guid clientId)
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.ClientId == clientId && r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CoOwners = r.CoOwners,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByQuotationIdAsync(
        Guid quotationId
    )
    {
        return await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .Where(r => r.QuotationId == quotationId && r.IsActive)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                ClientId = r.ClientId,
                ClientName = r.Client.DisplayName,
                QuotationId = r.QuotationId,
                QuotationCode = r.Quotation.Code,
                ReservationDate = r.ReservationDate,
                AmountPaid = r.AmountPaid,
                Currency = r.Currency,
                Status = r.Status,
                PaymentMethod = r.PaymentMethod,
                BankName = r.BankName,
                ExchangeRate = r.ExchangeRate,
                ExpiresAt = r.ExpiresAt,
                Notified = r.Notified,
                Schedule = r.Schedule,
                CoOwners = r.CoOwners,
                CreatedAt = r.CreatedAt,
                ModifiedAt = r.ModifiedAt,
            })
            .ToListAsync();
    }

    public async Task<ReservationDto?> ChangeStatusAsync(Guid id, ReservationStatusDto statusDto)
    {
        var reservation = await _context
            .Reservations.Include(r => r.Client)
            .Include(r => r.Quotation)
            .ThenInclude(q => q.Lot)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

        if (
            reservation == null
            || !Enum.TryParse<ReservationStatus>(statusDto.Status, true, out var statusEnum)
        )
            return null;

        var previousStatus = reservation.Status;
        reservation.Status = statusEnum;
        reservation.ModifiedAt = DateTime.UtcNow;

        // Manejar pagos si se proporciona información de pago
        if (statusDto.IsFullPayment.HasValue)
        {
            if (statusDto.IsFullPayment.Value)
            {
                // Pago completo: AmountPaid = TotalAmountRequired, RemainingAmount = 0
                reservation.AmountPaid = reservation.TotalAmountRequired;
                reservation.RemainingAmount = 0;

                // Agregar al PaymentHistory si se proporciona información del pago
                if (statusDto.PaymentDate.HasValue)
                {
                    await AddPaymentToHistoryFromStatusChangeAsync(reservation, statusDto);
                }
            }
            else if (statusDto.PaymentAmount.HasValue)
            {
                // Pago parcial: agregar al AmountPaid y recalcular RemainingAmount
                reservation.AmountPaid += statusDto.PaymentAmount.Value;
                reservation.RemainingAmount = Math.Max(
                    0,
                    reservation.TotalAmountRequired - reservation.AmountPaid
                );

                // Agregar al PaymentHistory si se proporciona información del pago
                if (statusDto.PaymentDate.HasValue)
                {
                    await AddPaymentToHistoryFromStatusChangeAsync(reservation, statusDto);
                }
            }
        }
        else if (statusDto.PaymentAmount.HasValue)
        {
            // Si no se especifica IsFullPayment pero sí PaymentAmount, tratarlo como pago parcial
            reservation.AmountPaid += statusDto.PaymentAmount.Value;
            reservation.RemainingAmount = Math.Max(
                0,
                reservation.TotalAmountRequired - reservation.AmountPaid
            );

            // Agregar al PaymentHistory si se proporciona información del pago
            if (statusDto.PaymentDate.HasValue)
            {
                await AddPaymentToHistoryFromStatusChangeAsync(reservation, statusDto);
            }
        }

        // **NUEVO: Actualizar estado del lote según el cambio de estado de la reserva**
        if (reservation.Quotation?.Lot != null)
        {
            switch (statusEnum)
            {
                case ReservationStatus.CANCELED:
                    // Si se cancela la reserva, el lote pasa a reservado
                    reservation.Quotation.Lot.Status = LotStatus.Reserved;
                    break;

                case ReservationStatus.ANULATED:
                    // Si se anula la reserva, el lote pasa a disponible
                    reservation.Quotation.Lot.Status = LotStatus.Available;
                    break;

                case ReservationStatus.ISSUED:
                    // Si vuelve a emitida desde cancelada/anulada, el lote pasa a cotizado
                    reservation.Quotation.Lot.Status = LotStatus.Quoted;
                    break;
            }

            reservation.Quotation.Lot.ModifiedAt = DateTime.UtcNow;
        }

        // Actualiza el estado de validación de contrato según el nuevo estado
        if (statusEnum == ReservationStatus.CANCELED)
        {
            reservation.ContractValidationStatus = ContractValidationStatus.PendingValidation;
            // Si tienes lógica para pagos, la mantienes aquí
            if (previousStatus != ReservationStatus.CANCELED)
            {
                await GeneratePaymentScheduleAsync(reservation);
            }
        }
        else
        {
            reservation.ContractValidationStatus = ContractValidationStatus.None;
            // Aquí podrías limpiar pagos si lo necesitas
        }

        await _context.SaveChangesAsync();

        // Return the updated reservation as DTO
        return new ReservationDto
        {
            Id = reservation.Id,
            ClientId = reservation.ClientId,
            ClientName = reservation.Client.DisplayName,
            QuotationId = reservation.QuotationId,
            QuotationCode = reservation.Quotation.Code,
            ReservationDate = reservation.ReservationDate,
            AmountPaid = reservation.AmountPaid,
            TotalAmountRequired = reservation.TotalAmountRequired,
            RemainingAmount = reservation.RemainingAmount,
            PaymentHistory = reservation.PaymentHistory,
            Currency = reservation.Currency,
            Status = reservation.Status,
            PaymentMethod = reservation.PaymentMethod,
            BankName = reservation.BankName,
            ExchangeRate = reservation.ExchangeRate,
            ExpiresAt = reservation.ExpiresAt,
            Notified = reservation.Notified,
            Schedule = reservation.Schedule,
            CoOwners = reservation.CoOwners,
            CreatedAt = reservation.CreatedAt,
            ModifiedAt = reservation.ModifiedAt,
        };
    }

    /// <summary>
    /// Generates payment schedule when reservation status changes to CANCELED (which means "paid").
    /// Creates n monthly payment entities based on the quotation's financing terms.
    /// </summary>
    private Task GeneratePaymentScheduleAsync(Reservation reservation)
    {
        var quotation = reservation.Quotation;

        // Get the financed amount and months from the quotation
        var financedAmount = quotation.AmountFinanced;
        var monthsFinanced = quotation.MonthsFinanced;

        // Parse quotation date (it's stored as string, ugh...)
        if (!DateTime.TryParse(quotation.QuotationDate, out var quotationDate))
        {
            quotationDate = DateTime.UtcNow;
        }

        // Calculate monthly payment amount
        var monthlyPayment = financedAmount / monthsFinanced;

        // Generate payment entities
        var payments = new List<Payment>();

        for (int i = 1; i <= monthsFinanced; i++)
        {
            var dueDate = quotationDate.AddMonths(i);

            var payment = new Payment
            {
                ReservationId = reservation.Id,
                Reservation = reservation,
                DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc),
                AmountDue = monthlyPayment,
                Paid = false,
            };

            payments.Add(payment);
        }

        // Add all payments to the context
        _context.Payments.AddRange(payments);

        return Task.CompletedTask;
    }

    public async Task<byte[]> GenerateReservationPdfAsync(Guid reservationId)
    {
        var reservation =
            await _context
                .Reservations.Include(r => r.Client)
                .Include(r => r.Quotation)
                .ThenInclude(q => q.Lot)
                .ThenInclude(q => q.Block)
                .ThenInclude(b => b.Project)
                .Include(r => r.Quotation)
                .ThenInclude(q => q.Lead)
                .ThenInclude(l => l.AssignedTo)
                .FirstOrDefaultAsync(r => r.Id == reservationId)
            ?? throw new ArgumentException("Reserva no encontrada");

        var client = reservation.Client;
        var quotation = reservation.Quotation;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        // Fechas
                        x.Item()
                            .PaddingVertical(10)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(100);
                                    columns.RelativeColumn(5);
                                    columns.ConstantColumn(100);
                                    columns.RelativeColumn(2);
                                    columns.ConstantColumn(40);
                                    columns.ConstantColumn(30);
                                    columns.RelativeColumn(6);
                                });

                                table
                                    .Cell()
                                    .RowSpan(2)
                                    .Border(1)
                                    .Padding(5)
                                    .Text($"Fecha: {reservation.ReservationDate:dd/MM/yyyy}");

                                table.Cell().RowSpan(2);

                                table
                                    .Cell()
                                    .RowSpan(2)
                                    .Border(1)
                                    .Padding(5)
                                    .Text($"Vence: {reservation.ExpiresAt:dd/MM/yyyy}");

                                table.Cell().RowSpan(2);

                                table.Cell().Text("SOLES");
                                table.Cell().Text("S/.");
                                if (reservation.Currency == Currency.SOLES)
                                {
                                    table
                                        .Cell()
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text($"S/ {reservation.AmountPaid:N2}");
                                }
                                else
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("");
                                }

                                table.Cell().Text("DOLARES");
                                table.Cell().Text("US$");
                                if (reservation.Currency == Currency.DOLARES)
                                {
                                    table
                                        .Cell()
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text($"$ {reservation.AmountPaid:N2}");
                                }
                                else
                                {
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Black).Text("");
                                }
                            });

                        // Header
                        x.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(64);
                                    columns.ConstantColumn(35);
                                    columns.RelativeColumn(11);
                                    columns.ConstantColumn(45);
                                    columns.RelativeColumn(11);
                                    columns.ConstantColumn(35);
                                    columns.RelativeColumn(12);
                                });

                                // Nombre del cliente
                                {
                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .Text("Nombre del cliente:");

                                    table
                                        .Cell()
                                        .ColumnSpan(3)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.Name ?? "");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(10)
                                        .Text("D.N.I.");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.Dni ?? "");
                                }

                                // Nombre del conyugue
                                {
                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .Text("Nombre del cónyuge:");

                                    table
                                        .Cell()
                                        .ColumnSpan(3)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text("");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(10)
                                        .Text("D.N.I.");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text("");
                                }

                                // Co-propietario
                                {
                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .Text("Nombre del Co-propietario:");

                                    string coOwnerName = "";
                                    string coOwnerDni = "";

                                    // Procesar el JSON de co-owners para extraer solo el primero
                                    if (!string.IsNullOrEmpty(client?.CoOwners))
                                    {
                                        try
                                        {
                                            // Deserializar el JSON a una lista de objetos anónimos
                                            var coOwners =
                                                System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                                                    client.CoOwners
                                                );

                                            // Verificar que sea un array y tenga al menos un elemento
                                            if (
                                                coOwners.ValueKind
                                                    == System.Text.Json.JsonValueKind.Array
                                                && coOwners.GetArrayLength() > 0
                                            )
                                            {
                                                // Obtener el primer elemento
                                                var firstCoOwner = coOwners[0];

                                                // Intentar obtener la propiedad "name" del primer co-owner
                                                if (
                                                    firstCoOwner.TryGetProperty(
                                                        "name",
                                                        out var nameElement
                                                    )
                                                )
                                                {
                                                    coOwnerName = nameElement.GetString() ?? "";
                                                }

                                                // Intentar obtener la propiedad "dni" del primer co-owner
                                                if (
                                                    firstCoOwner.TryGetProperty(
                                                        "dni",
                                                        out var dniElement
                                                    )
                                                )
                                                {
                                                    coOwnerDni = dniElement.GetString() ?? "";
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            // En caso de error al procesar el JSON, dejar los campos vacíos
                                            coOwnerName = "";
                                            coOwnerDni = "";
                                        }
                                    }

                                    table
                                        .Cell()
                                        .ColumnSpan(3)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(coOwnerName);

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(10)
                                        .Text("D.N.I.");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(coOwnerDni);
                                }
                                // Razon social
                                {
                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .Text("Razón social:");

                                    table
                                        .Cell()
                                        .ColumnSpan(3)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.CompanyName ?? "");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(10)
                                        .Text("R.U.C.");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.Ruc ?? "");
                                }

                                // Direccion
                                {
                                    table.Cell().AlignLeft().PaddingVertical(5).Text("Dirección:");

                                    table
                                        .Cell()
                                        .ColumnSpan(6)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.Address ?? "");
                                }

                                // Email
                                {
                                    table.Cell().AlignLeft().PaddingVertical(5).Text("E-mail:");

                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.Email ?? "");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(5)
                                        .Text("Telef. Fijo");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text("");

                                    table
                                        .Cell()
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .PaddingLeft(5)
                                        .Text("Celular");

                                    table
                                        .Cell()
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text(client?.PhoneNumber ?? "");
                                }

                                // Importe de separacion
                                {
                                    table
                                        .Cell()
                                        .ColumnSpan(2)
                                        .AlignLeft()
                                        .PaddingVertical(5)
                                        .Text("Importe de Separación");

                                    var currencySymbol =
                                        reservation.Currency == Currency.SOLES ? "S/" : "$";
                                    table
                                        .Cell()
                                        .ColumnSpan(5)
                                        .PaddingVertical(5)
                                        .BorderBottom(1)
                                        .BorderColor(Colors.Black)
                                        .Text($"{currencySymbol} {reservation.AmountPaid:N2}");
                                }
                            });

                        // Payment method table
                        x.Item()
                            .PaddingTop(20)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(3);
                                });

                                table.Cell().AlignLeft().PaddingVertical(5).Text("EFECTIVO").Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .PaddingVertical(5)
                                    .Text(
                                        reservation.PaymentMethod == PaymentMethod.CASH ? "X" : ""
                                    );

                                table
                                    .Cell()
                                    .AlignLeft()
                                    .PaddingVertical(5)
                                    .PaddingLeft(10)
                                    .Text("DEPOSITO")
                                    .Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .PaddingVertical(5)
                                    .Text(
                                        reservation.PaymentMethod == PaymentMethod.BANK_DEPOSIT
                                            ? "X"
                                            : ""
                                    );

                                table
                                    .Cell()
                                    .AlignRight()
                                    .PaddingVertical(5)
                                    .PaddingHorizontal(10)
                                    .PaddingLeft(10)
                                    .Text("Banco");

                                table
                                    .Cell()
                                    .PaddingVertical(5)
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Black)
                                    .Text(reservation.BankName ?? "");
                            });

                        x.Item().PaddingTop(20).Text("Por el concepto de separación del");

                        // Custom table with spans
                        x.Item()
                            .PaddingTop(5)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(50);
                                    columns.RelativeColumn(10);
                                    // |Precio
                                    columns.ConstantColumn(40);
                                    // |Etapa
                                    columns.ConstantColumn(40);
                                    columns.RelativeColumn(5);
                                    // |Mz
                                    columns.ConstantColumn(30);
                                    columns.RelativeColumn(7);
                                    // |N lote
                                    columns.ConstantColumn(40);
                                    columns.RelativeColumn(7);
                                });

                                // First row with spans: 1, 3, 1, 1, 1, 1, 1, 1
                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Proyecto")
                                    .Bold();

                                table
                                    .Cell()
                                    .ColumnSpan(2)
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text(quotation?.ProjectName ?? "");

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Etapa")
                                    .Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text("");

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Mz.")
                                    .Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text(quotation?.BlockName ?? "");

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("N lote")
                                    .Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text(quotation?.LotNumber ?? "");

                                // Second row with spans: 1, 2, 1, 6 (remaining)
                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Area")
                                    .Bold();

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text($"{quotation?.AreaAtQuotation ?? 0} m²");

                                table
                                    .Cell()
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()
                                    .Text("Precio")
                                    .Bold();

                                table
                                    .Cell()
                                    .ColumnSpan(6)
                                    .Border(1)
                                    .BorderColor(Colors.Black)
                                    .Padding(5)
                                    .Text($"$ {quotation?.FinalPrice ?? 0:N2}");
                            });

                        x.Item()
                            .PaddingTop(20)
                            .Text(
                                "Declaración Jurada: El cliente por el presente declara que ha sido informado verbalmente por la empresa sobre el lote a adquirir, así como los datos de la empresa (RUC, Partida, Representante)."
                            );

                        // Numbered text blocks
                        x.Item()
                            .PaddingTop(10)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(30); // Fixed width for numbers
                                    columns.RelativeColumn(1); // Flexible width for text
                                });

                                // Item 1
                                table.Cell().AlignLeft().PaddingRight(10).Text("1.");

                                table
                                    .Cell()
                                    .AlignLeft()
                                    .Text(
                                        "Este documento es único y exclusivamente válido para la separación de un lote y tiene la condición de arras."
                                    );

                                // Item 2
                                table.Cell().AlignLeft().PaddingRight(10).PaddingTop(5).Text("2.");

                                table
                                    .Cell()
                                    .AlignLeft()
                                    .PaddingTop(5)
                                    .Text(
                                        "Este documento tiene la condición de arras de acuerdo al Art. 1478 del Código Civil, por lo cual el cliente tiene un plazo de 3 días naturales de realizar el depósito de la inicial acordada en el lota de venta, para suscribir al respectivo contrato de compraventa de bien futuro, vencido dicho plazo y sin producirse dicho depósito se entenderá que el cliente se retracta de la compra perdiendo las arras entregadas a favor de la empresa."
                                    );

                                // Item 3
                                table.Cell().AlignLeft().PaddingRight(10).PaddingTop(5).Text("3.");

                                table
                                    .Cell()
                                    .AlignLeft()
                                    .PaddingTop(5)
                                    .Text(
                                        "Si fuese la empresa quien se negase injustificadamente a suscribir el contrato de compraventa dentro del plazo establecido previamente, deberá devolver al cliente el arras que le fueron entregadas."
                                    );

                                // Item 4
                                table.Cell().AlignLeft().PaddingRight(10).PaddingTop(5).Text("4.");

                                table
                                    .Cell()
                                    .AlignLeft()
                                    .PaddingTop(5)
                                    .Text(
                                        "Todos los trámites relacionados a este documento y al contrato que pudieran firmarse en base al mismo son personales y deberán efectuarse únicamente en las oficinas de la empresa."
                                    );
                            });

                        // Signature section
                        x.Item()
                            .PaddingTop(80)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                // Signature labels with top border
                                table
                                    .Cell()
                                    .PaddingLeft(40)
                                    .PaddingRight(60)
                                    .BorderTop(1)
                                    .BorderColor(Colors.Black)
                                    .AlignCenter()
                                    .PaddingTop(5)
                                    .BorderColor(Colors.Black)
                                    .Text("LA EMPRESA")
                                    .Bold();

                                table
                                    .Cell()
                                    .PaddingLeft(60)
                                    .PaddingRight(40)
                                    .BorderTop(1)
                                    .BorderColor(Colors.Black)
                                    .AlignCenter()
                                    .PaddingTop(5)
                                    .Text("EL CLIENTE")
                                    .Bold();
                            });
                    });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> GenerateSchedulePdfAsync(Guid reservationId)
    {
        // Fetch the reservation with all related data
        var reservation =
            await _context
                .Reservations.Include(r => r.Client)
                .Include(r => r.Quotation)
                .ThenInclude(q => q.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(r => r.Payments) // All payments (paid and unpaid)
                .FirstOrDefaultAsync(r => r.Id == reservationId && r.IsActive)
            ?? throw new ArgumentException("Reserva no encontrada");

        var client = reservation.Client;
        var quotation = reservation.Quotation;
        var project = quotation?.Lot?.Block?.Project;
        var allPayments = reservation.Payments.OrderBy(p => p.DueDate).ToList();

        // Calculate totals
        var initialAmount = reservation.AmountPaid;
        var creditAmount = quotation?.AmountFinanced ?? 0;
        var totalSaleAmount = quotation?.FinalPrice ?? 0;

        // Format currency symbol
        var currencySymbol = reservation.Currency == Currency.SOLES ? "S/" : "$";

        // Static placeholders (unique data)
        var staticPlaceholders = new Dictionary<string, string>()
        {
            { "{NOMBRE_PROYECTO}", project?.Name?.ToUpper() ?? "" },
            { "{nombre_cliente}", client?.Name ?? client?.CompanyName ?? "" },
            { "{lote_cliente}", quotation?.LotNumber ?? "" },
            { "{total_inicial}", $"{currencySymbol} {initialAmount:N2}" },
            { "{total_credito}", $"{currencySymbol} {creditAmount:N2}" },
            { "{total_venta}", $"{currencySymbol} {totalSaleAmount:N2}" },
            { "{nro_cuotas}", allPayments.Count.ToString() },
        };

        // Dynamic placeholders (one row per payment in the schedule)
        var dynamicRowsData = new List<Dictionary<string, string>>();
        decimal remainingAmount = creditAmount;

        for (int i = 0; i < allPayments.Count; i++)
        {
            var payment = allPayments[i];

            // Calculate remaining amount AFTER this payment
            remainingAmount -= payment.AmountDue;

            var rowData = new Dictionary<string, string>()
            {
                { "{nro_cuota}", (i + 1).ToString() },
                { "{fecha_pago_cuota}", payment.DueDate.ToString("dd/MM/yyyy") },
                { "{monto_restante}", $"{currencySymbol} {remainingAmount:N2}" },
                { "{monto_cuota}", $"{currencySymbol} {payment.AmountDue:N2}" },
            };

            dynamicRowsData.Add(rowData);
        }

        // Load the ODS template
        var templatePath = "Templates/cronograma_de_pagos.ods";
        var templateBytes = await File.ReadAllBytesAsync(templatePath);

        // Fill template with dynamic rows (row 19 is 0-based, so 20 in 1-based)
        var (filledBytes, fillError) = _odsTemplateService.ReplacePlaceholdersWithDynamicRows(
            templateBytes,
            staticPlaceholders,
            dynamicRowsData,
            20 // Template row number (converting from 0-based 19 to 1-based 20)
        );
        if (fillError != null)
            throw new ArgumentException($"Error al procesar plantilla ODS: {fillError}");

        // Convert to PDF
        var (pdfBytes, pdfError) = _sofficeConverterService.ConvertToPdf(filledBytes, "ods");
        if (pdfError != null)
            throw new ArgumentException($"Error al convertir ODS a PDF: {pdfError}");

        return pdfBytes;
    }

    public async Task<byte[]> GenerateProcessedPaymentsPdfAsync(Guid reservationId)
    {
        // Fetch the reservation with all related data
        var reservation =
            await _context
                .Reservations.Include(r => r.Client)
                .Include(r => r.Quotation)
                .ThenInclude(q => q.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(r => r.Payments.Where(p => p.Paid)) // Only paid payments
                .FirstOrDefaultAsync(r => r.Id == reservationId && r.IsActive)
            ?? throw new ArgumentException("Reserva no encontrada");

        var client = reservation.Client;
        var quotation = reservation.Quotation;
        var project = quotation?.Lot?.Block?.Project;
        var paidPayments = reservation.Payments.Where(p => p.Paid).OrderBy(p => p.DueDate).ToList();

        // Calculate totals
        var initialAmount = reservation.AmountPaid;
        var creditAmount = quotation?.AmountFinanced ?? 0;
        var totalSaleAmount = quotation?.FinalPrice ?? 0;
        var totalPaidAmount = paidPayments.Sum(p => p.AmountDue);
        var currentBalance = creditAmount - totalPaidAmount;

        // Format currency symbol
        var currencySymbol = reservation.Currency == Currency.SOLES ? "S/" : "$";

        // Static placeholders (unique data)
        var staticPlaceholders = new Dictionary<string, string>()
        {
            { "{NOMBRE_PROYECTO}", project?.Name?.ToUpper() ?? "" },
            { "{nombre_cliente}", client?.Name ?? client?.CompanyName ?? "" },
            { "{lote_cliente}", quotation?.LotNumber ?? "" },
            { "{total_inicial}", $"{currencySymbol} {initialAmount:N2}" },
            { "{total_credito}", $"{currencySymbol} {creditAmount:N2}" },
            { "{total_venta}", $"{currencySymbol} {totalSaleAmount:N2}" },
            { "{fecha_y_hora_pago_inicial}", reservation.ReservationDate.ToString("dd/MM/yyyy") },
            { "{subtotal_monto_pagos}", $"{currencySymbol} {totalPaidAmount:N2}" },
            { "{subtotal_nro_cuotas}", paidPayments.Count.ToString() },
            { "{total_pagado}", $"{currencySymbol} {(initialAmount + totalPaidAmount):N2}" },
            { "{saldo_actual}", $"{currencySymbol} {currentBalance:N2}" },
        };

        // Dynamic placeholders (one row per payment)
        var dynamicRowsData = new List<Dictionary<string, string>>();
        decimal runningBalance = creditAmount;

        for (int i = 0; i < paidPayments.Count; i++)
        {
            var payment = paidPayments[i];
            runningBalance -= payment.AmountDue;

            var rowData = new Dictionary<string, string>()
            {
                { "{nro_pago}", (i + 1).ToString() },
                { "{fecha_y_hora_pago}", payment.DueDate.ToString("dd/MM/yyyy") },
                { "{monto_pago}", $"{currencySymbol} {payment.AmountDue:N2}" },
                { "{nro_cuotas}", "1" }, // Each payment represents 1 installment
                { "{saldo_restante}", $"{currencySymbol} {runningBalance:N2}" },
            };

            dynamicRowsData.Add(rowData);
        }

        // Load the ODS template
        var templatePath = "Templates/pagos_realizados.ods";
        var templateBytes = await File.ReadAllBytesAsync(templatePath);

        // Fill template with dynamic rows (row 24 is the template row)
        var (filledBytes, fillError) = _odsTemplateService.ReplacePlaceholdersWithDynamicRows(
            templateBytes,
            staticPlaceholders,
            dynamicRowsData,
            23 // Template row number
        );
        if (fillError != null)
            throw new ArgumentException($"Error al procesar plantilla ODS: {fillError}");

        // Convert to PDF
        var (pdfBytes, pdfError) = _sofficeConverterService.ConvertToPdf(filledBytes, "ods");
        if (pdfError != null)
            throw new ArgumentException($"Error al convertir ODS a PDF: {pdfError}");

        return pdfBytes;
    }

    public async Task<byte[]> GenerateReceiptPdfAsync(Guid reservationId)
    {
        // Fetch the reservation with all related data
        var reservation =
            await _context
                .Reservations.Include(r => r.Client)
                .Include(r => r.Quotation)
                .ThenInclude(q => q.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(r => r.Quotation)
                .ThenInclude(q => q.Lead)
                .ThenInclude(l => l.AssignedTo)
                .FirstOrDefaultAsync(r => r.Id == reservationId && r.IsActive)
            ?? throw new ArgumentException("Reserva no encontrada");

        var client = reservation.Client;
        var quotation = reservation.Quotation;
        var project = quotation?.Lot?.Block?.Project;
        var assignedUser = quotation?.Lead?.AssignedTo;

        // Format payment method in Spanish
        var paymentMethodText = reservation.PaymentMethod switch
        {
            PaymentMethod.CASH => "Efectivo",
            PaymentMethod.BANK_DEPOSIT => "Depósito Bancario",
            PaymentMethod.BANK_TRANSFER => "Transferencia Bancaria",
            _ => "No especificado",
        };

        // Format currency
        var currencySymbol = reservation.Currency == Currency.SOLES ? "S/" : "$";

        // Load the ODS template
        var templatePath = "Templates/recibo.ods";
        var templateBytes = await File.ReadAllBytesAsync(templatePath);

        var placeholders = new Dictionary<string, string>()
        {
            { "{NOMBRE_PROYECTO}", project?.Name?.ToUpper() ?? "" },
            { "{nombre_cliente}", client?.Name ?? client?.CompanyName ?? "" },
            { "{dni_cliente}", client?.Dni ?? client?.Ruc ?? "" },
            { "{emision_recibo}", reservation.ReservationDate.ToString("dd/MM/yyyy") },
            { "{lote_cliente}", quotation?.LotNumber ?? "" },
            { "{forma_pago_cliente}", paymentMethodText },
            { "{precio_total_venta}", $"{currencySymbol} {quotation?.FinalPrice ?? 0:N2}" },
            { "{nro_cuota}", "Separación" },
            { "{monto_cuota}", $"{currencySymbol} {reservation.AmountPaid:N2}" },
            { "{monto_cuota_total}", $"{currencySymbol} {reservation.AmountPaid:N2}" },
            {
                "{total_letras}",
                ConvertAmountToWords(reservation.AmountPaid, reservation.Currency)
            },
        };

        // Fill template
        var (filledBytes, fillError) = _odsTemplateService.ReplacePlaceholders(
            templateBytes,
            placeholders
        );
        if (fillError != null)
            throw new ArgumentException($"Error al procesar plantilla ODS: {fillError}");

        // Convert to PDF
        var (pdfBytes, pdfError) = _sofficeConverterService.ConvertToPdf(filledBytes, "ods");
        if (pdfError != null)
            throw new ArgumentException($"Error al convertir ODS a PDF: {pdfError}");

        return pdfBytes;
    }

    /// <summary>
    /// Converts a decimal amount to words in Spanish
    /// </summary>
    private string ConvertAmountToWords(decimal amount, Currency currency)
    {
        // This is a basic implementation - you might want to use a more comprehensive library
        var currencyName = currency == Currency.SOLES ? "SOLES" : "DÓLARES";
        var integerPart = (int)Math.Floor(amount);
        var decimalPart = (int)Math.Round((amount - integerPart) * 100);

        var words = ConvertNumberToWords(integerPart);

        if (decimalPart > 0)
        {
            words += $" CON {decimalPart:00}/100";
        }
        else
        {
            words += " CON 00/100";
        }

        return $"{words} {currencyName}";
    }

    /// <summary>
    /// Converts an integer to words in Spanish (basic implementation)
    /// </summary>
    private string ConvertNumberToWords(int number)
    {
        if (number == 0)
            return "CERO";

        var ones = new[]
        {
            "",
            "UNO",
            "DOS",
            "TRES",
            "CUATRO",
            "CINCO",
            "SEIS",
            "SIETE",
            "OCHO",
            "NUEVE",
        };
        var tens = new[]
        {
            "",
            "",
            "VEINTE",
            "TREINTA",
            "CUARENTA",
            "CINCUENTA",
            "SESENTA",
            "SETENTA",
            "OCHENTA",
            "NOVENTA",
        };
        var teens = new[]
        {
            "DIEZ",
            "ONCE",
            "DOCE",
            "TRECE",
            "CATORCE",
            "QUINCE",
            "DIECISÉIS",
            "DIECISIETE",
            "DIECIOCHO",
            "DIECINUEVE",
        };
        var hundreds = new[]
        {
            "",
            "CIENTO",
            "DOSCIENTOS",
            "TRESCIENTOS",
            "CUATROCIENTOS",
            "QUINIENTOS",
            "SEISCIENTOS",
            "SETECIENTOS",
            "OCHOCIENTOS",
            "NOVECIENTOS",
        };

        if (number < 10)
            return ones[number];
        if (number < 20)
            return teens[number - 10];
        if (number < 100)
        {
            var ten = number / 10;
            var one = number % 10;
            if (ten == 2 && one > 0)
                return $"VEINTI{ones[one]}";
            return tens[ten] + (one > 0 ? $" Y {ones[one]}" : "");
        }
        if (number < 1000)
        {
            var hundred = number / 100;
            var remainder = number % 100;
            var hundredText = hundred == 1 && remainder == 0 ? "CIEN" : hundreds[hundred];
            return hundredText + (remainder > 0 ? $" {ConvertNumberToWords(remainder)}" : "");
        }
        if (number < 1000000)
        {
            var thousand = number / 1000;
            var remainder = number % 1000;
            var thousandText = thousand == 1 ? "MIL" : $"{ConvertNumberToWords(thousand)} MIL";
            return thousandText + (remainder > 0 ? $" {ConvertNumberToWords(remainder)}" : "");
        }

        // For larger numbers, you'd need to extend this logic
        return number.ToString();
    }

    public async Task<byte[]> GenerateContractPdfAsync(Guid reservationId)
    {
        var docxBytes = await GenerateContractDocxAsync(reservationId);

        // Convert to PDF
        var (pdfBytes, pdfError) = _sofficeConverterService.ConvertToPdf(docxBytes, "docx");
        if (pdfError != null)
            throw new ArgumentException($"Error al convertir contrato a PDF: {pdfError}");

        return pdfBytes;
    }

    public async Task<byte[]> GenerateContractDocxAsync(Guid reservationId)
    {
        // Fetch the reservation with all related data
        var reservation =
            await _context
                .Reservations.Include(r => r.Client)
                .Include(r => r.Quotation)
                .ThenInclude(q => q.Lot)
                .ThenInclude(l => l.Block)
                .ThenInclude(b => b.Project)
                .Include(r => r.Quotation)
                .ThenInclude(q => q.Lead)
                .ThenInclude(l => l.AssignedTo)
                .FirstOrDefaultAsync(r => r.Id == reservationId && r.IsActive)
            ?? throw new ArgumentException("Reserva no encontrada");

        var client = reservation.Client;
        var quotation = reservation.Quotation;
        var project = quotation?.Lot?.Block?.Project;
        var lot = quotation?.Lot;
        var block = quotation?.Lot?.Block;

        // Generate contract number
        // FIXME: Generate from actual data
        var contractNumber =
            $"CNT-{reservation.ReservationDate:yyyyMMdd}-{reservationId.ToString()[..8].ToUpper()}";

        // Format client title (honorific)
        var clientTitle = client?.Type == ClientType.Natural ? "Sr./Sra." : "Empresa";

        // Load template
        var templatePath = "Templates/plantilla_contrato_gestion_hogar.docx";
        using var inputFileStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read);

        var placeholders = new Dictionary<string, string>()
        {
            { "{nro_contrato}", contractNumber },
            { "{honorifico_cliente}", clientTitle },
            { "{nombre_cliente}", client?.Name ?? client?.CompanyName ?? "" },
            { "{dni_cliente}", client?.Dni ?? client?.Ruc ?? "" },
            { "{estado_civil_cliente}", "" }, // You might want to add this field to the Client model
            { "{ocupacion_cliente}", "" }, // You might want to add this field to the Client model
            { "{domicilio_cliente}", client?.Address ?? "" },
            { "{distrito_cliente}", "" }, // You might want to add these address fields
            { "{provincia_cliente}", "" },
            { "{departamento_cliente}", client?.Country ?? "" },
            { "{nombre_proyecto}", project?.Name ?? "" },
            { "{precio_dolares_metro_cuadrado}", $"$ {quotation?.PricePerM2AtQuotation ?? 0:N2}" },
            { "{area_terreno}", $"{quotation?.AreaAtQuotation ?? 0:N2}" },
            { "{precio_departamento_dolares}", $"$ {quotation?.FinalPrice ?? 0:N2}" },
            {
                "{precio_departamento_dolares_letras}",
                ConvertAmountToWords(quotation?.FinalPrice ?? 0, Currency.DOLARES)
            },
            { "{precio_cochera_dolares}", "" }, // Parking space price - you might need to add this
            { "{precio_cochera_dolares_letras}", "" },
            { "{area_cochera}", "" }, // Parking space area
            { "{nro_signada_cochera}", "" }, // Parking space number
            { "{precio_total_dolares}", $"$ {quotation?.FinalPrice ?? 0:N2}" },
            {
                "{precio_total_dolares_letras}",
                ConvertAmountToWords(quotation?.FinalPrice ?? 0, Currency.DOLARES)
            },
            { "{precio_inicial_dolares}", $"$ {reservation.AmountPaid:N2}" },
            {
                "{precio_inicial_dolares_letras}",
                ConvertAmountToWords(reservation.AmountPaid, Currency.DOLARES)
            },
            {
                "{fecha_suscripcion_contrato_letras}",
                ConvertDateToWords(reservation.ReservationDate)
            },
        };

        // Fill template
        var (filledBytes, fillError) = _wordTemplateService.ReplacePlaceholders(
            inputFileStream,
            placeholders
        );
        if (fillError != null)
            throw new ArgumentException($"Error al procesar plantilla de contrato: {fillError}");

        return filledBytes;
    }

    /// <summary>
    /// Converts a DateOnly to words in Spanish
    /// </summary>
    private string ConvertDateToWords(DateOnly date)
    {
        var months = new[]
        {
            "",
            "enero",
            "febrero",
            "marzo",
            "abril",
            "mayo",
            "junio",
            "julio",
            "agosto",
            "septiembre",
            "octubre",
            "noviembre",
            "diciembre",
        };

        var dayWords = ConvertNumberToWords(date.Day).ToLower();
        var monthWord = months[date.Month];
        var yearWords = ConvertNumberToWords(date.Year).ToLower();

        return $"{dayWords} de {monthWord} del año {yearWords}";
    }

    // Payment History Management Methods

    public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(Guid reservationId)
    {
        var reservation = await _context.Reservations.FirstOrDefaultAsync(r =>
            r.Id == reservationId && r.IsActive
        );

        if (reservation == null)
            throw new ArgumentException("Reserva no encontrada");

        if (string.IsNullOrEmpty(reservation.PaymentHistory))
            return new List<PaymentHistoryDto>();

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            };
            var paymentHistory = System.Text.Json.JsonSerializer.Deserialize<
                List<PaymentHistoryDto>
            >(reservation.PaymentHistory, options);
            return paymentHistory ?? new List<PaymentHistoryDto>();
        }
        catch
        {
            return new List<PaymentHistoryDto>();
        }
    }

    public async Task<PaymentHistoryDto> AddPaymentToHistoryAsync(
        Guid reservationId,
        AddPaymentHistoryDto paymentDto
    )
    {
        var reservation = await _context.Reservations.FirstOrDefaultAsync(r =>
            r.Id == reservationId && r.IsActive
        );

        if (reservation == null)
            throw new ArgumentException("Reserva no encontrada");

        var paymentId = Guid.NewGuid().ToString();
        var newPayment = new PaymentHistoryDto
        {
            Id = paymentId,
            Date = paymentDto.Date,
            Amount = paymentDto.Amount,
            Method = paymentDto.Method,
            BankName = paymentDto.BankName,
            Reference = paymentDto.Reference,
            Status = paymentDto.Status,
            Notes = paymentDto.Notes,
        };

        var currentHistory = await GetPaymentHistoryAsync(reservationId);
        currentHistory.Add(newPayment);

        // Actualizar el JSON en la base de datos
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        reservation.PaymentHistory = System.Text.Json.JsonSerializer.Serialize(
            currentHistory,
            options
        );

        // Si el pago está confirmado, actualizar AmountPaid y RemainingAmount
        if (paymentDto.Status == PaymentStatus.CONFIRMED)
        {
            reservation.AmountPaid += paymentDto.Amount;
            reservation.RemainingAmount = Math.Max(
                0,
                reservation.TotalAmountRequired - reservation.AmountPaid
            );
        }

        reservation.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return newPayment;
    }

    public async Task<PaymentHistoryDto> UpdatePaymentInHistoryAsync(
        Guid reservationId,
        UpdatePaymentHistoryDto paymentDto
    )
    {
        var reservation = await _context.Reservations.FirstOrDefaultAsync(r =>
            r.Id == reservationId && r.IsActive
        );

        if (reservation == null)
            throw new ArgumentException("Reserva no encontrada");

        var currentHistory = await GetPaymentHistoryAsync(reservationId);
        var paymentIndex = currentHistory.FindIndex(p => p.Id == paymentDto.Id);

        if (paymentIndex == -1)
            throw new ArgumentException("Pago no encontrado en el historial");

        var oldPayment = currentHistory[paymentIndex];
        var oldAmount = oldPayment.Status == PaymentStatus.CONFIRMED ? oldPayment.Amount : 0;
        var newAmount = paymentDto.Status == PaymentStatus.CONFIRMED ? paymentDto.Amount : 0;

        // Actualizar el pago
        currentHistory[paymentIndex] = new PaymentHistoryDto
        {
            Id = paymentDto.Id,
            Date = paymentDto.Date,
            Amount = paymentDto.Amount,
            Method = paymentDto.Method,
            BankName = paymentDto.BankName,
            Reference = paymentDto.Reference,
            Status = paymentDto.Status,
            Notes = paymentDto.Notes,
        };

        // Actualizar el JSON en la base de datos
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        reservation.PaymentHistory = System.Text.Json.JsonSerializer.Serialize(
            currentHistory,
            options
        );

        // Recalcular AmountPaid y RemainingAmount
        reservation.AmountPaid = reservation.AmountPaid - oldAmount + newAmount;
        reservation.RemainingAmount = Math.Max(
            0,
            reservation.TotalAmountRequired - reservation.AmountPaid
        );

        reservation.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return currentHistory[paymentIndex];
    }

    public async Task<bool> RemovePaymentFromHistoryAsync(Guid reservationId, string paymentId)
    {
        var reservation = await _context.Reservations.FirstOrDefaultAsync(r =>
            r.Id == reservationId && r.IsActive
        );

        if (reservation == null)
            throw new ArgumentException("Reserva no encontrada");

        var currentHistory = await GetPaymentHistoryAsync(reservationId);
        var paymentIndex = currentHistory.FindIndex(p => p.Id == paymentId);

        if (paymentIndex == -1)
            return false;

        var paymentToRemove = currentHistory[paymentIndex];
        var amountToRemove =
            paymentToRemove.Status == PaymentStatus.CONFIRMED ? paymentToRemove.Amount : 0;

        // Remover el pago
        currentHistory.RemoveAt(paymentIndex);

        // Actualizar el JSON en la base de datos
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        reservation.PaymentHistory = System.Text.Json.JsonSerializer.Serialize(
            currentHistory,
            options
        );

        // Recalcular AmountPaid y RemainingAmount
        reservation.AmountPaid = Math.Max(0, reservation.AmountPaid - amountToRemove);
        reservation.RemainingAmount = Math.Max(
            0,
            reservation.TotalAmountRequired - reservation.AmountPaid
        );

        reservation.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Helper method to add payment to history when changing reservation status
    /// </summary>
    private async Task AddPaymentToHistoryFromStatusChangeAsync(
        Reservation reservation,
        ReservationStatusDto statusDto
    )
    {
        var paymentId = Guid.NewGuid().ToString();
        var paymentDate = statusDto.PaymentDate ?? DateTime.UtcNow;
        var paymentAmount = statusDto.PaymentAmount ?? reservation.TotalAmountRequired;
        var paymentMethod = statusDto.PaymentMethod ?? PaymentMethod.CASH;

        var newPayment = new PaymentHistoryDto
        {
            Id = paymentId,
            Date = paymentDate,
            Amount = paymentAmount,
            Method = paymentMethod,
            BankName = statusDto.BankName,
            Reference = statusDto.PaymentReference,
            Status = PaymentStatus.CONFIRMED, // Automáticamente confirmado cuando se cambia el estado
            Notes =
                statusDto.PaymentNotes ?? $"Pago confirmado al cambiar estado a {statusDto.Status}",
        };

        var currentHistory = await GetPaymentHistoryAsync(reservation.Id);
        currentHistory.Add(newPayment);

        // Actualizar el JSON en la base de datos
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        reservation.PaymentHistory = System.Text.Json.JsonSerializer.Serialize(
            currentHistory,
            options
        );
    }

    private List<PaymentHistoryDto> DeserializePaymentHistory(string? paymentHistoryJson)
    {
        if (string.IsNullOrEmpty(paymentHistoryJson))
            return new List<PaymentHistoryDto>();

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            };
            return System.Text.Json.JsonSerializer.Deserialize<List<PaymentHistoryDto>>(
                    paymentHistoryJson,
                    options
                ) ?? new List<PaymentHistoryDto>();
        }
        catch
        {
            return new List<PaymentHistoryDto>();
        }
    }
}
