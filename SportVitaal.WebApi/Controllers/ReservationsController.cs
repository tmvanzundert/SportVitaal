using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportVitaal.Domain.Services;
using SportVitaal.Domain.ValueObjects;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReservationsController : ControllerBase
    {
        private readonly IReservationService _reservationService;

        public ReservationsController(IReservationService reservationService)
        {
            _reservationService = reservationService;
        }

        [HttpPost("reserve")]
        [Authorize]
        public async Task<IActionResult> Reserve([FromBody] ReserveDto dto)
        {
            // Caller identity
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User id missing"));

            Seat? seat = null;
            if (dto.SeatRow.HasValue && dto.SeatColumn.HasValue)
            {
                seat = new Seat(dto.SeatRow.Value, dto.SeatColumn.Value);
            }

            await _reservationService.ReserveAsync(userId, dto.LessonId, seat);
            return Ok();
        }

        [HttpPost("cancel/{reservationId}")]
        [Authorize]
        public async Task<IActionResult> Cancel(Guid reservationId)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User id missing"));
            await _reservationService.CancelReservationAsync(reservationId, userId);
            return Ok();
        }
    }

    public class ReserveDto
    {
        public Guid LessonId { get; set; }
        public int? SeatRow { get; set; }
        public int? SeatColumn { get; set; }
    }
}

