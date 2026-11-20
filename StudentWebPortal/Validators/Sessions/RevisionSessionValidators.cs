using FluentValidation;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Model.Entity.Enum;
using StudentWebPortal.Validators.Common;

namespace StudentWebPortal.Validators.Sessions;

public sealed class RevisionSessionCreateDtoValidator : AbstractValidator<RevisionSessionCreateDto>
{
    public RevisionSessionCreateDtoValidator()
    {
        RuleFor(x => x.Surah).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid Surah.");
        RuleFor(x => x.Status).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid session status.");
        RuleFor(x => x.Rank).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid rank.");
        RuleFor(x => x.RecordedBy).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid teacher.");
        RuleFor(x => x.VerseStart).MustExistInSurah(x => x.Surah);
        RuleFor(x => x.VerseEnd).MustExistInSurah(x => x.Surah);
        RuleFor(x => x.VerseEnd).GreaterThanOrEqualTo(x => x.VerseStart).WithMessage("VerseEnd must be greater than or equal to VerseStart.");
    }
}


public sealed class RevisionSessionUpdateDtoValidator : AbstractValidator<RevisionSessionUpdateDto>
{
    public RevisionSessionUpdateDtoValidator()
    {
        RuleFor(x => x.Surah).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid Surah.");
        RuleFor(x => x.Status).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid session status.");
        RuleFor(x => x.Rank).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid rank.");
        RuleFor(x => x.RecordedBy).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid teacher.");
        RuleFor(x => x.VerseStart).MustExistInSurah(x => x.Surah);
        RuleFor(x => x.VerseEnd).MustExistInSurah(x => x.Surah);
        RuleFor(x => x.VerseEnd).GreaterThanOrEqualTo(x => x.VerseStart).WithMessage("VerseEnd must be greater than or equal to VerseStart.");
    }
}
