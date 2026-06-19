using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Domain.Services;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReservationsController : ControllerBase
    {
        private readonly IReservationService _reservationService;
        private readonly IReservationRepository _reservationRepo;
        private readonly ILessonRepository _lessonRepo;
        private readonly IWorkoutRepository _workoutRepo;
        private readonly IInstructorRepository _instructorRepo;
        private readonly IUserRepository _userRepo;
        private readonly IWaitingListRepository _waitingListRepo;
        private readonly IConfiguration _config;

        public ReservationsController(
            IReservationService reservationService,
            IReservationRepository reservationRepo,
            ILessonRepository lessonRepo,
            IWorkoutRepository workoutRepo,
            IInstructorRepository instructorRepo,
            IUserRepository userRepo,
            IWaitingListRepository waitingListRepo,
            IConfiguration config)
        {
            _reservationService = reservationService;
            _reservationRepo = reservationRepo;
            _lessonRepo = lessonRepo;
            _workoutRepo = workoutRepo;
            _instructorRepo = instructorRepo;
            _userRepo = userRepo;
            _waitingListRepo = waitingListRepo;
            _config = config;
        }

        // Member-facing detail for one lesson: full info, occupancy, seat occupancy, the registered
        // members (by username) and the caller's own reservation.
        [HttpGet("lesson/{lessonId:guid}")]
        [Authorize]
        public async Task<IActionResult> LessonDetail(Guid lessonId)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? throw new InvalidOperationException("User id missing"));

            var lesson = (await _lessonRepo.GetByIdsAsync(new[] { lessonId })).FirstOrDefault();
            if (lesson == null) return NotFound();

            var active = lesson.Reservations.Where(r => r.Status != ReservationStatus.Cancelled).ToList();
            var workout = await _workoutRepo.GetByIdAsync(lesson.WorkoutId);
            var instructor = lesson.InstructorId is { } iid ? await _instructorRepo.GetByIdAsync(iid) : null;

            var participants = new List<object>();
            foreach (var r in active.OrderBy(r => r.SeatNumber ?? int.MaxValue))
            {
                var u = await _userRepo.GetByIdAsync(r.MemberId);
                // Show only the member-visible username set on the profile screen. The full name is
                // private and must not be exposed to other members, so fall back to a neutral label
                // rather than FullName when no username has been set.
                var name = !string.IsNullOrWhiteSpace(u?.UserName) ? u!.UserName! : "Lid";
                participants.Add(new { name, isMe = r.MemberId == userId, seat = r.SeatNumber });
            }

            var mine = active.FirstOrDefault(r => r.MemberId == userId);
            var waiting = (await _waitingListRepo.GetForLessonAsync(lessonId)).ToList();

            return Ok(new
            {
                lessonId = lesson.Id,
                workout = workout?.Name ?? "Les",
                description = workout?.Description,
                startAt = lesson.StartAt,
                durationMinutes = lesson.DurationMinutes,
                instructor = instructor?.Name,
                capacity = lesson.Capacity,
                reserved = active.Count,
                allowsSeatSelection = lesson.Location.AllowsSeatSelection,
                occupiedSeats = active.Where(r => r.SeatNumber.HasValue).Select(r => r.SeatNumber!.Value).ToList(),
                reservedByMe = mine != null,
                mySeat = mine?.SeatNumber,
                myReservationId = mine?.Id,
                onWaitlist = waiting.Any(w => w.MemberId == userId),
                waitlistCount = waiting.Count,
                participants
            });
        }

        // Leave a lesson's waiting list.
        [HttpPost("waitlist/leave/{lessonId:guid}")]
        [Authorize]
        public async Task<IActionResult> LeaveWaitlist(Guid lessonId)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            await _reservationService.LeaveWaitlistAsync(userId, lessonId);
            return Ok();
        }

        // The lesson ids the member is currently waitlisted for (used to flag rooster cards).
        [HttpGet("waitlist/mine")]
        [Authorize]
        public async Task<IActionResult> MyWaitlist()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var ids = (await _waitingListRepo.GetForUserAsync(userId)).Select(w => w.LessonId).Distinct();
            return Ok(ids);
        }

        // Lessons the member is waitlisted for that now have a free spot (the in-app "spot freed" alert).
        [HttpGet("waitlist/available")]
        [Authorize]
        public async Task<IActionResult> WaitlistAvailable()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var lessonIds = (await _waitingListRepo.GetForUserAsync(userId)).Select(w => w.LessonId).Distinct().ToList();
            if (lessonIds.Count == 0) return Ok(Array.Empty<object>());

            var now = DateTime.UtcNow;
            var lessons = (await _lessonRepo.GetByIdsAsync(lessonIds)).Where(l => l.StartAt >= now).ToList();
            var workouts = (await _workoutRepo.GetAllAsync()).ToDictionary(w => w.Id, w => w.Name);

            var result = lessons
                .Where(l => l.Reservations.Count(r => r.Status != ReservationStatus.Cancelled) < l.Capacity)
                .OrderBy(l => l.StartAt)
                .Select(l => new
                {
                    lessonId = l.Id,
                    workout = workouts.GetValueOrDefault(l.WorkoutId, "Les"),
                    startAt = l.StartAt
                });

            return Ok(result);
        }

        // The signed-in instructor's own scheduled lessons (those they teach), for managing
        // attendance/registrations. Window: yesterday through two weeks ahead.
        [HttpGet("instructor/mine")]
        [Authorize]
        public async Task<IActionResult> InstructorLessons()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var user = await _userRepo.GetByIdAsync(userId);
            if (user is null || user.Role != SportVitaal.Domain.Enums.Role.Instructor || user.InstructorId is not { } instructorId)
                return Forbid();

            var from = DateTime.UtcNow.Date.AddDays(-1);
            var to = DateTime.UtcNow.Date.AddDays(14);
            var lessons = await _lessonRepo.GetForInstructorAsync(instructorId, from, to);

            var workouts = (await _workoutRepo.GetAllAsync()).ToDictionary(w => w.Id, w => w.Name);

            var result = lessons.Select(l => new
            {
                lessonId = l.Id,
                workout = workouts.GetValueOrDefault(l.WorkoutId, "Les"),
                startAt = l.StartAt,
                durationMinutes = l.DurationMinutes,
                location = l.Location.Name,
                reserved = l.Reservations.Count(r => r.Status != ReservationStatus.Cancelled),
                capacity = l.Capacity
            });

            return Ok(result);
        }

        private bool TryGetUserId(out Guid userId)
        {
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out userId);
        }

        // The authenticated member's own upcoming reserved lessons, with workout/instructor names
        // and occupancy resolved server-side.
        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> Mine()
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? throw new InvalidOperationException("User id missing"));

            var lessonIds = (await _reservationRepo.GetForUserAsync(userId))
                .Where(r => r.Status == ReservationStatus.Reserved)
                .Select(r => r.LessonId)
                .Distinct()
                .ToList();
            if (lessonIds.Count == 0) return Ok(Array.Empty<object>());

            var now = DateTime.UtcNow;
            var lessons = (await _lessonRepo.GetByIdsAsync(lessonIds))
                .Where(l => l.StartAt >= now)
                .OrderBy(l => l.StartAt)
                .ToList();

            var workouts = (await _workoutRepo.GetAllAsync()).ToDictionary(w => w.Id, w => w.Name);
            var instructors = (await _instructorRepo.GetAllAsync()).ToDictionary(i => i.Id, i => i.Name);

            var result = lessons.Select(l => new
            {
                lessonId = l.Id,
                workout = workouts.GetValueOrDefault(l.WorkoutId, "Les"),
                startAt = l.StartAt,
                durationMinutes = l.DurationMinutes,
                instructor = l.InstructorId is { } id ? instructors.GetValueOrDefault(id) : null,
                reserved = l.Reservations.Count(r => r.Status != ReservationStatus.Cancelled),
                capacity = l.Capacity
            });

            return Ok(result);
        }

        // The signed-in member's lesson history: lessons they took part in (a non-cancelled
        // reservation) that have already taken place, limited to the past year. Most recent first.
        [HttpGet("history")]
        [Authorize]
        public async Task<IActionResult> History()
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? throw new InvalidOperationException("User id missing"));

            var lessonIds = (await _reservationRepo.GetForUserAsync(userId))
                .Where(r => r.Status != ReservationStatus.Cancelled)
                .Select(r => r.LessonId)
                .Distinct()
                .ToList();
            if (lessonIds.Count == 0) return Ok(Array.Empty<object>());

            var now = DateTime.UtcNow;
            var since = now.AddYears(-1);
            var lessons = (await _lessonRepo.GetByIdsAsync(lessonIds))
                .Where(l => l.StartAt < now && l.StartAt >= since)
                .OrderByDescending(l => l.StartAt)
                .ToList();

            var workouts = (await _workoutRepo.GetAllAsync()).ToDictionary(w => w.Id, w => w.Name);
            var instructors = (await _instructorRepo.GetAllAsync()).ToDictionary(i => i.Id, i => i.Name);

            var result = lessons.Select(l => new
            {
                lessonId = l.Id,
                workout = workouts.GetValueOrDefault(l.WorkoutId, "Les"),
                startAt = l.StartAt,
                durationMinutes = l.DurationMinutes,
                instructor = l.InstructorId is { } id ? instructors.GetValueOrDefault(id) : null
            });

            return Ok(result);
        }

        [HttpPost("reserve")]
        [Authorize]
        public async Task<IActionResult> Reserve([FromBody] ReserveDto dto)
        {
            // Caller identity
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User id missing"));

            try
            {
                await _reservationService.ReserveAsync(userId, dto.LessonId, dto.Seat);
                return Ok();
            }
            catch (DomainException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("cancel/{reservationId}")]
        [Authorize]
        public async Task<IActionResult> Cancel(Guid reservationId)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User id missing"));
            try
            {
                await _reservationService.CancelReservationAsync(reservationId, userId);
                return Ok();
            }
            catch (DomainException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // The lesson the member can currently check in for: their soonest reserved (not yet attended)
        // lesson within the check-in window (from 30 min before start until it ends). A lessonId may be
        // supplied to target a specific reservation (e.g. when arriving from a lesson card). Returns 204
        // when there is nothing to check in for.
        [HttpGet("checkin/current")]
        [Authorize]
        public async Task<IActionResult> CheckInTarget([FromQuery] Guid? lessonId = null)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var myReservations = (await _reservationRepo.GetForUserAsync(userId))
                .Where(r => r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.Attended)
                .ToList();

            var lessonIds = myReservations.Select(r => r.LessonId).Distinct().ToList();
            if (lessonIds.Count == 0) return NoContent();

            var now = DateTime.UtcNow;
            var lessons = await _lessonRepo.GetByIdsAsync(lessonIds);

            Lesson? lesson = lessonId is { } id
                ? lessons.FirstOrDefault(l => l.Id == id)
                : lessons
                    .Where(l => now >= l.StartAt.AddMinutes(-30) && now <= l.StartAt.AddMinutes(l.DurationMinutes))
                    .OrderBy(l => l.StartAt)
                    .FirstOrDefault();

            if (lesson == null) return NoContent();

            var workout = await _workoutRepo.GetByIdAsync(lesson.WorkoutId);
            var alreadyCheckedIn = lesson.Reservations
                .Any(r => r.MemberId == userId && r.Status == ReservationStatus.Attended);
            var canCheckInNow = now >= lesson.StartAt.AddMinutes(-30)
                && now <= lesson.StartAt.AddMinutes(lesson.DurationMinutes);

            return Ok(new
            {
                lessonId = lesson.Id,
                workout = workout?.Name ?? "Les",
                startAt = lesson.StartAt,
                durationMinutes = lesson.DurationMinutes,
                location = lesson.Location.Name,
                alreadyCheckedIn,
                canCheckInNow
            });
        }

        // Check in for a lesson via GPS (verified against the club location) or RFID (simulated at the
        // door). Marks the member's reservation as attended.
        [HttpPost("checkin")]
        [Authorize]
        public async Task<IActionResult> CheckIn([FromBody] CheckInDto dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            if (string.Equals(dto.Method, "gps", StringComparison.OrdinalIgnoreCase))
            {
                if (dto.Latitude is not { } lat || dto.Longitude is not { } lng)
                    return BadRequest("Locatie ontbreekt. Sta locatietoegang toe en probeer opnieuw.");

                var clubLat = _config.GetValue("ClubLocation:Latitude", 52.3676);
                var clubLng = _config.GetValue("ClubLocation:Longitude", 4.9041);
                var radius = _config.GetValue("ClubLocation:RadiusMeters", 200.0);

                var meters = DistanceInMeters(clubLat, clubLng, lat, lng);
                if (meters > radius)
                    return BadRequest($"Je bevindt je niet bij de sportclub (±{(int)meters} m). Check in bij de zaal.");
            }
            // RFID check-in is simulated: a real reader at the door would have authenticated the pass.

            try
            {
                await _reservationService.CheckInAsync(userId, dto.LessonId);
                return Ok();
            }
            catch (DomainException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Great-circle distance between two WGS84 coordinates, in metres (haversine formula).
        private static double DistanceInMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadius = 6_371_000; // metres
            double ToRad(double deg) => deg * Math.PI / 180.0;

            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }

    public class ReserveDto
    {
        public Guid LessonId { get; set; }

        /// <summary>Optional flat seat number (1..Capacity) for seat-selection lessons (e.g. a spinning bike).</summary>
        public int? Seat { get; set; }
    }

    public class CheckInDto
    {
        public Guid LessonId { get; set; }
        public string Method { get; set; } = "gps"; // "gps" or "rfid"
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}

