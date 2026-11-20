using FluentValidation;
using StudentWebPortal.Model.Dto;

namespace StudentWebPortal.Validators.Sessions;

public sealed class AttendanceCreateDtoValidator : AbstractValidator<AttendanceCreateDto>
{
    public AttendanceCreateDtoValidator()
    {
        RuleFor(x => x.AttendanceDate)
            .Must(date => date!.Value.Date <= DateTime.UtcNow.Date)
            .When(x => x.AttendanceDate.HasValue)
            .WithMessage("Attendance cannot be recorded for a future date.");
    }
}
