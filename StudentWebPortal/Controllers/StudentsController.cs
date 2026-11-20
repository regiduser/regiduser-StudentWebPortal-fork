using FluentValidation;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentWebPortal.Common;
using StudentWebPortal.Data;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Model.Entity;
using StudentWebPortal.Model.Entity.Enum;

namespace StudentWebPortal.Controllers;

[Route("api/[controller]")]
public sealed class StudentsController(StudentWebPortalContext context, IValidator<UpdateStudentDto> updateValidator) : ApiControllerBase
{
    private static readonly string[] PatchableFields = ["StudentName", "Email", "PhoneNumber", "EnrollmentDate", "Notes"];
    private const string DuplicateEmailMessage = "A student with this email already exists.";

    /// <summary>Lists students. Search is case-insensitive over name, email and phone number.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<Student>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Student>>> GetAllStudents(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (ValidatePaging(page, pageSize) is { } invalidPaging) return invalidPaging;

        var query = context.Students.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.StudentName, pattern) ||
                (s.Email != null && EF.Functions.ILike(s.Email, pattern)) ||
                (s.PhoneNumber != null && EF.Functions.ILike(s.PhoneNumber, pattern)));
        }
        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        SetPagingHeaders(await query.CountAsync(cancellationToken), page, pageSize);

        var students = await query.OrderBy(s => s.StudentName).ThenBy(s => s.StudentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return Ok(students);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<Student>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Student>> GetById(int id, CancellationToken cancellationToken)
    {
        var student = await context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == id, cancellationToken);
        return student is null ? NotFound() : Ok(student);
    }

    /// <summary>
    /// Progress snapshot for one student: attendance rate, plus Hifz and revision activity.
    /// Late counts as attended when computing the attendance rate.
    /// </summary>
    [HttpGet("{id:int}/summary")]
    [ProducesResponseType<StudentSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentSummaryDto>> GetSummary(int id, CancellationToken cancellationToken)
    {
        var student = await context.Students.AsNoTracking()
            .Where(s => s.StudentId == id)
            .Select(s => new { s.StudentId, s.StudentName, s.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        if (student is null) return NotFound();

        var attendance = context.Attendances.AsNoTracking().Where(a => a.StudentId == id);
        var present = await attendance.CountAsync(a => a.SessionStatus == SessionStatus.Present, cancellationToken);
        var late = await attendance.CountAsync(a => a.SessionStatus == SessionStatus.Late, cancellationToken);
        var absent = await attendance.CountAsync(a => a.SessionStatus == SessionStatus.Absent, cancellationToken);
        var total = present + late + absent;

        var hifzQuery = context.HifzSessions.AsNoTracking().Where(s => s.StudentId == id);
        var lastHifz = await hifzQuery.OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.Id)
            .Select(s => new { s.SessionDate, s.Surah, Verse = s.VerseStart })
            .FirstOrDefaultAsync(cancellationToken);

        var revisionQuery = context.RevisionSessions.AsNoTracking().Where(s => s.StudentId == id);
        var lastRevision = await revisionQuery.OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.Id)
            .Select(s => new { s.SessionDate, s.Surah, Verse = s.VerseEnd })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new StudentSummaryDto
        {
            StudentId = student.StudentId,
            StudentName = student.StudentName,
            IsActive = student.IsActive,
            Attendance = new AttendanceSummary(
                total, present, late, absent,
                total == 0 ? null : Math.Round((present + late) * 100.0 / total, 1)),
            Hifz = new SessionSummary(
                await hifzQuery.CountAsync(cancellationToken),
                await hifzQuery.SumAsync(s => s.DurationMinutes, cancellationToken),
                lastHifz?.SessionDate, lastHifz?.Surah?.ToString(), lastHifz?.Verse),
            Revision = new SessionSummary(
                await revisionQuery.CountAsync(cancellationToken),
                await revisionQuery.SumAsync(s => s.DurationMinutes, cancellationToken),
                lastRevision?.SessionDate, lastRevision?.Surah?.ToString(), lastRevision?.Verse)
        });
    }

    [HttpPost]
    [ProducesResponseType<Student>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Student>> Create([FromBody] StudentCreateDto dto, CancellationToken cancellationToken)
    {
        var student = new Student
        {
            StudentName = dto.StudentName.Trim(),
            Email = NormalizeEmail(dto.Email),
            PhoneNumber = PhoneNumberNormalizer.Normalize(dto.PhoneNumber),
            EnrollmentDate = dto.EnrollmentDate,
            Notes = NormalizeOptional(dto.Notes)
        };

        context.Students.Add(student);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ConflictProblem(DuplicateEmailMessage);
        }

        return CreatedAtAction(nameof(GetById), new { id = student.StudentId }, student);
    }

    /// <summary>Updates the supplied fields only; omitted (null) fields are left unchanged. Use PATCH to clear a value.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStudent(int id, [FromBody] UpdateStudentDto dto, CancellationToken cancellationToken)
    {
        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == id, cancellationToken);
        if (student is null) return NotFound();

        if (dto.StudentName is not null) student.StudentName = dto.StudentName.Trim();
        if (dto.Email is not null) student.Email = NormalizeEmail(dto.Email);
        if (dto.PhoneNumber is not null) student.PhoneNumber = PhoneNumberNormalizer.Normalize(dto.PhoneNumber);
        if (dto.EnrollmentDate.HasValue) student.EnrollmentDate = dto.EnrollmentDate.Value;
        if (dto.Notes is not null) student.Notes = NormalizeOptional(dto.Notes);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ConflictProblem(DuplicateEmailMessage);
        }
        return NoContent();
    }

    /// <summary>RFC 6902 JSON Patch limited to StudentName, Email, PhoneNumber, EnrollmentDate and Notes.</summary>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PatchStudent(int id, [FromBody] JsonPatchDocument<Student> patchDoc, CancellationToken cancellationToken)
    {
        if (patchDoc is null) return BadRequestProblem("A JSON Patch document is required.");
        if (patchDoc.Operations.Any(op => !IsAllowedPatchPath(op.path) || (op.from is not null && !IsAllowedPatchPath(op.from))))
            return BadRequestProblem("PATCH only supports StudentName, Email, PhoneNumber, EnrollmentDate, and Notes fields.");

        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == id, cancellationToken);
        if (student is null) return NotFound();

        patchDoc.ApplyTo(student, ModelState);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // Normalise first: a valid number becomes canonical E.164; an invalid one stays as typed
        // so the validators below reject it.
        if (student.StudentName is not null) student.StudentName = student.StudentName.Trim();
        student.Email = NormalizeEmail(student.Email);
        student.PhoneNumber = PhoneNumberNormalizer.Normalize(student.PhoneNumber);
        student.Notes = NormalizeOptional(student.Notes);

        if (!TryValidateModel(student)) return ValidationProblem(ModelState);
        var patchValidation = await updateValidator.ValidateAsync(new UpdateStudentDto
        {
            StudentName = student.StudentName,
            Email = student.Email,
            PhoneNumber = student.PhoneNumber,
            EnrollmentDate = student.EnrollmentDate,
            Notes = student.Notes
        }, cancellationToken);
        if (!patchValidation.IsValid)
        {
            foreach (var error in patchValidation.Errors) ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ConflictProblem(DuplicateEmailMessage);
        }
        return NoContent();
    }

    /// <summary>Deletes a student. Blocked (409) while attendance or session records still reference them.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteStudent(int id, CancellationToken cancellationToken)
    {
        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == id, cancellationToken);
        if (student is null) return NotFound();

        var hasLinkedRecords = await context.Attendances.AnyAsync(a => a.StudentId == id, cancellationToken)
            || await context.HifzSessions.AnyAsync(s => s.StudentId == id, cancellationToken)
            || await context.RevisionSessions.AnyAsync(s => s.StudentId == id, cancellationToken);
        if (hasLinkedRecords)
            return ConflictProblem("This student has linked attendance or learning-session records and cannot be deleted. Set the student inactive instead.");

        context.Students.Remove(student);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static bool IsAllowedPatchPath(string path)
    {
        var field = path.TrimStart('/')
            .Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);
        return PatchableFields.Contains(field, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Escapes LIKE wildcards so user input is matched literally.</summary>
    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
