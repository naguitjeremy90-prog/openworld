using System;
using System.Threading;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace Alaala.Authentication
{
    public sealed class PlayFabAuthenticationService : IAuthenticationService
    {
        private sealed class Operation
        {
            public int Generation;
            public bool Cancelled;
            public readonly PlayFabAuthenticationContext Context = new PlayFabAuthenticationContext();
            public readonly TaskCompletionSource<AuthenticationResult> Completion =
                new TaskCompletionSource<AuthenticationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private readonly object gate = new object();
        private Operation active;
        private int generation;
        private AuthenticationSession session;
        private Operation sessionOwner;

        public AuthenticationSession Session
        {
            get
            {
                lock (gate)
                    return session != null && PlayFabClientAPI.IsClientLoggedIn() &&
                        PlayFabSettings.staticPlayer.PlayFabId == session.AccountId ? session : null;
            }
        }

        public Task<AuthenticationResult> LoginAsync(string username, string password, CancellationToken cancellationToken)
        {
            if (!AuthenticationValidation.Login(username, password).IsValid)
                return Task.FromResult(AuthenticationResult.Failure(AuthenticationStatus.InvalidInput));
            return ExecuteAsync(cancellationToken, operation =>
            {
                var request = new LoginWithPlayFabRequest
                {
                    Username = username, Password = password, AuthenticationContext = operation.Context
                    // The installed SDK supplies TitleId from PlayFabSettings.staticSettings.
                };
                PlayFabClientAPI.LoginWithPlayFab(request, result =>
                {
                    request.Password = null;
                    Complete(operation, () =>
                    {
                        if (result == null || string.IsNullOrWhiteSpace(result.PlayFabId) ||
                            !operation.Context.IsClientLoggedIn() || operation.Context.PlayFabId != result.PlayFabId)
                            return AuthenticationResult.Failure(AuthenticationStatus.ServiceUnavailable);
                        // Only a current, explicit LoginWithPlayFab success can publish SDK credentials.
                        PlayFabSettings.staticPlayer.CopyFrom(operation.Context);
                        session = new AuthenticationSession("PlayFab", result.PlayFabId);
                        sessionOwner = operation;
                        return AuthenticationResult.Authenticated(session);
                    });
                    result?.AuthenticationContext?.ForgetAllCredentials();
                }, error =>
                {
                    request.Password = null;
                    Complete(operation, () => AuthenticationResult.Failure(MapError(error, false)));
                });
            });
        }

        public Task<AuthenticationResult> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken)
        {
            var validation = AuthenticationValidation.Register(username, email, password, password);
            if (!validation.IsValid)
                return Task.FromResult(AuthenticationResult.Failure(validation.Field == AuthenticationField.Email
                    ? AuthenticationStatus.InvalidEmail : AuthenticationStatus.InvalidInput));
            return ExecuteAsync(cancellationToken, operation =>
            {
                var request = new RegisterPlayFabUserRequest
                {
                    Username = username, Email = email, Password = password,
                    RequireBothUsernameAndEmail = true, AuthenticationContext = operation.Context
                };
                PlayFabClientAPI.RegisterPlayFabUser(request, result =>
                {
                    request.Password = null;
                    // Registration implicitly receives SDK credentials. Never publish them to staticPlayer.
                    Complete(operation, () => result != null && !string.IsNullOrWhiteSpace(result.PlayFabId)
                        ? AuthenticationResult.Registered()
                        : AuthenticationResult.Failure(AuthenticationStatus.ServiceUnavailable));
                    result?.AuthenticationContext?.ForgetAllCredentials();
                }, error =>
                {
                    request.Password = null;
                    Complete(operation, () => AuthenticationResult.Failure(MapError(error, true)));
                });
            });
        }

        private async Task<AuthenticationResult> ExecuteAsync(CancellationToken token, Action<Operation> dispatch)
        {
            token.ThrowIfCancellationRequested();
            // Authoring/static validation must never send credentials or create accounts in Edit Mode.
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(PlayFabSettings.TitleId))
                return AuthenticationResult.Failure(AuthenticationStatus.ServiceUnavailable);
            Operation operation;
            lock (gate)
            {
                if (active != null) return AuthenticationResult.Failure(AuthenticationStatus.ServiceUnavailable);
                ClearSession();
                operation = new Operation { Generation = ++generation };
                active = operation;
            }
            using (token.Register(() => Cancel(operation)))
            {
                try
                {
                    // Cancellation before dispatch must not create an outgoing request.
                    if (!token.IsCancellationRequested) dispatch(operation);
                }
                catch (Exception)
                {
                    // SDK exceptions can contain request details; never log or expose them.
                    Complete(operation, () => AuthenticationResult.Failure(AuthenticationStatus.ServiceUnavailable));
                }
                dispatch = null;
                return await operation.Completion.Task;
            }
        }

        private void Complete(Operation operation, Func<AuthenticationResult> result)
        {
            lock (gate)
            {
                if (active != operation || operation.Cancelled || operation.Generation != generation)
                {
                    operation.Context.ForgetAllCredentials();
                    operation.Completion.TrySetCanceled();
                    return;
                }
                AuthenticationResult response = result();
                operation.Context.ForgetAllCredentials();
                active = null;
                operation.Completion.TrySetResult(response);
            }
        }

        private void Cancel(Operation operation)
        {
            lock (gate)
            {
                operation.Cancelled = true;
                operation.Context.ForgetAllCredentials();
                if (active == operation) active = null;
                if (sessionOwner == operation) ClearSession();
                operation.Completion.TrySetCanceled();
            }
        }

        public void Logout()
        {
            lock (gate)
            {
                generation++;
                if (active != null) Cancel(active);
                ClearSession();
            }
        }

        private void ClearSession()
        {
            // A superseded provider must not erase credentials later owned by another provider.
            if (session != null && PlayFabSettings.staticPlayer.PlayFabId == session.AccountId)
                PlayFabClientAPI.ForgetAllCredentials();
            session = null;
            sessionOwner = null;
        }

        internal static AuthenticationStatus MapError(PlayFabError error, bool registration)
        {
            if (error == null) return AuthenticationStatus.ServiceUnavailable;
            switch (error.Error)
            {
                case PlayFabErrorCode.ConnectionError: return AuthenticationStatus.NetworkError;
                case PlayFabErrorCode.UsernameNotAvailable:
                case PlayFabErrorCode.EmailAddressNotAvailable: return AuthenticationStatus.UsernameAlreadyTaken;
                case PlayFabErrorCode.InvalidEmailAddress: return AuthenticationStatus.InvalidEmail;
                case PlayFabErrorCode.AccountNotFound:
                case PlayFabErrorCode.InvalidUsernameOrPassword:
                case PlayFabErrorCode.AccountBanned: return AuthenticationStatus.InvalidCredentials;
                case PlayFabErrorCode.InvalidUsername:
                case PlayFabErrorCode.InvalidPassword:
                    return registration ? AuthenticationStatus.InvalidInput : AuthenticationStatus.InvalidCredentials;
                case PlayFabErrorCode.InvalidParams:
                    return error.ErrorDetails != null && error.ErrorDetails.ContainsKey("Email")
                        ? AuthenticationStatus.InvalidEmail : AuthenticationStatus.InvalidInput;
                default:
                    return error.HttpCode == 408 || error.HttpCode == 504
                        ? AuthenticationStatus.NetworkError : AuthenticationStatus.ServiceUnavailable;
            }
        }
    }
}
