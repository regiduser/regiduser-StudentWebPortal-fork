using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentWebPortal.Data;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Model.Entity;
using StudentWebPortal.Model.Entity.Enum;

namespace StudentWebPortal.Controllers;


[ApiController]
[Route("api/[controller]")]
public sealed class RevisionSessionController(StudentWebPortalContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RevisionSessionDto>>> GetAll(
        [FromQuery] int? studentId, [FromQuery] SessionStatus? status,
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            return BadRequest("Page must be between 1 and 1,000,000 and pageSize must be between 1 and 100.");
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            return BadRequest("fromDate must be on or before toDate.");
        var query = context.RevisionSessions.AsNoTracking().Include(s => s.Student).AsQueryable();
        if (studentId.HasValue) query = query.Where(s => s.StudentId == studentId.Value);
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        if (fromDate.HasValue) query = query.Where(s => s.SessionDate >= DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc));
        if (toDate.HasValue) query = query.Where(s => s.SessionDate < DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc));
        Response.Headers["X-Total-Count"] = (await query.CountAsync(cancellationToken)).ToString();
        Response.Headers["X-Page"] = page.ToString();
        Response.Headers["X-Page-Size"] = pageSize.ToString();
        var records = await query.OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(records.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RevisionSessionDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var session = await context.RevisionSessions.AsNoTracking().Include(s => s.Student)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return session is null ? NotFound() : Ok(ToDto(session));
    }

    [HttpPost]
    public async Task<ActionResult<RevisionSessionDto>> Create([FromBody] RevisionSessionCreateDto dto, CancellationToken cancellationToken)
    {
        if (!await context.Students.AnyAsync(s => s.StudentId == dto.StudentId, cancellationToken))
            return BadRequest($"Student with Id {dto.StudentId} does not exist.");
        var session = new RevisionSession
        {
            StudentId = dto.StudentId,
            SessionDate = DateTime.SpecifyKind(dto.SessionDate.Date, DateTimeKind.Utc),
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
    public async Task<IActionResult> Update(int id, [FromBody] RevisionSessionUpdateDto dto, CancellationToken cancellationToken)
    {
        if (!await context.Students.AnyAsync(s => s.StudentId == dto.StudentId, cancellationToken))
            return BadRequest($"Student with Id {dto.StudentId} does not exist.");
        var session = await context.RevisionSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null) return NotFound();
        if (dto.RowVersion is not null) context.Entry(session).Property(s => s.RowVersion).OriginalValue = dto.RowVersion;
        session.StudentId = dto.StudentId;
        session.SessionDate = DateTime.SpecifyKind(dto.SessionDate.Date, DateTimeKind.Utc);
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
            return Conflict("This session was changed by another request. Reload the record and retry.");
        }
        return NoContent();
    }


    [HttpDelete("{id:int}")]
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


    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
