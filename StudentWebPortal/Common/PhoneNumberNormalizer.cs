using PhoneNumbers;

namespace StudentWebPortal.Common;

/// <summary>Stores phone numbers in one canonical international (E.164) format, e.g. +919876543210.</summary>
public static class PhoneNumberNormalizer
{
    public const string DefaultRegion = "IN";
    public const int MaxStoredLength = 15; // matches the Student.PhoneNumber column length

    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    public static string? Normalize(string? phone, string defaultRegion = DefaultRegion)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var trimmed = phone.Trim();
        return TryToE164(trimmed, defaultRegion, out var e164) ? e164 : trimmed;
    }

    public static bool TryToE164(string phone, string defaultRegion, out string e164)
    {
        e164 = string.Empty;
        try
        {
            var parsed = PhoneUtil.Parse(phone, defaultRegion);
            if (!PhoneUtil.IsValidNumber(parsed)) return false;
            e164 = PhoneUtil.Format(parsed, PhoneNumberFormat.E164);
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }
}
