using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TenisAhora.Application.DTOs;
using TenisAhora.Application.Interfaces;
using TenisAhora.Domain.Entities;
using TenisAhora.Domain.Exceptions;
using TenisAhora.Infrastructure.Persistence;

namespace TenisAhora.Application.Services;

/// <summary>
/// Servicio de aplicación que implementa la lógica de negocio de reservas de canchas,
/// control atómico de stock (<see cref="Producto"/> + <see cref="Stock"/> + <see cref="AsignacionEquipamiento"/>),
/// participantes (<see cref="Participante"/>), señas obligatorias del 50% vía Mercado Pago (<see cref="Pago"/> + <see cref="Recibo"/>)
/// y políticas de cancelación y reembolsos (<see cref="PoliticaReserva"/> + <see cref="Reembolso"/>) bajo IEEE SRS-830 (RD01 a RD06).
/// </summary>
public class ReservationService : IReservationService
{
    private static readonly string[] SpanishWeekDays =
    [
        "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly TenisAhoraDbContext _dbContext;
    private readonly IMercadoPagoGateway _mercadoPagoGateway;
    private readonly ICurrentUserService _currentUser;
    private readonly ClubConfigOptions _clubConfig;

    public ReservationService(
        TenisAhoraDbContext dbContext,
        IMercadoPagoGateway mercadoPagoGateway,
        ICurrentUserService currentUser,
        IOptions<ClubConfigOptions> clubConfigOptions)
    {
        _dbContext = dbContext;
        _mercadoPagoGateway = mercadoPagoGateway;
        _currentUser = currentUser;
        _clubConfig = clubConfigOptions.Value;
    }

    /// <inheritdoc />
    public async Task<CreateReservationResultDto> CreateReservationAsync(
        CreateReservationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsStaff && !_currentUser.IsMember)
        {
            throw DomainException.Forbidden("Esta acción está permitida únicamente para socios o personal del club.");
        }

        var leadParticipant = request.Participants.FirstOrDefault();
        var leadName = !string.IsNullOrWhiteSpace(leadParticipant?.Name)
            ? leadParticipant!.Name.Trim()
            : "Socio Tenis Ahora";
        var leadDni = !string.IsNullOrWhiteSpace(leadParticipant?.Dni)
            ? leadParticipant!.Dni.Trim()
            : _currentUser.Dni;

        if (!_currentUser.IsStaff && !_currentUser.IsMeByDni(leadDni))
        {
            throw DomainException.Forbidden("Solo podés reservar a tu nombre: el titular tiene que ser tu DNI.");
        }

        if (!DateOnly.TryParseExact(request.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var reservationDate))
        {
            throw DomainException.BadRequest("La fecha de la reserva no es válida (formato AAAA-MM-DD).");
        }

        if (!TimeOnly.TryParseExact(request.StartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
        {
            throw DomainException.BadRequest("La hora de inicio no es válida (formato HH:mm).");
        }

        var durationHours = request.DurationHours <= 0 ? 1.0m : Math.Round(request.DurationHours, 2);
        if (durationHours <= 0m || durationHours > 2.00m)
        {
            throw DomainException.BadRequest($"La duración máxima permitida por reserva es de 2 horas (solicitado: {durationHours:0.##} h).");
        }

        // RD05: Antelación máxima de 30 días calendario
        ValidateBookingWindow(reservationDate);

        var durationMinutes = (int)Math.Round(durationHours * 60m);
        var startMinutes = startTime.Hour * 60 + startTime.Minute;
        var endMinutes = startMinutes + durationMinutes;
        if (endMinutes > 24 * 60)
        {
            throw DomainException.BadRequest("El turno solicitado excede el horario del día.");
        }
        var endTime = new TimeOnly(endMinutes / 60, endMinutes % 60);

        // =========================================================================
        // TRANSACCIÓN ATÓMICA OBLIGATORIA (Disponibilidad -> Stock -> Mercado Pago)
        // =========================================================================
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            // ---------------------------------------------------------------------
            // PASO 1: Verificar disponibilidad de cancha y no solapamiento (RD06)
            // ---------------------------------------------------------------------
            var cancha = await _dbContext.Canchas
                .Include(c => c.HorariosCancha)
                .Include(c => c.BloqueosMantenimiento)
                .FirstOrDefaultAsync(c => c.Id == request.CourtId, cancellationToken);

            if (cancha is null)
            {
                throw DomainException.NotFound($"La cancha '{request.CourtId}' no existe.");
            }

            if (string.Equals(cancha.Estado, "mantenimiento", StringComparison.OrdinalIgnoreCase))
            {
                throw DomainException.Conflict("La cancha está bloqueada por mantenimiento y no admite reservas (RD06).");
            }

            var blockReason = ValidateCourtScheduleAndMaintenance(cancha, reservationDate, startTime, durationHours);
            if (!string.IsNullOrEmpty(blockReason))
            {
                throw DomainException.Conflict($"La cancha no admite reservas en ese turno: {blockReason} (RD06).");
            }

            var overlappingReservationId = await _dbContext.Reservas
                .Where(r => r.CanchaId == cancha.Id
                            && r.Fecha == reservationDate
                            && (r.Estado == "pendiente_pago" || r.Estado == "confirmada")
                            && startTime < r.HoraFin
                            && endTime > r.HoraInicio)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrEmpty(overlappingReservationId))
            {
                throw DomainException.Conflict(
                    $"La cancha ya posee una reserva activa ({overlappingReservationId}) en el horario {startTime:HH\\:mm} - {endTime:HH\\:mm} (RD06).");
            }

            // ---------------------------------------------------------------------
            // PASO 2: Descontar Stock de materiales deportivos (Bloqueo si Stock == 0)
            // ---------------------------------------------------------------------
            var playersType = string.Equals(request.PlayersType, "dobles", StringComparison.OrdinalIgnoreCase)
                ? "dobles"
                : "singles";

            var equipment = request.EquipmentAssigned ?? new EquipmentAssignedDto
            {
                Nets = 1,
                BallTubes = 1,
                Rackets = playersType == "dobles" ? 4 : 2
            };

            var reqNets = Math.Max(equipment.Nets, 1);
            var reqBalls = Math.Max(equipment.BallTubes, 1);
            var reqRackets = Math.Max(equipment.Rackets, 1);

            var requiredCategories = new[] { "Redes", "Pelotas", "Raquetas" };
            var stockEntries = await _dbContext.Stocks
                .Include(s => s.Producto)
                .Where(s => s.Producto != null && requiredCategories.Contains(s.Producto.Categoria))
                .OrderBy(s => s.Producto!.Categoria)
                .ThenBy(s => s.Id)
                .ToListAsync(cancellationToken);

            var netStock = stockEntries.FirstOrDefault(s => s.Producto?.Categoria == "Redes");
            var ballStock = stockEntries.FirstOrDefault(s => s.Producto?.Categoria == "Pelotas");
            var racketStock = stockEntries.FirstOrDefault(s => s.Producto?.Categoria == "Raquetas");

            if (netStock is null || netStock.StockDisponible <= 0 || netStock.StockDisponible < reqNets)
            {
                throw DomainException.Conflict(
                    $"Reserva bloqueada por falta de stock: Redes disponibles ({netStock?.StockDisponible ?? 0}), requeridas ({reqNets}).");
            }

            if (ballStock is null || ballStock.StockDisponible <= 0 || ballStock.StockDisponible < reqBalls)
            {
                throw DomainException.Conflict(
                    $"Reserva bloqueada por falta de stock: Pelotas disponibles ({ballStock?.StockDisponible ?? 0}), requeridas ({reqBalls}).");
            }

            if (racketStock is null || racketStock.StockDisponible <= 0 || racketStock.StockDisponible < reqRackets)
            {
                throw DomainException.Conflict(
                    $"Reserva bloqueada por falta de stock: Raquetas disponibles ({racketStock?.StockDisponible ?? 0}), requeridas ({reqRackets}).");
            }

            if (_clubConfig.UseApplicationLayerStockManagement)
            {
                ApplyStockDeduction(netStock, reqNets);
                ApplyStockDeduction(ballStock, reqBalls);
                ApplyStockDeduction(racketStock, reqRackets);
            }

            // ---------------------------------------------------------------------
            // PASO 3: Generar Preferencia de Pago en Mercado Pago (Seña 50% — RD01)
            // ---------------------------------------------------------------------
            var courtCost = Math.Round(cancha.PrecioPorHora * durationHours, 2, MidpointRounding.AwayFromZero);
            var equipmentCost = Math.Max(request.EquipmentCost, 0m);
            var totalCost = courtCost + equipmentCost;
            var requiredDeposit50 = Math.Round(totalCost * 0.50m, 2, MidpointRounding.AwayFromZero);
            var depositPaid = request.DepositPaid.HasValue && request.DepositPaid.Value >= requiredDeposit50
                ? Math.Round(request.DepositPaid.Value, 2, MidpointRounding.AwayFromZero)
                : requiredDeposit50;
            var remainingBalance = Math.Max(totalCost - depositPaid, 0m);

            var reservationId = await GenerateNextReservationIdAsync(cancellationToken);
            var receiptId = $"REC-50-{Random.Shared.Next(1000, 9999)}";
            var pagoId = $"PAG-{receiptId}";
            var courtDisplayName = !string.IsNullOrWhiteSpace(request.CourtName)
                ? request.CourtName.Trim()
                : $"{cancha.Nombre} ({cancha.Superficie})";

            var (usuario, cliente) = await ResolveReservationClientAsync(leadDni, leadName, cancellationToken);
            var politicaId = await ResolveActivePolicyIdAsync(cancellationToken);

            var mpPreference = await _mercadoPagoGateway.CreateDepositPreferenceAsync(
                new PaymentPreferenceRequestDto
                {
                    ExternalReference = reservationId,
                    Title = $"Seña 50% Reserva {reservationId} - {courtDisplayName}",
                    Description = $"Alquiler {durationHours:0.##}hs en {courtDisplayName} el {reservationDate:yyyy-MM-dd} ({startTime:HH\\:mm}-{endTime:HH\\:mm})",
                    Amount = depositPaid,
                    PayerName = leadName,
                    PayerDni = leadDni,
                    PayerEmail = usuario.Email
                },
                cancellationToken);

            var participantsList = request.Participants.Count > 0
                ? request.Participants
                :
                [
                    new ReservationParticipantDto
                    {
                        Name = leadName,
                        Dni = leadDni,
                        Phone = usuario.Telefono,
                        IsLead = true
                    }
                ];

            var nowUtc = DateTimeOffset.UtcNow;
            var paymentMethod = NormalizePaymentMethod(request.DepositPaymentMethod);

            var reserva = new Reserva
            {
                Id = reservationId,
                CanchaId = cancha.Id,
                ClienteId = cliente.Id,
                PoliticaReservaId = politicaId,
                TitularDni = leadDni,
                NombreCancha = courtDisplayName,
                Superficie = !string.IsNullOrWhiteSpace(request.SurfaceType) ? request.SurfaceType.Trim() : cancha.Superficie,
                Fecha = reservationDate,
                HoraInicio = startTime,
                HoraFin = endTime,
                DuracionHoras = durationHours,
                ModalidadJuego = playersType,
                CostoCancha = courtCost,
                CostoEquipamiento = equipmentCost,
                CostoTotal = totalCost,
                SenaPagada = depositPaid,
                ReciboSenaId = receiptId,
                MetodoPagoSena = paymentMethod,
                SaldoPendiente = remainingBalance,
                SaldoAbonado = false,
                ReciboFinalId = null,
                Estado = "confirmada",
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            // Entidades relacionales Participante (Módulo 3)
            for (var i = 0; i < participantsList.Count; i++)
            {
                var pDto = participantsList[i];
                var pEntity = new Participante
                {
                    Id = $"PART-{reservationId}-{i + 1}",
                    ReservaId = reservationId,
                    ClienteId = i == 0 ? cliente.Id : null,
                    Nombre = !string.IsNullOrWhiteSpace(pDto.Name) ? pDto.Name.Trim() : leadName,
                    Dni = !string.IsNullOrWhiteSpace(pDto.Dni) ? pDto.Dni.Trim() : leadDni,
                    Telefono = pDto.Phone,
                    EsTitular = i == 0 || pDto.IsLead,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                reserva.Participantes.Add(pEntity);
                _dbContext.Participantes.Add(pEntity);
            }

            // Entidades relacionales AsignacionEquipamiento (Módulo 3)
            var netUnitCost = netStock.Producto?.PrecioUnitarioAlquilerOVenta ?? 0m;
            var ballUnitCost = ballStock.Producto?.PrecioUnitarioAlquilerOVenta ?? 0m;
            var racketUnitCost = racketStock.Producto?.PrecioUnitarioAlquilerOVenta ?? 0m;

            var asignaciones = new List<AsignacionEquipamiento>
            {
                new()
                {
                    Id = $"ASIG-{reservationId}-RED",
                    ReservaId = reservationId,
                    ProductoId = netStock.ProductoId,
                    StockId = netStock.Id,
                    Producto = netStock.Producto,
                    CantidadAsignada = reqNets,
                    CostoUnitario = netUnitCost,
                    Subtotal = netUnitCost * reqNets,
                    EstadoDevolucion = "en_uso",
                    FechaAsignacion = nowUtc,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                },
                new()
                {
                    Id = $"ASIG-{reservationId}-PEL",
                    ReservaId = reservationId,
                    ProductoId = ballStock.ProductoId,
                    StockId = ballStock.Id,
                    Producto = ballStock.Producto,
                    CantidadAsignada = reqBalls,
                    CostoUnitario = ballUnitCost,
                    Subtotal = ballUnitCost * reqBalls,
                    EstadoDevolucion = "en_uso",
                    FechaAsignacion = nowUtc,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                },
                new()
                {
                    Id = $"ASIG-{reservationId}-RAQ",
                    ReservaId = reservationId,
                    ProductoId = racketStock.ProductoId,
                    StockId = racketStock.Id,
                    Producto = racketStock.Producto,
                    CantidadAsignada = reqRackets,
                    CostoUnitario = racketUnitCost,
                    Subtotal = racketUnitCost * reqRackets,
                    EstadoDevolucion = "en_uso",
                    FechaAsignacion = nowUtc,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                }
            };

            foreach (var asig in asignaciones)
            {
                reserva.AsignacionesEquipamiento.Add(asig);
                _dbContext.AsignacionesEquipamiento.Add(asig);
            }

            var receiptItems = new List<ReceiptItemDto>
            {
                new() { Description = $"Alquiler {durationHours:0.##}hs {courtDisplayName}", Amount = courtCost },
                new() { Description = "Equipamiento incluido (Red, Pelotas, Raquetas)", Amount = equipmentCost },
                new() { Description = "Total Turno", Amount = totalCost },
                new() { Description = "Cobro de Seña 50% para Confirmación", Amount = depositPaid }
            };

            var conceptoSena = $"Seña 50% Obligatoria - {courtDisplayName}";

            // Entidad Pago (Módulo 6)
            var pagoSena = new Pago
            {
                Id = pagoId,
                ClienteId = cliente.Id,
                ReservaId = reservationId,
                Monto = depositPaid,
                TipoPago = "sena_50",
                MedioPago = paymentMethod,
                Estado = "completado",
                MpPreferenceId = mpPreference.PreferenceId,
                MpStatus = mpPreference.Status,
                QrData = mpPreference.QrData,
                PaymentUrl = !string.IsNullOrWhiteSpace(mpPreference.SandboxInitPoint)
                    ? mpPreference.SandboxInitPoint
                    : mpPreference.InitPoint,
                FechaPago = nowUtc,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            // Entidad Recibo 1:1 con Pago (Módulo 6)
            var reciboSena = new Recibo
            {
                Id = receiptId,
                NumeroRecibo = receiptId,
                PagoId = pagoSena.Id,
                ClienteId = cliente.Id,
                ClienteNombre = leadName,
                ClienteDni = leadDni,
                ReservaId = reservationId,
                Concepto = conceptoSena,
                ItemsJson = JsonSerializer.Serialize(receiptItems, JsonOptions),
                TotalAbonado = depositPaid,
                MedioPago = paymentMethod,
                QrData = mpPreference.QrData,
                CajeroNombre = "Cobro Digital Online",
                FechaEmision = nowUtc,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            cliente.TotalReservas += 1;
            cliente.UpdatedAt = nowUtc;

            _dbContext.Reservas.Add(reserva);
            _dbContext.Pagos.Add(pagoSena);
            _dbContext.Recibos.Add(reciboSena);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CreateReservationResultDto
            {
                Data = MapToResponseDto(reserva),
                Receipt = MapReciboToReceiptDto(reciboSena, pagoSena, receiptItems),
                PaymentPreference = mpPreference
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReservationResponseDto>> GetReservationsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsStaff && !_currentUser.IsMember)
        {
            throw DomainException.Forbidden("Esta sección es solo para socios y personal del club.");
        }

        IQueryable<Reserva> query = _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Participantes)
            .Include(r => r.AsignacionesEquipamiento)
                .ThenInclude(a => a.Producto);

        if (!_currentUser.IsStaff)
        {
            var dni = _currentUser.Dni;
            var userId = _currentUser.UserId;
            query = query.Where(r => r.TitularDni == dni || r.ClienteId == userId);
        }

        var list = await query
            .OrderByDescending(r => r.Fecha)
            .ThenByDescending(r => r.HoraInicio)
            .ToListAsync(cancellationToken);

        return list.Select(MapToResponseDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReservationOccupancyDto>> GetOccupancyAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsStaff && !_currentUser.IsMember)
        {
            throw DomainException.Forbidden("Esta sección es solo para socios y personal del club.");
        }

        var activeReservations = await _dbContext.Reservas
            .AsNoTracking()
            .Where(r => r.Estado != "cancelada")
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.HoraInicio)
            .ToListAsync(cancellationToken);

        return activeReservations
            .Select(r => new ReservationOccupancyDto
            {
                Id = r.Id,
                CourtId = r.CanchaId,
                CourtName = r.NombreCancha,
                Date = r.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                StartTime = r.HoraInicio.ToString("HH:mm", CultureInfo.InvariantCulture),
                EndTime = r.HoraFin.ToString("HH:mm", CultureInfo.InvariantCulture),
                DurationHours = r.DuracionHoras,
                Status = r.Estado
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ReservationResponseDto> UpdateReservationAsync(
        string id,
        UpdateReservationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsStaff)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }

        var existing = await _dbContext.Reservas
            .Include(r => r.Participantes)
            .Include(r => r.AsignacionesEquipamiento)
                .ThenInclude(a => a.Producto)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw DomainException.NotFound($"La reserva '{id}' no existe.");

        var courtChanged = !string.IsNullOrWhiteSpace(request.CourtId) && request.CourtId != existing.CanchaId;
        var dateChanged = !string.IsNullOrWhiteSpace(request.Date)
                          && request.Date != existing.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var startChanged = !string.IsNullOrWhiteSpace(request.StartTime)
                           && request.StartTime != existing.HoraInicio.ToString("HH:mm", CultureInfo.InvariantCulture);
        var durationChanged = request.DurationHours.HasValue && request.DurationHours.Value != existing.DuracionHoras;

        var nextDate = existing.Fecha;
        if (dateChanged)
        {
            if (!DateOnly.TryParseExact(request.Date!, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out nextDate))
            {
                throw DomainException.BadRequest("La fecha de la reserva no es válida (formato AAAA-MM-DD).");
            }
            ValidateBookingWindow(nextDate);
        }

        var nextStart = existing.HoraInicio;
        if (startChanged && !TimeOnly.TryParseExact(request.StartTime!, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out nextStart))
        {
            throw DomainException.BadRequest("La hora de inicio no es válida (formato HH:mm).");
        }

        var nextDuration = request.DurationHours.HasValue && request.DurationHours.Value > 0
            ? Math.Round(request.DurationHours.Value, 2)
            : existing.DuracionHoras;

        if (nextDuration <= 0m || nextDuration > 2.00m)
        {
            throw DomainException.BadRequest("La duración máxima permitida por reserva es de 2 horas.");
        }

        var nextEnd = existing.HoraFin;
        if (startChanged || durationChanged)
        {
            var totalMin = nextStart.Hour * 60 + nextStart.Minute + (int)Math.Round(nextDuration * 60m);
            nextEnd = new TimeOnly(totalMin / 60, totalMin % 60);
        }

        var targetCourtId = courtChanged ? request.CourtId! : existing.CanchaId;
        var cancha = await _dbContext.Canchas
            .Include(c => c.HorariosCancha)
            .Include(c => c.BloqueosMantenimiento)
            .FirstOrDefaultAsync(c => c.Id == targetCourtId, cancellationToken);

        if (cancha is not null)
        {
            if (courtChanged && string.Equals(cancha.Estado, "mantenimiento", StringComparison.OrdinalIgnoreCase))
            {
                throw DomainException.Conflict("La cancha seleccionada está bloqueada por mantenimiento (RD06).");
            }

            if (courtChanged || dateChanged || startChanged || durationChanged)
            {
                var blockReason = ValidateCourtScheduleAndMaintenance(cancha, nextDate, nextStart, nextDuration);
                if (!string.IsNullOrEmpty(blockReason))
                {
                    throw DomainException.Conflict($"La cancha no admite reservas en ese turno: {blockReason} (RD06).");
                }

                var hasOverlap = await _dbContext.Reservas.AnyAsync(
                    r => r.CanchaId == targetCourtId
                         && r.Fecha == nextDate
                         && r.Id != existing.Id
                         && (r.Estado == "pendiente_pago" || r.Estado == "confirmada")
                         && nextStart < r.HoraFin
                         && nextEnd > r.HoraInicio,
                    cancellationToken);

                if (hasOverlap)
                {
                    throw DomainException.Conflict("Existe solapamiento horario con otra reserva activa en esa cancha (RD06).");
                }
            }

            if (courtChanged || durationChanged)
            {
                if (courtChanged)
                {
                    existing.CanchaId = cancha.Id;
                    existing.NombreCancha = $"{cancha.Nombre} ({cancha.Superficie})";
                    existing.Superficie = cancha.Superficie;
                }

                var equipmentCost = request.EquipmentCost ?? existing.CostoEquipamiento;
                var courtCost = Math.Round(cancha.PrecioPorHora * nextDuration, 2, MidpointRounding.AwayFromZero);
                var totalCost = courtCost + equipmentCost;

                existing.CostoEquipamiento = equipmentCost;
                existing.CostoCancha = courtCost;
                existing.CostoTotal = totalCost;
                existing.SaldoPendiente = Math.Max(totalCost - existing.SenaPagada, 0m);
            }
        }

        existing.Fecha = nextDate;
        existing.HoraInicio = nextStart;
        existing.HoraFin = nextEnd;
        existing.DuracionHoras = nextDuration;

        if (!string.IsNullOrWhiteSpace(request.PlayersType))
        {
            existing.ModalidadJuego = request.PlayersType.Trim().ToLowerInvariant();
        }

        if (request.Participants is not null)
        {
            _dbContext.Participantes.RemoveRange(existing.Participantes);
            existing.Participantes.Clear();

            for (var i = 0; i < request.Participants.Count; i++)
            {
                var pDto = request.Participants[i];
                var pEntity = new Participante
                {
                    Id = $"PART-{existing.Id}-{i + 1}-{Random.Shared.Next(100, 999)}",
                    ReservaId = existing.Id,
                    ClienteId = i == 0 ? existing.ClienteId : null,
                    Nombre = pDto.Name.Trim(),
                    Dni = pDto.Dni.Trim(),
                    Telefono = pDto.Phone,
                    EsTitular = i == 0 || pDto.IsLead,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                existing.Participantes.Add(pEntity);
                _dbContext.Participantes.Add(pEntity);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            existing.Estado = request.Status.Trim();
        }

        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponseDto(existing);
    }

    /// <inheritdoc />
    public async Task<ReservationResponseDto> PayRemainingBalanceAsync(
        string id,
        PayRemainingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsStaff)
        {
            throw DomainException.Forbidden("Esta acción es solo para el personal del club.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var reserva = await _dbContext.Reservas
                .Include(r => r.Participantes)
                .Include(r => r.AsignacionesEquipamiento)
                    .ThenInclude(a => a.Producto)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                ?? throw DomainException.NotFound($"La reserva '{id}' no existe.");

            if (reserva.SaldoAbonado)
            {
                throw DomainException.Conflict("El saldo de esta reserva ya fue liquidado.");
            }

            if (string.Equals(reserva.Estado, "cancelada", StringComparison.OrdinalIgnoreCase))
            {
                throw DomainException.Conflict("La reserva está cancelada.");
            }

            var remainingAmount = reserva.SaldoPendiente > 0
                ? reserva.SaldoPendiente
                : Math.Round(reserva.CostoTotal - reserva.SenaPagada, 2, MidpointRounding.AwayFromZero);

            var (leadName, leadDni) = ExtractLeadClient(reserva);
            var finalReceiptId = $"REC-FINAL-{Random.Shared.Next(1000, 9999)}";
            var pagoFinalId = $"PAG-{finalReceiptId}";
            var previousStatus = reserva.Estado;

            var mpPreference = await _mercadoPagoGateway.CreateRemainingBalancePreferenceAsync(
                new PaymentPreferenceRequestDto
                {
                    ExternalReference = reserva.Id,
                    Title = $"Liquidación Final 50% Saldo - {reserva.NombreCancha}",
                    Description = $"Saldo restante 50% de turno finalizado ({reserva.Id})",
                    Amount = remainingAmount,
                    PayerName = leadName,
                    PayerDni = leadDni
                },
                cancellationToken);

            // Restituir stock automáticamente al finalizar el turno
            if (_clubConfig.UseApplicationLayerStockManagement
                && (previousStatus == "pendiente_pago" || previousStatus == "confirmada"))
            {
                await RestoreStockForReservationAsync(reserva, cancellationToken);
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var paymentMethod = NormalizePaymentMethod(request.PaymentMethod);

            reserva.SaldoAbonado = true;
            reserva.SaldoPendiente = 0m;
            reserva.Estado = "finalizada";
            reserva.ReciboFinalId = finalReceiptId;
            reserva.UpdatedAt = nowUtc;

            var receiptItems = new List<ReceiptItemDto>
            {
                new() { Description = "Saldo restante 50% de turno finalizado", Amount = remainingAmount }
            };

            var conceptoFinal = $"Liquidación Final 50% Saldo - {reserva.NombreCancha}";

            var pagoFinal = new Pago
            {
                Id = pagoFinalId,
                ClienteId = reserva.ClienteId,
                ReservaId = reserva.Id,
                Monto = Math.Max(remainingAmount, 0.01m),
                TipoPago = "saldo_50",
                MedioPago = paymentMethod,
                Estado = "completado",
                MpPreferenceId = mpPreference.PreferenceId,
                MpStatus = mpPreference.Status,
                QrData = mpPreference.QrData,
                PaymentUrl = !string.IsNullOrWhiteSpace(mpPreference.SandboxInitPoint)
                    ? mpPreference.SandboxInitPoint
                    : mpPreference.InitPoint,
                FechaPago = nowUtc,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            var reciboFinal = new Recibo
            {
                Id = finalReceiptId,
                NumeroRecibo = finalReceiptId,
                PagoId = pagoFinal.Id,
                ClienteId = reserva.ClienteId,
                ClienteNombre = leadName,
                ClienteDni = leadDni,
                ReservaId = reserva.Id,
                Concepto = conceptoFinal,
                ItemsJson = JsonSerializer.Serialize(receiptItems, JsonOptions),
                TotalAbonado = pagoFinal.Monto,
                MedioPago = paymentMethod,
                QrData = mpPreference.QrData,
                CajeroNombre = "Recepción del Club",
                FechaEmision = nowUtc,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            _dbContext.Pagos.Add(pagoFinal);
            _dbContext.Recibos.Add(reciboFinal);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return MapToResponseDto(reserva);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CancelReservationResponseDto> CancelReservationAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var reserva = await _dbContext.Reservas
                .Include(r => r.Participantes)
                .Include(r => r.AsignacionesEquipamiento)
                    .ThenInclude(a => a.Producto)
                .Include(r => r.PoliticaReserva)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                ?? throw DomainException.NotFound($"La reserva '{id}' no existe.");

            if (!_currentUser.IsStaff && !OwnsReservation(reserva))
            {
                throw DomainException.Forbidden("La reserva no es tuya.");
            }

            if (string.Equals(reserva.Estado, "cancelada", StringComparison.OrdinalIgnoreCase))
            {
                throw DomainException.Conflict("La reserva ya está cancelada.");
            }

            var previousStatus = reserva.Estado;

            // RD02-RD04: Cálculo de horas hasta el inicio de la reserva
            var startDateTime = reserva.Fecha.ToDateTime(reserva.HoraInicio, DateTimeKind.Local);
            var hoursUntilStart = Math.Round((startDateTime - DateTime.Now).TotalHours * 10.0) / 10.0;

            var freeHours = reserva.PoliticaReserva?.HorasCancelacionGratuita > 0
                ? (double)reserva.PoliticaReserva.HorasCancelacionGratuita
                : _clubConfig.FreeCancellationHours;

            var retentionPct = reserva.PoliticaReserva?.PorcentajeRetencionCancelacionTardia > 0
                ? reserva.PoliticaReserva.PorcentajeRetencionCancelacionTardia
                : _clubConfig.LateCancellationRetentionPercent;

            var isWithinFreeWindow = hoursUntilStart >= freeHours;

            var policyText = isWithinFreeWindow
                ? $"Cancelación anticipada (>={freeHours:0.##}hs): Reembolso de seña según política."
                : $"Cancelación tardía (<{freeHours:0.##}hs): Se aplica retención del {retentionPct:0.##}% de seña por costo operativo.";

            var nowUtc = DateTimeOffset.UtcNow;

            // Restitución de stock asignado al cancelar
            if (_clubConfig.UseApplicationLayerStockManagement
                && (previousStatus == "pendiente_pago" || previousStatus == "confirmada"))
            {
                await RestoreStockForReservationAsync(reserva, cancellationToken);
            }

            reserva.Estado = "cancelada";
            reserva.FechaCancelacion = nowUtc;
            reserva.HorasAntelacionCancelacion = (decimal)hoursUntilStart;
            reserva.MotivoOPoliticaCancelacion = policyText;
            reserva.UpdatedAt = nowUtc;

            var (leadName, leadDni) = ExtractLeadClient(reserva);
            var deposit = reserva.SenaPagada;
            var refundCount = await _dbContext.Reembolsos.CountAsync(cancellationToken);
            var refundId = $"RMB-{nowUtc.Year}-{refundCount + 9:D4}";
            var stamp = nowUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

            ReceiptResponseDto? penaltyReceiptDto = null;
            RefundResponseDto refundDto;

            if (isWithinFreeWindow)
            {
                refundDto = new RefundResponseDto
                {
                    Id = refundId,
                    ClientName = leadName,
                    ClientDni = leadDni,
                    RelatedReservationId = reserva.Id,
                    RelatedReceiptId = reserva.ReciboSenaId,
                    Reason = $"Cancelación de reserva {reserva.Id} con {hoursUntilStart:0.#} hs de antelación (dentro del plazo reglamentario).",
                    RefundType = "total",
                    OriginalAmount = deposit,
                    RefundAmount = deposit,
                    RefundMethod = "Devolución a " + (reserva.MetodoPagoSena ?? "medio de pago original"),
                    Date = stamp,
                    ProcessedBy = "Sistema de Reservas"
                };

                _dbContext.Reembolsos.Add(new Reembolso
                {
                    Id = refundId,
                    NumeroReembolso = refundId,
                    ClienteId = reserva.ClienteId,
                    ClienteNombre = leadName,
                    ClienteDni = leadDni,
                    ReservaRelacionadaId = reserva.Id,
                    ReciboRelacionadoId = reserva.ReciboSenaId,
                    Motivo = refundDto.Reason,
                    TipoReembolso = "total",
                    MontoOriginal = deposit,
                    MontoReembolsado = deposit,
                    MedioDevolucion = refundDto.RefundMethod,
                    Estado = "procesado",
                    ProcesadoPor = "Sistema de Reservas",
                    FechaReembolso = nowUtc,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                });
            }
            else
            {
                var retained = Math.Round(deposit * (retentionPct / 100m), 2, MidpointRounding.AwayFromZero);
                var refundedCredit = Math.Max(deposit - retained, 0m);
                var penaltyReceiptId = $"REC-CARGO-{Random.Shared.Next(1000, 9999)}";
                var penaltyPagoId = $"PAG-{penaltyReceiptId}";

                var penaltyItems = new List<ReceiptItemDto>
                {
                    new() { Description = $"Seña abonada ({reserva.Id})", Amount = deposit },
                    new() { Description = $"Retención {retentionPct:0.##}% por cancelación con menos de {freeHours:0.##} hs", Amount = retained },
                    new() { Description = "Saldo reintegrado como crédito en cuenta", Amount = refundedCredit }
                };

                var conceptoCargo = $"Cargo por cancelación fuera de plazo - {reserva.NombreCancha}";

                var pagoCargo = new Pago
                {
                    Id = penaltyPagoId,
                    ClienteId = reserva.ClienteId,
                    ReservaId = reserva.Id,
                    Monto = Math.Max(retained, 0.01m),
                    TipoPago = "cargo_cancelacion",
                    MedioPago = "Retención sobre seña",
                    Estado = "completado",
                    FechaPago = nowUtc,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                var reciboCargo = new Recibo
                {
                    Id = penaltyReceiptId,
                    NumeroRecibo = penaltyReceiptId,
                    PagoId = pagoCargo.Id,
                    ClienteId = reserva.ClienteId,
                    ClienteNombre = leadName,
                    ClienteDni = leadDni,
                    ReservaId = reserva.Id,
                    Concepto = conceptoCargo,
                    ItemsJson = JsonSerializer.Serialize(penaltyItems, JsonOptions),
                    TotalAbonado = pagoCargo.Monto,
                    MedioPago = "Retención sobre seña",
                    CajeroNombre = "Sistema de Reservas",
                    FechaEmision = nowUtc,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                _dbContext.Pagos.Add(pagoCargo);
                _dbContext.Recibos.Add(reciboCargo);
                penaltyReceiptDto = MapReciboToReceiptDto(reciboCargo, pagoCargo, penaltyItems);

                refundDto = new RefundResponseDto
                {
                    Id = refundId,
                    ClientName = leadName,
                    ClientDni = leadDni,
                    RelatedReservationId = reserva.Id,
                    RelatedReceiptId = reserva.ReciboSenaId,
                    Reason = $"Cancelación fuera de plazo ({hoursUntilStart:0.#} hs). Se retiene el {retentionPct:0.##}% de la seña y se reintegra el resto.",
                    RefundType = "parcial",
                    OriginalAmount = deposit,
                    RefundAmount = refundedCredit,
                    RefundMethod = "Crédito en cuenta de socio",
                    Date = stamp,
                    ProcessedBy = "Sistema de Reservas"
                };

                _dbContext.Reembolsos.Add(new Reembolso
                {
                    Id = refundId,
                    NumeroReembolso = refundId,
                    ClienteId = reserva.ClienteId,
                    ClienteNombre = leadName,
                    ClienteDni = leadDni,
                    ReservaRelacionadaId = reserva.Id,
                    ReciboRelacionadoId = reserva.ReciboSenaId,
                    PagoRelacionadoId = pagoCargo.Id,
                    Motivo = refundDto.Reason,
                    TipoReembolso = "parcial",
                    MontoOriginal = deposit,
                    MontoReembolsado = refundedCredit,
                    MedioDevolucion = refundDto.RefundMethod,
                    Estado = "procesado",
                    ProcesadoPor = "Sistema de Reservas",
                    FechaReembolso = nowUtc,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CancelReservationResponseDto
            {
                Data = MapToResponseDto(reserva),
                IsWithinFreeWindow = isWithinFreeWindow,
                Refund = refundDto,
                PenaltyReceipt = penaltyReceiptDto
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // =========================================================================
    // MÉTODOS PRIVADOS DE REGLAS DE DOMINIO (RD01 - RD06)
    // =========================================================================

    private void ValidateBookingWindow(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (date < today)
        {
            throw DomainException.BadRequest($"No se permiten reservas en fechas pasadas ({date:yyyy-MM-dd}) (RD05).");
        }

        var maxDays = _clubConfig.MaxAdvanceBookingDays > 0 ? _clubConfig.MaxAdvanceBookingDays : 30;
        var limit = today.AddDays(maxDays);
        if (date > limit)
        {
            throw DomainException.BadRequest(
                $"Las reservas se pueden hacer con hasta {maxDays} días de antelación (hasta el {limit:yyyy-MM-dd}) (RD05).");
        }
    }

    private static string ValidateCourtScheduleAndMaintenance(
        Cancha cancha,
        DateOnly date,
        TimeOnly startTime,
        decimal durationHours)
    {
        var dayName = SpanishWeekDays[(int)date.DayOfWeek];
        var fromMinutes = startTime.Hour * 60 + startTime.Minute;
        var toMinutes = fromMinutes + (int)Math.Round(durationHours * 60m);
        var endTime = new TimeOnly(toMinutes / 60, toMinutes % 60);

        if (cancha.HorariosCancha.Count > 0)
        {
            var scheduleForDay = cancha.HorariosCancha.FirstOrDefault(h =>
                string.Equals(h.DiaSemana, dayName, StringComparison.OrdinalIgnoreCase));

            if (scheduleForDay is null || !scheduleForDay.Habilitado)
            {
                return $"sin atención el día {dayName}";
            }

            if (startTime < scheduleForDay.HoraApertura || endTime > scheduleForDay.HoraCierre)
            {
                return $"fuera de horario ({scheduleForDay.HoraApertura:HH\\:mm} a {scheduleForDay.HoraCierre:HH\\:mm})";
            }
        }
        else
        {
            // Horario operativo por defecto 08:00 a 22:00
            if (fromMinutes < 8 * 60 || toMinutes > 22 * 60)
            {
                return "fuera de horario (08:00 a 22:00)";
            }
        }

        if (cancha.BloqueosMantenimiento.Count > 0)
        {
            foreach (var b in cancha.BloqueosMantenimiento.Where(b => b.Activo))
            {
                var appliesByDateRange = date >= b.FechaInicio && date <= b.FechaFin;
                var appliesByDay = !string.IsNullOrWhiteSpace(b.DiaSemana)
                                   && string.Equals(b.DiaSemana, dayName, StringComparison.OrdinalIgnoreCase);

                if (!appliesByDateRange && !appliesByDay)
                {
                    continue;
                }

                if (startTime < b.HoraFin && endTime > b.HoraInicio)
                {
                    return !string.IsNullOrWhiteSpace(b.Motivo)
                        ? b.Motivo
                        : $"bloqueada de {b.HoraInicio:HH\\:mm} a {b.HoraFin:HH\\:mm}";
                }
            }
        }

        return string.Empty;
    }

    private static void ApplyStockDeduction(Stock stock, int quantity)
    {
        stock.StockDisponible -= quantity;
        stock.StockEnUso += quantity;
        stock.Estado = stock.StockDisponible <= 0 ? "agotado" : "disponible";
        stock.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task RestoreStockForReservationAsync(Reserva reserva, CancellationToken cancellationToken)
    {
        if (reserva.AsignacionesEquipamiento.Count > 0)
        {
            var productIds = reserva.AsignacionesEquipamiento.Select(a => a.ProductoId).Distinct().ToList();
            var stocks = await _dbContext.Stocks
                .Where(s => productIds.Contains(s.ProductoId))
                .ToListAsync(cancellationToken);

            var nowUtc = DateTimeOffset.UtcNow;
            foreach (var asig in reserva.AsignacionesEquipamiento.Where(a => a.EstadoDevolucion == "en_uso"))
            {
                var stock = stocks.FirstOrDefault(s => s.ProductoId == asig.ProductoId);
                if (stock is not null)
                {
                    stock.StockEnUso = Math.Max(0, stock.StockEnUso - asig.CantidadAsignada);
                    stock.StockDisponible = stock.StockTotal - stock.StockEnUso;
                    stock.Estado = stock.StockDisponible <= 0 ? "agotado" : "disponible";
                    stock.UpdatedAt = nowUtc;
                }

                asig.EstadoDevolucion = "devuelto";
                asig.FechaRestitucion = nowUtc;
                asig.UpdatedAt = nowUtc;
            }
            return;
        }

        var requiredCategories = new[] { "Redes", "Pelotas", "Raquetas" };
        var stockItems = await _dbContext.Stocks
            .Include(s => s.Producto)
            .Where(s => s.Producto != null && requiredCategories.Contains(s.Producto.Categoria))
            .ToListAsync(cancellationToken);

        foreach (var item in stockItems)
        {
            var qtyToRestore = item.Producto?.Categoria switch
            {
                "Redes" => 1,
                "Pelotas" => 1,
                "Raquetas" => reserva.ModalidadJuego == "dobles" ? 4 : 2,
                _ => 0
            };

            if (qtyToRestore <= 0)
            {
                continue;
            }

            item.StockEnUso = Math.Max(0, item.StockEnUso - qtyToRestore);
            item.StockDisponible = item.StockTotal - item.StockEnUso;
            item.Estado = item.StockDisponible <= 0 ? "agotado" : "disponible";
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task<string> GenerateNextReservationIdAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _dbContext.Reservas.CountAsync(cancellationToken);
        var baseId = $"RES-{year}-{count + 1:D3}";
        var candidate = baseId;
        var suffix = 1;

        while (await _dbContext.Reservas.AnyAsync(r => r.Id == candidate, cancellationToken))
        {
            suffix++;
            candidate = $"{baseId}-{suffix}";
        }

        return candidate;
    }

    private async Task<string> ResolveActivePolicyIdAsync(CancellationToken cancellationToken)
    {
        var activePolicy = await _dbContext.PoliticasReserva
            .AsNoTracking()
            .Where(p => p.Activa)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (activePolicy is not null)
        {
            return activePolicy.Id;
        }

        var defaultPolicy = new PoliticaReserva
        {
            Id = "pol-estandar",
            NombrePolitica = "Política General de Reservas SRS-830",
            PorcentajeSenaObligatoria = 50.00m,
            HorasCancelacionGratuita = 24.00m,
            PorcentajeRetencionCancelacionTardia = 25.00m,
            DiasMaxAntelacion = 30,
            DuracionMaximaHoras = 2.00m,
            Activa = true
        };

        _dbContext.PoliticasReserva.Add(defaultPolicy);
        return defaultPolicy.Id;
    }

    private async Task<(Usuario Usuario, Cliente Cliente)> ResolveReservationClientAsync(
        string leadDni,
        string leadName,
        CancellationToken cancellationToken)
    {
        Cliente? cliente = null;

        if (!string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            cliente = await _dbContext.Clientes
                .FirstOrDefaultAsync(c => c.Id == _currentUser.UserId, cancellationToken);
        }

        if (cliente is null && _currentUser.AuthUserId.HasValue)
        {
            cliente = await _dbContext.Clientes
                .FirstOrDefaultAsync(c => c.AuthUserId == _currentUser.AuthUserId.Value, cancellationToken);
        }

        if (cliente is null && !string.IsNullOrWhiteSpace(leadDni))
        {
            cliente = await _dbContext.Clientes
                .FirstOrDefaultAsync(c => c.Dni == leadDni, cancellationToken);
        }

        if (cliente is null)
        {
            cliente = await _dbContext.Clientes
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (cliente is not null)
        {
            return (cliente, cliente);
        }

        throw DomainException.BadRequest($"No se encontró un perfil de cliente válido para el DNI '{leadDni}' ({leadName}).");
    }

    private bool OwnsReservation(Reserva reserva)
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.UserId)
            && string.Equals(reserva.ClienteId, _currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (_currentUser.IsMeByDni(reserva.TitularDni))
        {
            return true;
        }

        var leadParticipant = reserva.Participantes.FirstOrDefault(p => p.EsTitular)
                              ?? reserva.Participantes.FirstOrDefault();
        return leadParticipant is not null && _currentUser.IsMeByDni(leadParticipant.Dni);
    }

    private static (string Name, string Dni) ExtractLeadClient(Reserva reserva)
    {
        var lead = reserva.Participantes.FirstOrDefault(p => p.EsTitular)
                   ?? reserva.Participantes.FirstOrDefault();
        var name = !string.IsNullOrWhiteSpace(lead?.Nombre) ? lead!.Nombre : "Socio Tenis Ahora";
        var dni = !string.IsNullOrWhiteSpace(lead?.Dni)
            ? lead!.Dni
            : (!string.IsNullOrWhiteSpace(reserva.TitularDni) ? reserva.TitularDni : "00.000.000");

        return (name, dni);
    }

    private static string NormalizePaymentMethod(string? method)
    {
        var validMethods = new[]
        {
            "Mercado Pago",
            "Mercado Pago (QR)",
            "Tarjeta de Débito",
            "Tarjeta de Crédito",
            "Billetera Virtual",
            "Retención sobre seña",
            "Crédito en cuenta de socio"
        };

        if (!string.IsNullOrWhiteSpace(method))
        {
            var matched = validMethods.FirstOrDefault(m => string.Equals(m, method.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                return matched;
            }
        }

        return "Mercado Pago (QR)";
    }

    private static ReservationResponseDto MapToResponseDto(Reserva r)
    {
        var participants = r.Participantes
            .OrderByDescending(p => p.EsTitular)
            .ThenBy(p => p.Id)
            .Select(p => new ReservationParticipantDto
            {
                Name = p.Nombre,
                Dni = p.Dni,
                Phone = p.Telefono,
                IsLead = p.EsTitular
            })
            .ToList();

        var nets = r.AsignacionesEquipamiento
            .Where(a => a.Producto?.Categoria == "Redes" || a.Id.EndsWith("-RED", StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.CantidadAsignada);
        var balls = r.AsignacionesEquipamiento
            .Where(a => a.Producto?.Categoria == "Pelotas" || a.Id.EndsWith("-PEL", StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.CantidadAsignada);
        var rackets = r.AsignacionesEquipamiento
            .Where(a => a.Producto?.Categoria == "Raquetas" || a.Id.EndsWith("-RAQ", StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.CantidadAsignada);

        var equipment = new EquipmentAssignedDto
        {
            Nets = nets > 0 ? nets : 1,
            BallTubes = balls > 0 ? balls : 1,
            Rackets = rackets > 0 ? rackets : (r.ModalidadJuego == "dobles" ? 4 : 2)
        };

        CancellationDetailsDto? cancellation = null;
        if (r.FechaCancelacion.HasValue || !string.IsNullOrWhiteSpace(r.MotivoOPoliticaCancelacion))
        {
            cancellation = new CancellationDetailsDto
            {
                CancelledAt = (r.FechaCancelacion ?? r.UpdatedAt).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                HoursInAdvance = (double)(r.HorasAntelacionCancelacion ?? 0m),
                PolicyApplied = r.MotivoOPoliticaCancelacion ?? string.Empty
            };
        }

        return new ReservationResponseDto
        {
            Id = r.Id,
            CourtId = r.CanchaId,
            CourtName = r.NombreCancha,
            SurfaceType = r.Superficie,
            Date = r.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            StartTime = r.HoraInicio.ToString("HH:mm", CultureInfo.InvariantCulture),
            EndTime = r.HoraFin.ToString("HH:mm", CultureInfo.InvariantCulture),
            DurationHours = r.DuracionHoras,
            PlayersType = r.ModalidadJuego,
            Participants = participants,
            EquipmentAssigned = equipment,
            CourtCost = r.CostoCancha,
            EquipmentCost = r.CostoEquipamiento,
            TotalCost = r.CostoTotal,
            DepositPaid = r.SenaPagada,
            DepositReceiptNumber = r.ReciboSenaId,
            DepositPaymentMethod = r.MetodoPagoSena,
            RemainingBalance = r.SaldoPendiente,
            RemainingPaid = r.SaldoAbonado,
            FinalReceiptNumber = r.ReciboFinalId,
            Status = r.Estado,
            CreatedAt = r.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            UpdatedAt = r.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            CancellationDetails = cancellation
        };
    }

    private static ReceiptResponseDto MapReciboToReceiptDto(Recibo recibo, Pago pago, List<ReceiptItemDto> items)
    {
        return new ReceiptResponseDto
        {
            Id = recibo.Id,
            ReservationId = recibo.ReservaId,
            ClientName = recibo.ClienteNombre,
            ClientDni = recibo.ClienteDni,
            Concept = recibo.Concepto,
            Items = items,
            TotalPaid = recibo.TotalAbonado,
            PaymentMethod = recibo.MedioPago,
            Status = pago.Estado,
            Date = recibo.FechaEmision.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            QrData = recibo.QrData,
            PaymentUrl = pago.PaymentUrl,
            MpPreferenceId = pago.MpPreferenceId,
            CashierName = recibo.CajeroNombre
        };
    }
}
