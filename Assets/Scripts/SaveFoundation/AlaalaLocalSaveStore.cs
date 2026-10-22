using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Alaala.Saving
{
    public enum LocalSaveStatus
    {
        ValidSave, NoSave, CorruptPrimary, ValidBackup, UnsupportedSchema, AccountMismatch,
        IoError, InvalidSnapshot, OwnerChanged, UnsafeCheckpoint, Committed, Unchanged,
        RecoveryRequired, Recovered, RevisionConflict, NotInspected
    }

    public sealed class LocalSaveResult
    {
        public LocalSaveStatus Status { get; internal set; }
        public LocalSaveStatus PrimaryStatus { get; internal set; } = LocalSaveStatus.NotInspected;
        public LocalSaveStatus BackupStatus { get; internal set; } = LocalSaveStatus.NotInspected;
        public long Revision => Snapshot?.saveRevision ?? 0;
        public long BackupRevision { get; internal set; }
        // Detached validated data only. Reading this never imports state or loads a scene.
        public AlaalaSaveSnapshot Snapshot { get; internal set; }
        public string Diagnostic { get; internal set; }
        public bool HasValidatedSave => Snapshot != null;
    }

    /// <summary>
    /// Windows local transactions. Invoke on Unity's main thread: validation/capture use
    /// Unity assets. Async file writes yield; continuations retain Unity's synchronization
    /// context. One gate serializes all store instances, including discovery/recovery.
    /// </summary>
    public sealed class AlaalaLocalSaveStore
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        // Call only after revoking the run/session and suspending autosave. This drains
        // earlier transactions (including temporary-file cleanup), without reading or writing saves.
        internal static async Task WaitForIdleAsync()
        {
            await Gate.WaitAsync();
            Gate.Release();
        }
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private const long MaximumBytes = 16 * 1024 * 1024;
        private readonly string directory;
        private readonly AlaalaSaveCatalog catalog;
        private readonly AlaalaSaveOwner owner;
        private readonly Func<bool> ownerIsCurrent, runIsCurrent;
        private readonly bool isolatedTest;
        private readonly int mainThread;
        private string Primary => Path.Combine(directory, "checkpoint.json");
        private string Backup => Path.Combine(directory, "checkpoint.backup.json");

        private AlaalaLocalSaveStore(string root, AlaalaSaveCatalog catalog, AlaalaSaveOwner owner,
            Func<bool> ownerIsCurrent, Func<bool> runIsCurrent, bool isolatedTest)
        {
            this.catalog = catalog; this.owner = owner; this.ownerIsCurrent = ownerIsCurrent;
            this.runIsCurrent = runIsCurrent; this.isolatedTest = isolatedTest;
            mainThread = Thread.CurrentThread.ManagedThreadId;
            directory = Path.Combine(Path.GetFullPath(root), Hash(owner.TitleId), Hash(owner.AccountId));
        }

        public static bool TryOpenAuthenticated(AlaalaSaveCatalog catalog, out AlaalaLocalSaveStore store, out string error)
        {
            store = null; error = null;
            if (!Application.isPlaying || Application.isEditor || Debug.isDebugBuild ||
                !AlaalaSaveOwner.TryGetAuthenticated(out var owner))
                return AlaalaSaveCatalog.Fail("Production PlayFab login required for player storage.", out error);
            if (catalog == null) return AlaalaSaveCatalog.Fail("Missing save catalog.", out error);
            if (!catalog.Validate(out error)) return false;
            var login = Authentication.AuthenticationServices.Current.Session;
            string run = AlaalaSaveRunContext.RunId;
            bool OwnerCurrent() => ReferenceEquals(login, Authentication.AuthenticationServices.Current.Session) &&
                AlaalaSaveOwner.TryGetAuthenticated(out var now) && now.TitleId == owner.TitleId && now.AccountId == owner.AccountId;
            bool RunCurrent() => run == AlaalaSaveRunContext.RunId && AlaalaSaveRunContext.VerifyOwner(out _, out _);
            store = new AlaalaLocalSaveStore(Path.Combine(Application.persistentDataPath, "AlaalaSaves"), catalog, owner, OwnerCurrent, RunCurrent, false);
            return true;
        }

        public Task<LocalSaveResult> SaveCheckpointAsync(string checkpointId, CancellationToken cancellation = default)
            => SaveAsync(revision => AlaalaSaveFoundation.TryCapture(checkpointId, revision, catalog,
                TaskManager.Instance, InventoryManager.Instance, out var snapshot, out var error)
                ? snapshot : throw new CaptureRejectedException(error), captured =>
                !cancellation.IsCancellationRequested && !AlaalaSaveFoundation.RestorationInProgress &&
                AlaalaSaveFoundation.TryCapture(checkpointId, captured.saveRevision, catalog, TaskManager.Instance,
                    InventoryManager.Instance, out var fresh, out _) && Equivalent(captured, fresh));

        public Task<LocalSaveResult> InspectSaveAvailabilityAsync() => ReadValidatedSnapshotAsync();

        public async Task<LocalSaveResult> ReadValidatedSnapshotAsync()
        {
            AssertThread();
            await Gate.WaitAsync();
            try
            {
                if (!ownerIsCurrent()) return Result(LocalSaveStatus.OwnerChanged);
                CheckPath(false);
                using var lease = AcquireProcessLock(false);
                var result = Inspect();
                return ownerIsCurrent() ? result : Result(LocalSaveStatus.OwnerChanged);
            }
            catch (Exception exception) when (IsStorageException(exception)) { return IoFailure(exception); }
            finally { Gate.Release(); }
        }

        private async Task<LocalSaveResult> SaveAsync(Func<long, AlaalaSaveSnapshot> capture, Func<AlaalaSaveSnapshot, bool> stillEligible = null)
        {
            AssertThread();
            await Gate.WaitAsync();
            string temporary = null;
            try
            {
                if (!CanWrite()) return Result(LocalSaveStatus.OwnerChanged);
                CheckPath(true);
                using var lease = AcquireProcessLock(true);
                var existing = Inspect();
                // Corruption, mismatched ownership and future formats require explicit action.
                if (existing.PrimaryStatus != LocalSaveStatus.ValidSave && existing.PrimaryStatus != LocalSaveStatus.NoSave)
                    return Result(LocalSaveStatus.RecoveryRequired, "Resolve the primary before saving; existing copies were preserved.");
                if (existing.BackupStatus != LocalSaveStatus.ValidSave && existing.BackupStatus != LocalSaveStatus.NoSave)
                    return Result(LocalSaveStatus.RecoveryRequired, "Resolve the backup before saving; existing copies were preserved.");
                var previousBackup = Read(Backup);
                if (existing.PrimaryStatus == LocalSaveStatus.ValidSave && previousBackup.Status == LocalSaveStatus.ValidSave && previousBackup.Revision > existing.Revision)
                    return Result(LocalSaveStatus.RecoveryRequired, "Backup is newer than the primary; refusing to discard its history.");
                long latest = Math.Max(existing.Revision, previousBackup.Revision);
                var snapshot = capture(checked(latest + 1)); // Capture AFTER acquiring the transaction gate.
                if (snapshot == null) return Result(LocalSaveStatus.InvalidSnapshot, "Capture returned no snapshot.");
                if (!Validate(snapshot, out _, out var error)) return Result(LocalSaveStatus.InvalidSnapshot, error);
                if (snapshot.saveRevision != latest + 1) return Result(LocalSaveStatus.RevisionConflict);
                if (existing.Snapshot != null && existing.Snapshot.runId != snapshot.runId)
                    return Result(LocalSaveStatus.RecoveryRequired, "A different saved run is preserved. Explicit overwrite authorization is required; no confirmation UI is connected.");
                snapshot = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(snapshot));
                AlaalaSaveFoundation.Canonicalize(snapshot);
                if (existing.Snapshot != null)
                {
                    var previous = existing.Snapshot;
                    AlaalaSaveFoundation.Canonicalize(previous);
                    if (!AllowsProgression(previous, snapshot)) return Result(LocalSaveStatus.RevisionConflict, "Same-run task/history regression rejected.");
                    long nextRevision = snapshot.saveRevision; snapshot.saveRevision = previous.saveRevision;
                    bool unchanged = JsonUtility.ToJson(snapshot) == JsonUtility.ToJson(previous);
                    snapshot.saveRevision = nextRevision;
                    if (unchanged) return Result(LocalSaveStatus.Unchanged, snapshot: previous);
                }
                CheckPath(true);
                temporary = Path.Combine(directory, "transaction-" + Guid.NewGuid().ToString("N") + ".pending");
                byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(snapshot));
                if (bytes.LongLength > MaximumBytes) return Result(LocalSaveStatus.InvalidSnapshot, "Snapshot exceeds the local size limit.");
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                var verified = Read(temporary);
                if (verified.Status != LocalSaveStatus.ValidSave || JsonUtility.ToJson(verified.Snapshot) != JsonUtility.ToJson(snapshot))
                    return Result(LocalSaveStatus.InvalidSnapshot, "Temporary file failed round-trip validation.");
#if UNITY_EDITOR
                EditorTransactionProbe?.Invoke("BeforeCommit");
#endif
                // No await/callback occurs between this authorization and atomic rename.
                if (!CanWrite()) return Result(LocalSaveStatus.OwnerChanged);
                if (stillEligible != null && !stillEligible(snapshot)) return Result(LocalSaveStatus.UnsafeCheckpoint, "Checkpoint changed while writing; deferred for a fresh capture.");
                CheckPath(false);
                var current = Inspect();
                if (current.PrimaryStatus != existing.PrimaryStatus || current.BackupStatus != existing.BackupStatus ||
                    JsonUtility.ToJson(current.Snapshot) != JsonUtility.ToJson(existing.Snapshot))
                    return Result(LocalSaveStatus.RevisionConflict, "Committed files changed while writing; existing copies were preserved.");
                if (File.Exists(Primary))
                    File.Replace(temporary, Primary, Backup); // Same-volume Windows atomic replacement + previous-primary backup.
                else
                {
                    // Preserve any existing valid recovery copy. A first save gets a flushed backup
                    // before publishing the primary, so a failed first rename is recoverable.
                    if (!File.Exists(Backup))
                    {
                        string backupTemp = temporary + ".backup";
                        try { WriteFlushed(backupTemp, bytes); File.Move(backupTemp, Backup); }
                        finally { DeleteTemporary(backupTemp); }
                    }
                    File.Move(temporary, Primary);
                }
                temporary = null;
                var committed = Read(Primary);
                if (committed.Status != LocalSaveStatus.ValidSave || committed.Revision != snapshot.saveRevision)
                    return Result(LocalSaveStatus.IoError, "Commit verification failed; inspect primary/backup before retrying.");
                return Result(LocalSaveStatus.Committed, snapshot: committed.Snapshot);
            }
            catch (CaptureRejectedException exception) { return Result(LocalSaveStatus.UnsafeCheckpoint, exception.Message); }
            catch (ArgumentException) { return Result(LocalSaveStatus.InvalidSnapshot, "Malformed snapshot text/serialization."); }
            catch (Exception exception) when (IsStorageException(exception) || exception is OverflowException) { return IoFailure(exception); }
            finally { DeleteTemporary(temporary); Gate.Release(); }
        }

        /// <summary>Explicit recovery only. The invalid primary is retained as a quarantine file.</summary>
        public async Task<LocalSaveResult> RecoverBackupAsync()
        {
            AssertThread(); await Gate.WaitAsync(); string temporary = null;
            try
            {
                if (!ownerIsCurrent()) return Result(LocalSaveStatus.OwnerChanged);
                CheckPath(false);
                using var lease = AcquireProcessLock(false);
                var primary = Read(Primary); var backup = Read(Backup);
                if (primary.Status == LocalSaveStatus.ValidSave)
                {
                    if (backup.Status == LocalSaveStatus.ValidSave)
                        return backup.Revision > primary.Revision ? Result(LocalSaveStatus.RecoveryRequired, "Newer backup conflict requires explicit resolution.") :
                            Result(LocalSaveStatus.ValidSave, "Primary already valid; recovery would not downgrade it.", primary.Snapshot);
                    if (backup.Status != LocalSaveStatus.NoSave && backup.Status != LocalSaveStatus.CorruptPrimary)
                        return Result(LocalSaveStatus.RecoveryRequired, "Backup needs explicit migration/ownership/I/O resolution.");
                    temporary = Path.Combine(directory, "backup-repair-" + Guid.NewGuid().ToString("N") + ".pending");
                    WriteFlushed(temporary, Utf8.GetBytes(JsonUtility.ToJson(primary.Snapshot)));
                    if (Read(temporary).Status != LocalSaveStatus.ValidSave) return Result(LocalSaveStatus.InvalidSnapshot);
                    if (!ownerIsCurrent()) return Result(LocalSaveStatus.OwnerChanged);
                    CheckPath(false);
                    if (File.Exists(Backup)) File.Replace(temporary, Backup, Path.Combine(directory, "quarantine-backup-" + Guid.NewGuid().ToString("N") + ".json"));
                    else File.Move(temporary, Backup);
                    temporary = null;
                    return Read(Backup).Status == LocalSaveStatus.ValidSave ? Result(LocalSaveStatus.Recovered, snapshot: primary.Snapshot) : Result(LocalSaveStatus.IoError);
                }
                if (primary.Status == LocalSaveStatus.AccountMismatch || primary.Status == LocalSaveStatus.UnsupportedSchema || primary.Status == LocalSaveStatus.IoError)
                    return Result(LocalSaveStatus.RecoveryRequired, "Primary needs explicit migration/ownership/I/O resolution.");
                if (backup.Status != LocalSaveStatus.ValidSave) return backup;
                CheckPath(true);
                temporary = Path.Combine(directory, "recovery-" + Guid.NewGuid().ToString("N") + ".pending");
                WriteFlushed(temporary, Utf8.GetBytes(JsonUtility.ToJson(backup.Snapshot)));
                if (Read(temporary).Status != LocalSaveStatus.ValidSave) return Result(LocalSaveStatus.InvalidSnapshot);
                if (!ownerIsCurrent()) return Result(LocalSaveStatus.OwnerChanged);
                CheckPath(false);
                if (File.Exists(Primary)) File.Replace(temporary, Primary, Path.Combine(directory, "quarantine-" + Guid.NewGuid().ToString("N") + ".json"));
                else File.Move(temporary, Primary);
                temporary = null;
                var result = Read(Primary);
                return result.Status == LocalSaveStatus.ValidSave ? Result(LocalSaveStatus.Recovered, snapshot: result.Snapshot) : result;
            }
            catch (Exception exception) when (IsStorageException(exception)) { return IoFailure(exception); }
            finally { DeleteTemporary(temporary); Gate.Release(); }
        }

        private LocalSaveResult Inspect()
        {
            var primary = Read(Primary); var backup = Read(Backup);
            var result = primary.Status == LocalSaveStatus.ValidSave ? Result(LocalSaveStatus.ValidSave, snapshot: primary.Snapshot) :
                backup.Status == LocalSaveStatus.ValidSave ? Result(LocalSaveStatus.ValidBackup, "Validated backup available; no recovery was performed.", backup.Snapshot) :
                Result(primary.Status == LocalSaveStatus.NoSave ? backup.Status : primary.Status, primary.Diagnostic ?? backup.Diagnostic);
            result.PrimaryStatus = primary.Status; result.BackupStatus = backup.Status;
            result.BackupRevision = backup.Revision;
            return result;
        }

        private LocalSaveResult Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return Result(LocalSaveStatus.NoSave);
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return Result(LocalSaveStatus.IoError, "Linked save files are unsupported.");
                var length = new FileInfo(path).Length;
                if (length < 2 || length > MaximumBytes) return Result(LocalSaveStatus.CorruptPrimary, "Invalid file size.");
                var json = Utf8.GetString(File.ReadAllBytes(path));
                var save = JsonUtility.FromJson<AlaalaSaveSnapshot>(json);
                if (!Validate(save, out var status, out var error)) return Result(status, error);
                return Result(LocalSaveStatus.ValidSave, snapshot: save);
            }
            catch (ArgumentException) { return Result(LocalSaveStatus.CorruptPrimary, "Invalid JSON/UTF-8 data."); }
            catch (Exception exception) when (IsStorageException(exception)) { return IoFailure(exception); }
        }

        private bool Validate(AlaalaSaveSnapshot save, out LocalSaveStatus status, out string error)
        {
            status = LocalSaveStatus.CorruptPrimary; error = null;
            if (save == null) return false;
            if (save.schemaVersion != AlaalaSaveSchema.Version || save.contentVersion != AlaalaSaveSchema.ContentVersion)
            { status = LocalSaveStatus.UnsupportedSchema; error = "Unsupported schema/content version."; return false; }
            if (save.titleId != owner.TitleId || save.playFabAccountId != owner.AccountId)
            { status = LocalSaveStatus.AccountMismatch; error = "Account/title mismatch."; return false; }
            return AlaalaSaveValidation.Validate(save, catalog, owner, out error);
        }

        private bool CanWrite() => ownerIsCurrent() && runIsCurrent();
        private FileStream AcquireProcessLock(bool create)
        {
            if (!Directory.Exists(directory) && !create) return null;
            string path = Path.Combine(directory, "transaction.lock");
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked lock file is unsupported.");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        private static bool Equivalent(AlaalaSaveSnapshot left, AlaalaSaveSnapshot right)
        { AlaalaSaveFoundation.Canonicalize(right); return JsonUtility.ToJson(left) == JsonUtility.ToJson(right); }
        private void AssertThread()
        { if (Thread.CurrentThread.ManagedThreadId != mainThread) throw new InvalidOperationException("Use the Unity main thread for save operations."); }
        private static LocalSaveResult Result(LocalSaveStatus status, string diagnostic = null, AlaalaSaveSnapshot snapshot = null)
            => new LocalSaveResult { Status = status, Diagnostic = diagnostic, Snapshot = snapshot };
        private static bool IsStorageException(Exception exception) => exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException || exception is System.Security.SecurityException;
        private static LocalSaveResult IoFailure(Exception exception) => Result(LocalSaveStatus.IoError, "Local transaction failed: " + exception.GetType().Name);
        // 128-bit hash components bound Windows path length. Embedded full owner identity
        // is always validated as well, so even a hash collision cannot cross-write accounts.
        private static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(value))).Replace("-", "").ToLowerInvariant().Substring(0, 32); }
        private static void WriteFlushed(string path, byte[] bytes)
        { using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); } }
        private static void DeleteTemporary(string path)
        { if (path == null) return; try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        private void CheckPath(bool create)
        {
            // Hash-only children prevent traversal. Reject linked directories/files rather than following them.
            for (var node = new DirectoryInfo(directory); node != null; node = node.Parent)
                if (node.Exists && (node.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked storage directories are unsupported.");
            if (create) Directory.CreateDirectory(directory);
        }

        private bool AllowsProgression(AlaalaSaveSnapshot previous, AlaalaSaveSnapshot next)
        {
            if (previous.runId != next.runId) return false; // Overwrite authorization is deliberately not provided in Phase 2B.
            if (!previous.inventory.collected.All(next.inventory.collected.Contains)) return false;
            foreach (var old in previous.tasks)
            {
                var current = next.tasks.First(t => t.taskId == old.taskId);
                if (old.state == TaskState.Completed && current.state != TaskState.Completed || old.state == TaskState.Active && current.state == TaskState.Inactive) return false;
                var stages = catalog.Tasks.First(t => t.TaskId == old.taskId).Stages;
                if (Array.FindIndex(stages, s => s.StageId == current.stageId) < Array.FindIndex(stages, s => s.StageId == old.stageId)) return false;
            }
            foreach (var value in previous.session.booleans.Where(x => x.value && (x.key.StartsWith("journal_") && x.key.Contains("_unlocked:") || x.key.StartsWith("task_progress:"))))
                if (!next.session.booleans.Any(x => x.key == value.key && x.value)) return false;
            foreach (var value in previous.session.integers.Where(x => x.key.StartsWith("task_progress:")))
                if ((next.session.integers.FirstOrDefault(x => x.key == value.key)?.value ?? 0) < value.value) return false;
            foreach (var value in previous.session.strings.Where(x => x.value.Length > 0 &&
                (x.key.StartsWith("journal_reflection_response:") || x.key.StartsWith("journal_reflection_follow_up:"))))
                if (next.session.strings.FirstOrDefault(x => x.key == value.key)?.value != value.value) return false;
            return true;
        }

        private sealed class CaptureRejectedException : Exception { internal CaptureRejectedException(string message) : base(message) { } }

#if UNITY_EDITOR
        // Isolated fixture I/O only. No authentication is manufactured and no production path can be supplied.
        public static AlaalaLocalSaveStore CreateIsolatedEditorStore(string root, AlaalaSaveCatalog catalog,
            string title, string account, Func<bool> authorized)
        {
            string full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Application.isPlaying || !full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("alaala-storage-tests-", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(account)) throw new ArgumentException("Isolated Edit Mode test directory required.");
            return new AlaalaLocalSaveStore(full, catalog, new AlaalaSaveOwner(title, account), authorized, authorized, true);
        }
        public Task<LocalSaveResult> SaveIsolatedFixtureAsync(AlaalaSaveSnapshot fixture)
        {
            if (!isolatedTest || Application.isPlaying) throw new InvalidOperationException("Isolated Editor fixtures only.");
            return SaveAsync(revision => { var copy = JsonUtility.FromJson<AlaalaSaveSnapshot>(JsonUtility.ToJson(fixture)); copy.saveRevision = revision; return copy; });
        }
        public string EditorTestDirectory => isolatedTest ? directory : throw new InvalidOperationException("Not a test store.");
        public Action<string> EditorTransactionProbe { get; set; }
#endif
    }
}
