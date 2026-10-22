using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alaala.Saving
{
    /// <summary>Fail-closed provenance, authorized only by the explicit production New Game hook.</summary>
    public static class AlaalaSaveRunContext
    {
        public static SaveRunKind Kind { get; private set; }
        public static string RunId { get; private set; }
        public static string SyntheticReason { get; private set; }
        private static AlaalaSaveOwner owner;
        private static Authentication.AuthenticationSession loginSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Kind = SaveRunKind.Unverified; RunId = null; SyntheticReason = null; owner = default; loginSession = null; }

        public static void MarkSynthetic(string reason)
        {
            Kind = SaveRunKind.Synthetic;
            SyntheticReason = string.IsNullOrWhiteSpace(reason) ? "Development scenario" : reason;
            RunId = RunId ?? Guid.NewGuid().ToString("N");
        }

        // New Game starts a new run. Continue uses TryAuthorizeResume to retain its saved run ID.
        public static bool TryAuthorizeGenuineRun(out string error)
        {
            error = null;
            if (!Application.isPlaying || Application.isEditor || Debug.isDebugBuild || Kind == SaveRunKind.Synthetic)
                return AlaalaSaveCatalog.Fail("Editor/development/unverified test runs cannot produce genuine saves.", out error);
            if (SceneManager.GetActiveScene().path != "Assets/Scenes/SceneMenu.unity" || StorySequenceCoordinator.IsStorySequenceActive)
                return AlaalaSaveCatalog.Fail("Run authorization requires an idle SceneMenu.", out error);
            if (!AlaalaSaveOwner.TryGetAuthenticated(out owner)) return AlaalaSaveCatalog.Fail("Verified PlayFab login required.", out error);
            loginSession = Authentication.AuthenticationServices.Current.Session;
            Kind = SaveRunKind.Genuine; RunId = Guid.NewGuid().ToString("N");
            return true;
        }

        internal static bool VerifyOwner(out AlaalaSaveOwner verified, out string error)
        {
            error = null; verified = default;
            if (Kind != SaveRunKind.Genuine || !Application.isPlaying || Application.isEditor || Debug.isDebugBuild ||
                !AlaalaSaveOwner.TryGetAuthenticated(out verified) || owner.TitleId != verified.TitleId || owner.AccountId != verified.AccountId ||
                !ReferenceEquals(loginSession, Authentication.AuthenticationServices.Current.Session))
                return AlaalaSaveCatalog.Fail("No genuine run for the current authenticated owner.", out error);
            return true;
        }

        internal static bool TryAuthorizeResume(AlaalaSaveSnapshot snapshot, AlaalaSaveCatalog catalog, out string error)
        {
            error = null;
            if (!Application.isPlaying || Application.isEditor || Debug.isDebugBuild || Kind == SaveRunKind.Synthetic ||
                SceneManager.GetActiveScene().path != AlaalaResumePolicy.MenuScene ||
                !AlaalaSaveOwner.TryGetAuthenticated(out var current))
                return AlaalaSaveCatalog.Fail("Fresh authenticated production SceneMenu required.", out error);
            if (!AlaalaResumePolicy.ValidateSnapshot(snapshot, catalog, current, out error) || !AlaalaResumePolicy.FreshMenu(out error)) return false;
            owner = current; loginSession = Authentication.AuthenticationServices.Current.Session;
            RunId = snapshot.runId; Kind = SaveRunKind.Genuine; // Continue the saved run, never manufacture a new one.
            return true;
        }

        internal static void InvalidateRun()
        {
            if (Kind != SaveRunKind.Synthetic) Kind = SaveRunKind.Unverified;
            owner = default; loginSession = null; RunId = null;
        }
    }
}
