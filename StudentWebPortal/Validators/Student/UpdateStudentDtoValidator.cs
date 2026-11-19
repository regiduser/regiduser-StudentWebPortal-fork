using FluentValidation;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Validators.Common;

namespace StudentWebPortal.Validators.Student
{
    public class UpdateStudentDtoValidator : AbstractValidator<UpdateStudentDto>
    {
        public UpdateStudentDtoValidator()
        {
            RuleFor(x => x.StudentName).Must(name => name == null || name.Trim().Length > 0).WithMessage("Student name cannot be blank.");
            RuleFor(x => x.PhoneNumber)
                .ValidPhoneNumber()
                .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
        }
    }
}
