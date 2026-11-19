
using FluentValidation;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Validators.Common;

namespace StudentWebPortal.Validators.Student
{
    public class CreateStudentDtoValidator : AbstractValidator<StudentCreateDto>
    {
        public CreateStudentDtoValidator()
        {
            RuleFor(x => x.StudentName).NotEmpty().Must(name => name == null || name.Trim().Length > 0).WithMessage("Student name cannot be blank.");
            RuleFor(x => x.PhoneNumber)
                .ValidPhoneNumber();
        }
    }
}
