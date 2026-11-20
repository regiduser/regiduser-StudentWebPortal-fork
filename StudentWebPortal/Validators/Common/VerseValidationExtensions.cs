using FluentValidation;
using StudentWebPortal.Common;
using StudentWebPortal.Model.Entity.Enum;

namespace StudentWebPortal.Validators.Common;

public static class VerseValidationExtensions
{
    /// <summary>
    /// Verse number must exist in the selected surah (1..verse count). Skipped when the surah itself
    /// is invalid, because the surah rule already reports that.
    /// </summary>
    public static IRuleBuilderOptions<T, int> MustExistInSurah<T>(
        this IRuleBuilder<T, int> ruleBuilder,
        Func<T, Surahs?> surahSelector)
    {
        return ruleBuilder
            .Must((model, verse) =>
            {
                var surah = surahSelector(model);
                if (surah is null || QuranMetadata.GetVerseCount(surah.Value) is null) return true;
                return QuranMetadata.IsValidVerse(surah.Value, verse);
            })
            .WithMessage((model, verse) =>
            {
                var surah = surahSelector(model)!.Value;
                return $"Verse {verse} does not exist in {surah}; it has {QuranMetadata.GetVerseCount(surah)} verses.";
            });
    }
}
