using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentWebPortal.Data;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Model.Entity;

namespace StudentWebPortal.Controllers;

[Route("api/[controller]")]
public sealed class AttendanceController(StudentWebPortalContext context) : ApiControllerBase
{
    private static readonly string[] PatchableFields = ["SessionStatus", "Notes", "RecordedBy"];
    private const string ConcurrencyMessage = "This attendance record was changed by another request. Reload it and retry.";

    [HttpGet]
    [ProducesResponseType<IEnumerable<AttendanceDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AttendanceDto>>> GetAllAttendances(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (ValidatePaging(page, pageSize) is { } invalidPaging) return invalidPaging;
        return Ok(await ListAsync(context.Attendances.AsNoTracking(), page, pageSize, cancellationToken));
    }

    [HttpGet("student/{studentId:int}")]
    [ProducesResponseType<IEnumerable<AttendanceDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AttendanceDto>>> GetByStudentId(
        int studentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (ValidatePaging(page, pageSize) is { } invalidPaging) return invalidPaging;
        var query = context.Attendances.AsNoTracking().Where(a => a.StudentId == studentId);
        return Ok(await ListAsync(query, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<AttendanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var record = await context.Attendances.AsNoTracking().Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return record is null ? NotFound() : Ok(ToDto(record));
    }

    /// <summary>Records attendance for a student. The date defaults to today (UTC); one record per student per day.</summary>
    [HttpPost]
    [ProducesResponseType<AttendanceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttendanceDto>> CreateAttendance([FromBody] AttendanceCreateDto dto, CancellationToken cancellationToken)
    {
        if (!await context.Students.AnyAsync(s => s.StudentId == dto.StudentId, cancellationToken))
            return BadRequestProblem($"Student with Id {dto.StudentId} does not exist.");

        var attendance = new Attendance
        {
            StudentId = dto.StudentId,
            RecordedBy = dto.RecordedBy,
            SessionStatus = dto.SessionStatus,
            Notes = NormalizeOptional(dto.Notes),
            AttendanceDate = StartOfDayUtc(dto.AttendanceDate ?? DateTime.UtcNow)
        };
        context.Attendances.Add(attendance);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ConflictProblem("An attendance record already exists for this student and date.");
        }

        await context.Entry(attendance).Reference(a => a.Student).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = attendance.Id }, ToDto(attendance));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAttendance(int id, [FromBody] UpdateAttendanceDto dto, CancellationToken cancellationToken)
    {
        var attendance = await context.Attendances.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attendance is null) return NotFound();

        attendance.SessionStatus = dto.SessionStatus;
        attendance.Notes = NormalizeOptional(dto.Notes);
        attendance.RecordedBy = dto.RecordedBy;
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return ConflictProblem(ConcurrencyMessage); }
        return NoContent();
    }

    /// <summary>RFC 6902 JSON Patch limited to SessionStatus, Notes and RecordedBy.</summary>
    [HttpPatch("{id:int}")]
    [ProducesResponseType<AttendanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttendanceDto>> PatchAttendance(int id, [FromBody] JsonPatchDocument<Attendance> patchDoc, CancellationToken cancellationToken)
    {
        if (patchDoc is null) return BadRequestProblem("A JSON Patch document is required.");
        if (patchDoc.Operations.Any(op => !IsAllowedPatchPath(op.path) || (op.from is not null && !IsAllowedPatchPath(op.from))))
            return BadRequestProblem("PATCH only supports SessionStatus, Notes, and RecordedBy fields.");

        var attendance = await context.Attendances.Include(a => a.Student).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attendance is null) return NotFound();

        patchDoc.ApplyTo(attendance, ModelState);
        if (!ModelState.IsValid || !TryValidateModel(attendance)) return ValidationProblem(ModelState);

        attendance.Notes = NormalizeOptional(attendance.Notes);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return ConflictProblem(ConcurrencyMessage); }
        return Ok(ToDto(attendance));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAttendance(int id, CancellationToken cancellationToken)
    {
        var record = await context.Attendances.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (record is null) return NotFound();

        context.Attendances.Remove(record);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return ConflictProblem(ConcurrencyMessage); }
        return NoContent();
    }

    private async Task<List<AttendanceDto>> ListAsync(IQueryable<Attendance> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        SetPagingHeaders(await query.CountAsync(cancellationToken), page, pageSize);
        var records = await query.Include(a => a.Student)
            .OrderByDescending(a => a.AttendanceDate).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return records.Select(ToDto).ToList();
    }

    private static AttendanceDto ToDto(Attendance a) => new()
    {
        Id = a.Id,
        StudentId = a.StudentId,
        StudentName = a.Student?.StudentName,
        AttendanceDate = a.AttendanceDate,
        SessionStatus = a.SessionStatus,
        Notes = a.Notes,
        RecordedBy = a.RecordedBy,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
        RowVersion = a.RowVersion
    };

    private static bool IsAllowedPatchPath(string path)
    {
        var field = path.TrimStart('/')
            .Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);
        return PatchableFields.Contains(field, StringComparer.OrdinalIgnoreCase);
    }
}
