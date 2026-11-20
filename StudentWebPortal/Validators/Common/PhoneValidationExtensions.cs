using FluentValidation;
using StudentWebPortal.Common;

namespace StudentWebPortal.Validators.Common
{
    public static class PhoneValidationExtensions
    {
        public static IRuleBuilderOptions<T, string?> ValidPhoneNumber<T>(
            this IRuleBuilder<T, string?> ruleBuilder,
            string defaultRegion = PhoneNumberNormalizer.DefaultRegion)
        {
            return ruleBuilder
                .Must(phone => BeAValidPhoneNumber(phone, defaultRegion))
                .WithMessage("Invalid phone number for the specified region.");
        }

        private static bool BeAValidPhoneNumber(string? phone, string defaultRegion)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return true; // optional field; required-ness is handled separately

            // Must be a real number AND fit the storage column once normalised.
            return PhoneNumberNormalizer.TryToE164(phone, defaultRegion, out var e164)
                && e164.Length <= PhoneNumberNormalizer.MaxStoredLength;
        }
    }
}
