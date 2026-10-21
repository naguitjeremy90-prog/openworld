using System;
using System.Net.Mail;

namespace Alaala.Authentication
{
    public enum AuthenticationField { None, Username, Email, Password, Confirmation }

    public readonly struct AuthenticationValidationResult
    {
        public AuthenticationField Field { get; }
        public string Message { get; }
        public bool IsValid => Field == AuthenticationField.None;
        public AuthenticationValidationResult(AuthenticationField field, string message)
        { Field = field; Message = message; }
    }

    public static class AuthenticationValidation
    {
        // Verified against RegisterPlayFabUser, Microsoft Learn (2026-10-22).
        // Keep in one place for future backend policy changes; backend remains authoritative.
        public const int UsernameMinimum = 3, UsernameMaximum = 20;
        public const int PasswordMinimum = 6, PasswordMaximum = 100;

        public static AuthenticationValidationResult Login(string username, string password)
        {
            var result = Username(username);
            return result.IsValid ? Password(password) : result;
        }

        public static AuthenticationValidationResult Register(string username, string email, string password, string confirmation)
        {
            var result = Username(username);
            if (!result.IsValid) return result;
            if (string.IsNullOrWhiteSpace(email)) return Invalid(AuthenticationField.Email, "Ilagay ang iyong e-liham.");
            try
            {
                var address = new MailAddress(email);
                if (!string.Equals(address.Address, email, StringComparison.Ordinal) || email.IndexOf('@') <= 0)
                    return Invalid(AuthenticationField.Email, "Hindi wasto ang e-liham.");
            }
            catch (FormatException) { return Invalid(AuthenticationField.Email, "Hindi wasto ang e-liham."); }
            result = Password(password);
            if (!result.IsValid) return result;
            if (string.IsNullOrEmpty(confirmation)) return Invalid(AuthenticationField.Confirmation, "Ulitin ang iyong hudyat.");
            return string.Equals(password, confirmation, StringComparison.Ordinal)
                ? default
                : Invalid(AuthenticationField.Confirmation, "Hindi magkapareho ang mga hudyat.");
        }

        private static AuthenticationValidationResult Username(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Invalid(AuthenticationField.Username, "Ilagay ang iyong pangalan.");
            return value.Length < UsernameMinimum || value.Length > UsernameMaximum
                ? Invalid(AuthenticationField.Username, "Ang pangalan ay dapat may 3 hanggang 20 karakter.") : default;
        }

        private static AuthenticationValidationResult Password(string value)
        {
            if (string.IsNullOrEmpty(value)) return Invalid(AuthenticationField.Password, "Ilagay ang iyong hudyat.");
            return value.Length < PasswordMinimum || value.Length > PasswordMaximum
                ? Invalid(AuthenticationField.Password, "Ang hudyat ay dapat may 6 hanggang 100 karakter.") : default;
        }

        private static AuthenticationValidationResult Invalid(AuthenticationField field, string message)
            => new AuthenticationValidationResult(field, message);
    }
}
