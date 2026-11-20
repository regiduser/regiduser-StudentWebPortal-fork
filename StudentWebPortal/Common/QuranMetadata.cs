using StudentWebPortal.Model.Entity.Enum;

namespace StudentWebPortal.Common;

/// <summary>
/// Static reference data for the Quran. Used to reject impossible verse numbers
/// (for example verse 50 of Al-Fatiha, which has only 7 verses).
/// </summary>
public static class QuranMetadata
{
    public const int SurahCount = 114;
    public const int TotalVerses = 6236;

    // Index 0 = Surah 1 (Al-Fatiha) ... index 113 = Surah 114 (An-Nas).
    private static readonly int[] VerseCounts =
    [
        7, 286, 200, 176, 120, 165, 206, 75, 129, 109,
        123, 111, 43, 52, 99, 128, 111, 110, 98, 135,
        112, 78, 118, 64, 77, 227, 93, 88, 69, 60,
        34, 30, 73, 54, 45, 83, 182, 88, 75, 85,
        54, 53, 89, 59, 37, 35, 38, 29, 18, 45,
        60, 49, 62, 55, 78, 96, 29, 22, 24, 13,
        14, 11, 11, 18, 12, 12, 30, 52, 52, 44,
        28, 28, 20, 56, 40, 31, 50, 40, 46, 42,
        29, 19, 36, 25, 22, 17, 19, 26, 30, 20,
        15, 21, 11, 8, 8, 19, 5, 8, 8, 11,
        11, 8, 3, 9, 5, 4, 7, 3, 6, 3,
        5, 4, 5, 6
    ];

    /// <summary>Returns the number of verses in the surah, or null if the surah is not a valid value.</summary>
    public static int? GetVerseCount(Surahs surah)
    {
        var number = (int)surah;
        return number is >= 1 and <= SurahCount ? VerseCounts[number - 1] : null;
    }

    /// <summary>True when <paramref name="verse"/> exists in the given surah.</summary>
    public static bool IsValidVerse(Surahs surah, int verse) =>
        GetVerseCount(surah) is { } count && verse >= 1 && verse <= count;
}
