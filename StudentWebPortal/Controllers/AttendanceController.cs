using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentWebPortal.Data;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Model.Entity;
using Npgsql;

namespace StudentWebPortal.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AttendanceController(StudentWebPortalContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Attendance>>> GetAllAttendances([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (!IsValidPaging(page, pageSize)) return BadRequest("Page must be between 1 and 1,000,000 and pageSize must be between 1 and 100.");
        var query = context.Attendances.AsNoTracking();
        await SetPagingHeadersAsync(query, page, pageSize, cancellationToken);
        var records = await query.OrderByDescending(a => a.AttendanceDate).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(records);
    }

    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<Attendance>>> GetByStudentId(int studentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (!IsValidPaging(page, pageSize)) return BadRequest("Page must be between 1 and 1,000,000 and pageSize must be between 1 and 100.");
        var query = context.Attendances.AsNoTracking().Where(a => a.StudentId == studentId);
        await SetPagingHeadersAsync(query, page, pageSize, cancellationToken);
        var records = await query.OrderByDescending(a => a.AttendanceDate).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(records);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Attendance>> GetById(int id, CancellationToken cancellationToken)
    {
        var record = await context.Attendances.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return record is null ? NotFound() : Ok(record);
    }

    [HttpPost]
    public async Task<ActionResult<Attendance>> CreateAttendance([FromBody] AttendanceCreateDto dto, CancellationToken cancellationToken)
    {
        if (!await context.Students.AnyAsync(s => s.StudentId == dto.StudentId, cancellationToken))
            return BadRequest($"Student with Id {dto.StudentId} does not exist.");

        var attendance = new Attendance
        {
            StudentId = dto.StudentId,
            RecordedBy = dto.RecordedBy,
            SessionStatus = dto.SessionStatus,
            Notes = NormalizeOptional(dto.Notes),
            AttendanceDate = DateTime.UtcNow.Date
        };
        context.Attendances.Add(attendance);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict("An attendance record already exists for this student and date.");
        }
        return CreatedAtAction(nameof(GetById), new { id = attendance.Id }, attendance);
    }


    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAttendance(int id, [FromBody] UpdateAttendanceDto dto, CancellationToken cancellationToken)
    {
        var attendance = await context.Attendances.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attendance is null) return NotFound();
        attendance.SessionStatus = dto.SessionStatus;
        attendance.Notes = NormalizeOptional(dto.Notes);
        attendance.RecordedBy = dto.RecordedBy;
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict("This attendance record was changed by another request. Reload it and retry."); }
        return NoContent();
    }


    [HttpPatch("{id:int}")]
    public async Task<IActionResult> PatchAttendance(int id, [FromBody] JsonPatchDocument<Attendance> patchDoc, CancellationToken cancellationToken)
    {
        if (patchDoc is null) return BadRequest("A JSON Patch document is required.");
        if (patchDoc.Operations.Any(op => !IsAllowedPatchPath(op.path) || (op.from is not null && !IsAllowedPatchPath(op.from))))
            return BadRequest("PATCH only supports SessionStatus, Notes, and RecordedBy fields.");
        var attendance = await context.Attendances.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attendance is null) return NotFound();
        patchDoc.ApplyTo(attendance, ModelState);
        if (!ModelState.IsValid || !TryValidateModel(attendance)) return ValidationProblem(ModelState);
        attendance.Notes = NormalizeOptional(attendance.Notes);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict("This attendance record was changed by another request. Reload it and retry."); }
        return Ok(attendance);
    }


    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAttendance(int id, CancellationToken cancellationToken)
    {
        var record = await context.Attendances.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (record is null) return NotFound();
        context.Attendances.Remove(record);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict("This attendance record was changed by another request. Reload it and retry."); }
        return NoContent();
    }


    private static bool IsValidPaging(int page, int pageSize) => page is >= 1 and <= 1_000_000 && pageSize is >= 1 and <= 100;
    private async Task SetPagingHeadersAsync(IQueryable<Attendance> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        Response.Headers["X-Total-Count"] = (await query.CountAsync(cancellationToken)).ToString();
        Response.Headers["X-Page"] = page.ToString();
        Response.Headers["X-Page-Size"] = pageSize.ToString();
    }
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsAllowedPatchPath(string path) => new[] { "SessionStatus", "Notes", "RecordedBy" }.Contains(path.TrimStart('/').Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal), StringComparer.OrdinalIgnoreCase);
}
