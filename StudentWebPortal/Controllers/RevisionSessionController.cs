using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentWebPortal.Data;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Model.Entity;
using StudentWebPortal.Model.Entity.Enum;

namespace StudentWebPortal.Controllers;

[Route("api/[controller]")]
public sealed class RevisionSessionController(StudentWebPortalContext context) : ApiControllerBase
{
    private const string ConcurrencyMessage = "This session was changed by another request. Reload the record and retry.";

    /// <summary>Lists revision sessions, newest first. fromDate/toDate are inclusive calendar days.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<RevisionSessionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RevisionSessionDto>>> GetAll(
        [FromQuery] int? studentId, [FromQuery] SessionStatus? status,
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (ValidatePaging(page, pageSize) is { } invalidPaging) return invalidPaging;
        if (ValidateDateRange(fromDate, toDate) is { } invalidRange) return invalidRange;

        var query = context.RevisionSessions.AsNoTracking().Include(s => s.Student).AsQueryable();
        if (studentId.HasValue) query = query.Where(s => s.StudentId == studentId.Value);
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        if (fromDate.HasValue) { var from = StartOfDayUtc(fromDate.Value); query = query.Where(s => s.SessionDate >= from); }
        if (toDate.HasValue) { var to = StartOfNextDayUtc(toDate.Value); query = query.Where(s => s.SessionDate < to); }

        SetPagingHeaders(await query.CountAsync(cancellationToken), page, pageSize);
        var records = await query.OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(records.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<RevisionSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RevisionSessionDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var session = await context.RevisionSessions.AsNoTracking().Include(s => s.Student)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return session is null ? NotFound() : Ok(ToDto(session));
    }

    [HttpPost]
    [ProducesResponseType<RevisionSessionDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RevisionSessionDto>> Create([FromBody] RevisionSessionCreateDto dto, CancellationToken cancellationToken)
    {
        if (!await context.Students.AnyAsync(s => s.StudentId == dto.StudentId, cancellationToken))
            return BadRequestProblem($"Student with Id {dto.StudentId} does not exist.");

        var session = new RevisionSession
        {
            StudentId = dto.StudentId,
            SessionDate = StartOfDayUtc(dto.SessionDate),
            Surah = dto.Surah,
            VerseStart = dto.VerseStart,
            VerseEnd = dto.VerseEnd,
            Status = dto.Status,
            Rank = dto.Rank,
            DurationMinutes = dto.DurationMinutes,
            Notes = NormalizeOptional(dto.Notes),
            RecordedBy = dto.RecordedBy
        };
        context.RevisionSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        await context.Entry(session).Reference(s => s.Student).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = session.Id }, ToDto(session));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] RevisionSessionUpdateDto dto, CancellationToken cancellationToken)
    {
        if (!await context.Students.AnyAsync(s => s.StudentId == dto.StudentId, cancellationToken))
            return BadRequestProblem($"Student with Id {dto.StudentId} does not exist.");

        var session = await context.RevisionSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null) return NotFound();

        if (dto.RowVersion is not null) context.Entry(session).Property(s => s.RowVersion).OriginalValue = dto.RowVersion;
        session.StudentId = dto.StudentId;
        session.SessionDate = StartOfDayUtc(dto.SessionDate);
        session.Surah = dto.Surah;
        session.VerseStart = dto.VerseStart;
        session.VerseEnd = dto.VerseEnd;
        session.Status = dto.Status;
        session.Rank = dto.Rank;
        session.DurationMinutes = dto.DurationMinutes;
        session.Notes = NormalizeOptional(dto.Notes);
        session.RecordedBy = dto.RecordedBy;
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            if (!await context.RevisionSessions.AnyAsync(s => s.Id == id, cancellationToken)) return NotFound();
            return ConflictProblem(ConcurrencyMessage);
        }
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var session = await context.RevisionSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null) return NotFound();
        context.RevisionSessions.Remove(session);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static RevisionSessionDto ToDto(RevisionSession s) => new()
    {
        Id = s.Id, StudentId = s.StudentId, StudentName = s.Student?.StudentName,
        SessionDate = s.SessionDate, Surah = s.Surah, VerseStart = s.VerseStart,
        VerseEnd = s.VerseEnd, Status = s.Status, Rank = s.Rank,
        DurationMinutes = s.DurationMinutes, Notes = s.Notes, RecordedBy = s.RecordedBy,
        CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt, RowVersion = s.RowVersion
    };
}
