using FluentValidation;
using StudentWebPortal.Model.Dto;
using StudentWebPortal.Model.Entity.Enum;
using StudentWebPortal.Validators.Common;

namespace StudentWebPortal.Validators.Sessions;

public sealed class HifzSessionCreateDtoValidator : AbstractValidator<HifzSessionCreateDto>
{
    public HifzSessionCreateDtoValidator()
    {
        RuleFor(x => x.Surah).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid Surah.");
        RuleFor(x => x.Status).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid session status.");
        RuleFor(x => x.Rank).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid rank.");
        RuleFor(x => x.RecordedBy).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid teacher.");
        RuleFor(x => x.VerseStart).MustExistInSurah(x => x.Surah);
    }
}


public sealed class HifzSessionUpdateDtoValidator : AbstractValidator<HifzSessionUpdateDto>
{
    public HifzSessionUpdateDtoValidator()
    {
        RuleFor(x => x.Surah).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid Surah.");
        RuleFor(x => x.Status).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid session status.");
        RuleFor(x => x.Rank).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid rank.");
        RuleFor(x => x.RecordedBy).Must(value => value.HasValue && Enum.IsDefined(value.Value)).WithMessage("Choose a valid teacher.");
        RuleFor(x => x.VerseStart).MustExistInSurah(x => x.Surah);
    }
}
