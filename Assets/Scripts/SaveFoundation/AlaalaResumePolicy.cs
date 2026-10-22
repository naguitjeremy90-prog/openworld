using System;
using System.Linq;

namespace Alaala.Saving
{
    public static class AlaalaResumePolicy
    {
        public const string MenuScene = "Assets/Scenes/SceneMenu.unity";
        public const string LoginScene = "Assets/Scenes/FinalLoginPage.unity";
        public static bool IsSafeMenuScene(string path, bool isLoaded) => isLoaded && (path == MenuScene || path == LoginScene);

        public static bool CanOfferContinue(LocalSaveResult result, AlaalaSaveCatalog catalog, AlaalaSaveOwner owner, out string message)
        {
            message = "Wala pang katugmang naitalang laro para sa account na ito.";
            if (result != null)
            {
                if (result.Status == LocalSaveStatus.CorruptPrimary) message = "Nasira o hindi mabasa ang mga save. Hindi sisimulan ang laro.";
                else if (result.Status == LocalSaveStatus.UnsupportedSchema) message = "Hindi tugma ang bersiyon ng naitalang laro.";
                else if (result.Status == LocalSaveStatus.AccountMismatch) message = "Hindi pag-aari ng account na ito ang save.";
                else if (result.Status == LocalSaveStatus.IoError) message = "Hindi mabuksan ang mga save file. Subukan muli.";
                else if (result.Status == LocalSaveStatus.OwnerChanged) message = "Nagbago ang account. Pumasok muli.";
            }
            if (result == null || !result.HasValidatedSave) return false;
            if (result.Status != LocalSaveStatus.ValidSave && result.Status != LocalSaveStatus.ValidBackup) return false;
            if (result.PrimaryStatus == LocalSaveStatus.ValidSave && result.BackupRevision > result.Revision)
            { message = "Hindi magkatugma ang pagkakasunod ng mga save. Kailangang suriin muna ang mga kopya."; return false; }
            foreach (var status in new[] { result.PrimaryStatus, result.BackupStatus })
                if (status != LocalSaveStatus.ValidSave && status != LocalSaveStatus.NoSave && status != LocalSaveStatus.CorruptPrimary)
                { message = "Hindi magamit ang tala. Kailangang suriin ang bersiyon o mga kopya nito."; return false; }
            if (!ValidateSnapshot(result.Snapshot, catalog, owner, out _))
            { message = "Hindi ligtas ipagpatuloy ang checkpoint na ito sa kasalukuyang laro."; return false; }
            message = result.Status == LocalSaveStatus.ValidBackup || result.BackupStatus == LocalSaveStatus.CorruptPrimary
                ? "May ligtas na kopya. Aayusin ang sirang kopya kapag pinili ang Magpatuloy." : "";
            return true;
        }

        public static bool ValidateSnapshot(AlaalaSaveSnapshot snapshot, AlaalaSaveCatalog catalog, AlaalaSaveOwner owner, out string error)
        {
            if (!AlaalaSaveValidation.Validate(snapshot, catalog, owner, out error)) return false;
            var state = new AlaalaStateView(snapshot.session);
            // ArrivalNavigationController.Start writes Finished in morning. Requiring the
            // already-authored completed value avoids inventing history during initialization.
            if (state.Int("draftworld_night_navigation_phase") != 4)
                return AlaalaSaveCatalog.Fail("Saved arrival navigation is not at its completed boundary.", out error);
            if (!state.Flag("inventory_tutorial_intro_seen") || !state.Flag("inventory_tutorial_first_item_seen"))
                return AlaalaSaveCatalog.Fail("Unfinished inventory teaching cannot be resumed at this boundary.", out error);
            return true;
        }

        internal static bool FreshMenu(out string error)
        {
            if (AlaalaSaveFoundation.RestorationInProgress || !SessionStoryState.IsEmptyForSaveRestore ||
                TaskManager.Instance != null && TaskManager.Instance.CaptureSaveTasks().Any(x => x.state != TaskState.Inactive) ||
                InventoryManager.Instance != null && InventoryManager.Instance.GetOwnedItems().Count > 0)
                return AlaalaSaveCatalog.Fail("Live-playthrough replacement is unsupported; use a fresh authenticated menu.", out error);
            return AlaalaSaveFoundation.IsStableEnvironment(out error);
        }
    }

    /// <summary>Shared menu/coordinator lease: stale and duplicate requests cannot publish.</summary>
    public sealed class AlaalaResumeAttempt
    {
        private readonly Func<bool> ownerIsCurrent;
        private long version;
        public bool Active { get; private set; }
        public AlaalaResumeAttempt(Func<bool> ownerIsCurrent) { this.ownerIsCurrent = ownerIsCurrent ?? throw new ArgumentNullException(nameof(ownerIsCurrent)); }
        public bool TryBegin(out long ticket)
        {
            ticket = version;
            if (Active || !ownerIsCurrent()) return false;
            Active = true; ticket = ++version; return true;
        }
        public bool IsCurrent(long ticket) => Active && ticket == version && ownerIsCurrent();
        public void End() { Active = false; ++version; }
    }
}
