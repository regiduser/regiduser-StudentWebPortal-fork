using Microsoft.AspNetCore.JsonPatch;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentWebPortal.Data;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Model.Entity;
using Npgsql;


namespace StudentWebPortal.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StudentsController(StudentWebPortalContext context, IValidator<UpdateStudentDto> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Student>>> GetAllStudents(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || page > 1_000_000 || pageSize < 1 || pageSize > 100)
            return BadRequest("Page must be between 1 and 1,000,000 and pageSize must be between 1 and 100.");

        var query = context.Students.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.StudentName.Contains(term) || (s.Email != null && s.Email.Contains(term)));
        }
        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        Response.Headers["X-Total-Count"] = totalCount.ToString();
        Response.Headers["X-Page"] = page.ToString();
        Response.Headers["X-Page-Size"] = pageSize.ToString();

        var students = await query.OrderBy(s => s.StudentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return Ok(students);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Student>> GetById(int id, CancellationToken cancellationToken)
    {
        var student = await context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == id, cancellationToken);
        return student is null ? NotFound() : Ok(student);
    }

    [HttpPost]
    public async Task<ActionResult<Student>> Create([FromBody] StudentCreateDto dto, CancellationToken cancellationToken)
    {
        var student = new Student
        {
            StudentName = dto.StudentName.Trim(),
            Email = NormalizeEmail(dto.Email),
            PhoneNumber = NormalizeOptional(dto.PhoneNumber),
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
            return Conflict(new ProblemDetails { Title = "A student with this email already exists.", Status = StatusCodes.Status409Conflict });
        }

        return CreatedAtAction(nameof(GetById), new { id = student.StudentId }, student);
    }


    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateStudent(int id, [FromBody] UpdateStudentDto dto, CancellationToken cancellationToken)
    {
        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == id, cancellationToken);
        if (student is null) return NotFound();

        // PUT accepts the current update DTO; omitted values remain unchanged for backward compatibility.
        if (dto.StudentName is not null) student.StudentName = dto.StudentName.Trim();
        if (dto.Email is not null) student.Email = NormalizeEmail(dto.Email);
        if (dto.PhoneNumber is not null) student.PhoneNumber = NormalizeOptional(dto.PhoneNumber);
        if (dto.EnrollmentDate.HasValue) student.EnrollmentDate = dto.EnrollmentDate.Value;
        if (dto.Notes is not null) student.Notes = NormalizeOptional(dto.Notes);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict(new ProblemDetails { Title = "A student with this email already exists.", Status = StatusCodes.Status409Conflict });
        }
        return NoContent();
    }


    [HttpPatch("{id:int}")]
    public async Task<IActionResult> PatchStudent(int id, [FromBody] JsonPatchDocument<Student> patchDoc, CancellationToken cancellationToken)
    {
        if (patchDoc is null) return BadRequest("A JSON Patch document is required.");
        if (patchDoc.Operations.Any(op => !IsAllowedPatchPath(op.path) || (op.from is not null && !IsAllowedPatchPath(op.from))))
            return BadRequest("PATCH only supports StudentName, Email, PhoneNumber, EnrollmentDate, and Notes fields.");

        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == id, cancellationToken);
        if (student is null) return NotFound();

        patchDoc.ApplyTo(student, ModelState);
        if (!ModelState.IsValid || !TryValidateModel(student)) return ValidationProblem(ModelState);
        if (student.StudentName is not null) student.StudentName = student.StudentName.Trim();
        student.Email = NormalizeEmail(student.Email);
        student.PhoneNumber = NormalizeOptional(student.PhoneNumber);
        student.Notes = NormalizeOptional(student.Notes);
        var patchValidation = await updateValidator.ValidateAsync(new UpdateStudentDto
        {
            StudentName = student.StudentName, Email = student.Email, PhoneNumber = student.PhoneNumber,
            EnrollmentDate = student.EnrollmentDate, Notes = student.Notes
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
            return Conflict(new ProblemDetails { Title = "A student with this email already exists.", Status = StatusCodes.Status409Conflict });
        }
        return NoContent();
    }


    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteStudent(int id, CancellationToken cancellationToken)
    {
        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == id, cancellationToken);
        if (student is null) return NotFound();
        var hasLinkedRecords = await context.Attendances.AnyAsync(a => a.StudentId == id, cancellationToken)
            || await context.HifzSessions.AnyAsync(s => s.StudentId == id, cancellationToken)
            || await context.RevisionSessions.AnyAsync(s => s.StudentId == id, cancellationToken);
        if (hasLinkedRecords)
            return Conflict("This student has linked attendance or learning-session records and cannot be deleted.");
        context.Students.Remove(student);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }


    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static bool IsAllowedPatchPath(string path) => new[] { "StudentName", "Email", "PhoneNumber", "EnrollmentDate", "Notes" }.Contains(path.TrimStart('/').Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal), StringComparer.OrdinalIgnoreCase);
    private static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
}
