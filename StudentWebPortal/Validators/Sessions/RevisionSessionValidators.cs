using FluentValidation;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Model.Entity.Enum;


namespace StudentWebPortal.Validators.Sessions;


public sealed class RevisionSessionCreateDtoValidator : AbstractValidator<RevisionSessionCreateDto>
{
    public RevisionSessionCreateDtoValidator()
    {
        RuleFor(x => x.StudentId).GreaterThan(0);
        RuleFor(x => x.Surah).Must(value => value.HasValue && Enum.IsDefined(typeof(Surahs), value.Value)).WithMessage("Choose a valid Surah.");
        RuleFor(x => x.Status).Must(value => value.HasValue && Enum.IsDefined(typeof(SessionStatus), value.Value)).WithMessage("Choose a valid session status.");
        RuleFor(x => x.Rank).Must(value => value.HasValue && Enum.IsDefined(typeof(Ranks), value.Value)).WithMessage("Choose a valid rank.");
        RuleFor(x => x.RecordedBy).Must(value => value.HasValue && Enum.IsDefined(typeof(Teachers), value.Value)).WithMessage("Choose a valid teacher.");
        RuleFor(x => x.VerseEnd).GreaterThanOrEqualTo(x => x.VerseStart).WithMessage("VerseEnd must be greater than or equal to VerseStart.");
    }
}


public sealed class RevisionSessionUpdateDtoValidator : AbstractValidator<RevisionSessionUpdateDto>
{
    public RevisionSessionUpdateDtoValidator()
    {
        RuleFor(x => x.StudentId).GreaterThan(0);
        RuleFor(x => x.Surah).Must(value => value.HasValue && Enum.IsDefined(typeof(Surahs), value.Value)).WithMessage("Choose a valid Surah.");
        RuleFor(x => x.Status).Must(value => value.HasValue && Enum.IsDefined(typeof(SessionStatus), value.Value)).WithMessage("Choose a valid session status.");
        RuleFor(x => x.Rank).Must(value => value.HasValue && Enum.IsDefined(typeof(Ranks), value.Value)).WithMessage("Choose a valid rank.");
        RuleFor(x => x.RecordedBy).Must(value => value.HasValue && Enum.IsDefined(typeof(Teachers), value.Value)).WithMessage("Choose a valid teacher.");
        RuleFor(x => x.VerseEnd).GreaterThanOrEqualTo(x => x.VerseStart).WithMessage("VerseEnd must be greater than or equal to VerseStart.");
    }
}
