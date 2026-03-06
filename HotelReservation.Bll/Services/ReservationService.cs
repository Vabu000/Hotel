using AutoMapper;
using Microsoft.Extensions.Logging;
using HotelReservation.Bll.DTOs;
using HotelReservation.Bll.Events;
using HotelReservation.Bll.Services;
using HotelReservation.Dal.Interfaces;
using HotelReservation.Domain.Entities;
using HotelReservation.Domain.Exceptions;
using HotelPlatform.ServiceDefaults.Events;

namespace HotelReservation.Bll.Services
{
    public class ReservationService : IReservationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<ReservationService> _logger;
        private readonly IEventPublisher _eventPublisher;

        public ReservationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<ReservationService> logger,
            IEventPublisher eventPublisher)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _eventPublisher = eventPublisher;
        }

        public async Task<ReservationDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting reservation with ID: {ReservationId}", id);

            var reservation = await _unitOfWork.Reservations.GetByIdAsync(id, cancellationToken);

            if (reservation == null)
            {
                _logger.LogWarning("Reservation with ID {ReservationId} not found", id);
                return null;
            }

            return _mapper.Map<ReservationDto>(reservation);
        }

        public async Task<IEnumerable<ReservationDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting all reservations");

            var reservations = await _unitOfWork.Reservations.GetAllAsync(cancellationToken);
            return _mapper.Map<IEnumerable<ReservationDto>>(reservations);
        }

        public async Task<ReservationDto> CreateAsync(CreateReservationDto createReservationDto, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating new reservation for client ID: {ClientId}, room ID: {RoomId}",
                createReservationDto.ClientId, createReservationDto.RoomId);

            if (createReservationDto.CheckInDate >= createReservationDto.CheckOutDate)
            {
                throw new ValidationException("Check-out date must be after check-in date");
            }

            if (createReservationDto.CheckInDate < DateTime.Today)
            {
                throw new ValidationException("Check-in date cannot be in the past");
            }

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var client = await _unitOfWork.Clients.GetByIdAsync(createReservationDto.ClientId, cancellationToken);
                if (client == null)
                {
                    throw new NotFoundException($"Client with ID {createReservationDto.ClientId} not found");
                }

                var room = await _unitOfWork.Rooms.GetByIdAsync(createReservationDto.RoomId, cancellationToken);
                if (room == null)
                {
                    throw new NotFoundException($"Room with ID {createReservationDto.RoomId} not found");
                }

                var availableRooms = await _unitOfWork.Rooms.GetAvailableRoomsAsync(
                    createReservationDto.CheckInDate,
                    createReservationDto.CheckOutDate,
                    cancellationToken);

                if (!availableRooms.Any(r => r.RoomId == createReservationDto.RoomId))
                {
                    throw new BusinessConflictException($"Room {room.RoomNumber} is not available for the selected dates");
                }

                var reservation = _mapper.Map<Reservation>(createReservationDto);
                reservation.Status = ReservationStatus.Pending;

                var reservationId = await _unitOfWork.Reservations.CreateAsync(reservation, cancellationToken);
                reservation.ReservationId = reservationId;

                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("Reservation created with ID: {ReservationId}", reservationId);

                var createdReservation = await _unitOfWork.Reservations.GetWithDetailsAsync(reservationId, cancellationToken);

                // ✅ Publish ReservationMadeEvent to RabbitMQ
                var totalPrice = CalculateTotalPrice(100m, reservation.CheckInDate, reservation.CheckOutDate); // Default price
                var reservationEvent = new ReservationMadeEvent(
                    reservationId: reservationId,
                    clientId: reservation.ClientId,
                    roomId: reservation.RoomId,
                    checkInDate: reservation.CheckInDate,
                    checkOutDate: reservation.CheckOutDate,
                    totalPrice: totalPrice,
                    status: reservation.Status.ToString());

                await _eventPublisher.PublishAsync(reservationEvent, cancellationToken);

                return _mapper.Map<ReservationDto>(createdReservation);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<ReservationDto> UpdateStatusAsync(UpdateReservationStatusDto updateStatusDto, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Updating reservation status for ID: {ReservationId} to {Status}",
                updateStatusDto.ReservationId, updateStatusDto.Status);

            var reservation = await _unitOfWork.Reservations.GetByIdAsync(updateStatusDto.ReservationId, cancellationToken);
            if (reservation == null)
            {
                throw new NotFoundException($"Reservation with ID {updateStatusDto.ReservationId} not found");
            }

            ValidateStatusTransition(reservation.Status, updateStatusDto.Status);

            var isUpdated = await _unitOfWork.Reservations.UpdateStatusAsync(
                updateStatusDto.ReservationId,
                updateStatusDto.Status,
                cancellationToken);

            if (!isUpdated)
            {
                throw new BusinessConflictException($"Failed to update reservation status for ID {updateStatusDto.ReservationId}");
            }

            reservation.Status = updateStatusDto.Status;
            _logger.LogInformation("Reservation status updated successfully for ID: {ReservationId}", updateStatusDto.ReservationId);

            return _mapper.Map<ReservationDto>(reservation);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Deleting reservation with ID: {ReservationId}", id);

            var reservation = await _unitOfWork.Reservations.GetByIdAsync(id, cancellationToken);
            if (reservation == null)
            {
                throw new NotFoundException($"Reservation with ID {id} not found");
            }
            
            if (reservation.Status != ReservationStatus.Cancelled)
            {
                throw new BusinessConflictException("Can only delete cancelled reservations");
            }

            var isDeleted = await _unitOfWork.Reservations.DeleteAsync(id, cancellationToken);

            if (isDeleted)
            {
                _logger.LogInformation("Reservation with ID {ReservationId} deleted successfully", id);
            }

            return isDeleted;
        }

        public async Task<IEnumerable<ReservationDto>> GetByClientIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting reservations for client ID: {ClientId}", clientId);

            var reservations = await _unitOfWork.Reservations.GetByClientIdAsync(clientId, cancellationToken);
            return _mapper.Map<IEnumerable<ReservationDto>>(reservations);
        }

        public async Task<ReservationDto?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting reservation with details for ID: {ReservationId}", id);

            var reservation = await _unitOfWork.Reservations.GetWithDetailsAsync(id, cancellationToken);

            if (reservation == null)
            {
                _logger.LogWarning("Reservation with ID {ReservationId} not found", id);
                return null;
            }

            return _mapper.Map<ReservationDto>(reservation);
        }

        public async Task<IEnumerable<ReservationDto>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting reservations for date range: {From} - {To}", from, to);

            if (from > to)
            {
                throw new ValidationException("Start date cannot be after end date");
            }

            var reservations = await _unitOfWork.Reservations.GetByDateRangeAsync(from, to, cancellationToken);
            return _mapper.Map<IEnumerable<ReservationDto>>(reservations);
        }

        public async Task<ReservationDto> CheckInAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Check-in for reservation ID: {ReservationId}", reservationId);

            var updateStatusDto = new UpdateReservationStatusDto
            {
                ReservationId = reservationId,
                Status = ReservationStatus.CheckedIn
            };

            return await UpdateStatusAsync(updateStatusDto, cancellationToken);
        }

        public async Task<ReservationDto> CheckOutAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Check-out for reservation ID: {ReservationId}", reservationId);

            var updateStatusDto = new UpdateReservationStatusDto
            {
                ReservationId = reservationId,
                Status = ReservationStatus.CheckedOut
            };

            return await UpdateStatusAsync(updateStatusDto, cancellationToken);
        }

        public async Task<ReservationDto> CancelAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Cancelling reservation ID: {ReservationId}", reservationId);

            var updateStatusDto = new UpdateReservationStatusDto
            {
                ReservationId = reservationId,
                Status = ReservationStatus.Cancelled
            };

            return await UpdateStatusAsync(updateStatusDto, cancellationToken);
        }

        private static void ValidateStatusTransition(ReservationStatus currentStatus, ReservationStatus newStatus)
        {
            var validTransitions = new Dictionary<ReservationStatus, ReservationStatus[]>
            {
                [ReservationStatus.Pending] = new[] { ReservationStatus.Confirmed, ReservationStatus.Cancelled },
                [ReservationStatus.Confirmed] = new[] { ReservationStatus.CheckedIn, ReservationStatus.Cancelled },
                [ReservationStatus.CheckedIn] = new[] { ReservationStatus.CheckedOut },
                [ReservationStatus.CheckedOut] = new ReservationStatus[] { }, 
                [ReservationStatus.Cancelled] = new ReservationStatus[] { } 
            };

            if (!validTransitions.ContainsKey(currentStatus) ||
                !validTransitions[currentStatus].Contains(newStatus))
            {
                throw new BusinessConflictException($"Cannot change status from {currentStatus} to {newStatus}");
            }
        }

        private static decimal CalculateTotalPrice(decimal pricePerNight, DateTime checkIn, DateTime checkOut)
        {
            var nights = (checkOut - checkIn).Days;
            return pricePerNight * nights;
        }
    }
}