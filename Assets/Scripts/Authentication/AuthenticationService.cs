using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Alaala.Authentication
{
    public enum AuthenticationStatus
    {
        Success, InvalidCredentials, UsernameAlreadyTaken, InvalidInput,
        NetworkError, ServiceUnavailable, Registered, InvalidEmail
    }

    // Only real backend identifiers belong here. SDK tokens remain owned by the provider.
    // This object is memory-only and deliberately contains no password or token fields.
    public sealed class AuthenticationSession
    {
        public string Provider { get; }
        public string AccountId { get; }

        public AuthenticationSession(string provider, string accountId)
        {
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentException("A session requires a backend provider and account identifier.");
            Provider = provider;
            AccountId = accountId;
        }
    }

    public sealed class AuthenticationResult
    {
        public AuthenticationStatus Status { get; }
        public AuthenticationSession Session { get; }
        private AuthenticationResult(AuthenticationStatus status, AuthenticationSession session)
        { Status = status; Session = session; }

        public static AuthenticationResult Authenticated(AuthenticationSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return new AuthenticationResult(AuthenticationStatus.Success, session);
        }

        // Account creation is confirmed, but it is deliberately not an application login.
        public static AuthenticationResult Registered()
            => new AuthenticationResult(AuthenticationStatus.Registered, null);

        public static AuthenticationResult Failure(AuthenticationStatus status)
        {
            if (status == AuthenticationStatus.Success || status == AuthenticationStatus.Registered)
                throw new ArgumentException("Successful operations require their corresponding result factory.");
            return new AuthenticationResult(status, null);
        }
    }

    public interface IAuthenticationService
    {
        AuthenticationSession Session { get; }
        Task<AuthenticationResult> LoginAsync(string username, string password, CancellationToken cancellationToken);
        Task<AuthenticationResult> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken);
        void Logout();
    }

    // One application service survives scene changes; nothing invokes the legacy email-auth component.
    public static class AuthenticationServices
    {
        public static IAuthenticationService Current { get; private set; } = new PlayFabAuthenticationService();

        public static void Configure(IAuthenticationService provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            Current.Logout();
            Current = provider;
        }

        public static void Logout() => Current.Logout();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForLaunch()
        {
            Current?.Logout();
            Current = new PlayFabAuthenticationService();
        }
    }

    public sealed class UnconnectedAuthenticationService : IAuthenticationService
    {
        public AuthenticationSession Session => null;
        public Task<AuthenticationResult> LoginAsync(string username, string password, CancellationToken cancellationToken)
            => Unavailable(cancellationToken);
        public Task<AuthenticationResult> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken)
            => Unavailable(cancellationToken);
        public void Logout() { }

        private static Task<AuthenticationResult> Unavailable(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AuthenticationResult.Failure(AuthenticationStatus.ServiceUnavailable));
        }
    }
}
