# Phase 2A local checkpoint storage

This extends schema 1 / `alaala-foundation-1`. The storage layer has no cloud API, periodic
save, quit-save or arbitrary-position checkpoint. Phase 2B connects Continue and bounded
restoration through separate controllers; see `RESUMPTION.md`.

## Ownership and entry

The existing New Game reset has one observational call immediately before its existing
IntroScene load. `AlaalaLocalAutosave.NotifyNewGameStarting` authorizes a run in SceneMenu,
then opens storage for the current PlayFab session. Failure disables saving without
blocking New Game. Reset ordering and ContinueGame are unchanged.

The observer is bootstrapped only in non-development players. Editor/development builds
remain rejected by Phase 1. Direct development helpers are unchanged and their progression
cannot open production storage. A future Continue coordinator must establish its own
validated run lifecycle. Phase 2B now reuses the validated snapshot's run ID for Continue.
The global build list starts with FinalLoginPage. Authenticated login retains its existing
SceneMenu destination. Direct unauthenticated production SceneMenu entry returns to login.

The root is `Application.persistentDataPath/AlaalaSaves/<title-hash>/<account-hash>`.
Each hash is 32 lowercase hex characters (128 bits of SHA-256), keeping Windows paths
bounded. No entered username/email appears in paths. Full Title ID and PlayFab ID are
validated inside every snapshot, so a hash collision fails closed instead of crossing owners.
Linked directories and files are rejected. Unusually long/inaccessible platform roots
produce an I/O result rather than a destructive fallback.

## Transaction and recovery

All API calls start on Unity's main thread. A shared async semaphore serializes instances;
an exclusive per-account file lease also excludes another application process. Capture
occurs after obtaining the lease. FileStream.WriteAsync yields for the temporary write;
Flush(true), validation and commit run in the Unity continuation. No background thread
calls Unity APIs. JSON serialization/validation and the final flush/rename are bounded
main-thread work and still require runtime performance testing.

Files are `checkpoint.json`, `checkpoint.backup.json`, and `transaction.lock`.
A uniquely named same-directory temporary file is flushed and reread through the full
Phase 1 validator. The account session, genuine run, safe checkpoint and exact captured
progress are rechecked immediately before publishing. Windows File.Replace publishes
the primary while retaining its previous valid contents as backup. First publication
creates a flushed backup before moving the primary into place. There is no delete-then-move
replacement fallback. Unsupported atomic replacement returns I/O failure and preserves
committed copies.

Revision is one greater than the maximum surviving validated revision. Identical requests
coalesce without incrementing. Same-run task/progress, collected-item/discovery and accepted
reflection regressions are rejected. Phase 2B preserves a different existing saved run;
replacement needs explicit future overwrite authorization. A newer backup than primary is treated as a conflict,
not discarded. If a corrupt latest primary no longer exposes a trustworthy revision,
recovery ordering is necessarily based on the surviving validated backup.

Discovery validates primary, then backup, and reports their statuses separately. It never
repairs files or imports gameplay. Explicit RecoverBackupAsync preserves a corrupt primary
as `quarantine-<id>.json`; it refuses to downgrade a valid primary or overwrite a foreign,
unsupported-version or unreadable primary. It can also rebuild a missing/corrupt backup
from the validated primary, quarantining the corrupt backup. Continue invokes this policy
only after the menu explains recovery and the player selects Magpatuloy. Future formats,
foreign copies and newer-backup conflicts require explicit resolution; autosave will not
guess. Orphan `.pending` files from a process crash are never accepted as saves.

## Observed checkpoints

- `draftworld.sulatin_search`: PostPart2SulatinTaskCoordinator clears arrival pending,
  records arrival handled, opens the arrival fade, releases controls, starts the remaining
  Sulatin task and seeds owned progress. The observer waits beyond initialization and
  synchronous reward callbacks, then requires all Phase 1 checkpoint and presentation
  guards. Task notification/tutorial/modal completion is required, not just the task flag.
- `draftworld.completed_epilogue`: NewAlaalaPostCreditsEpilogue sets completion only after
  genuine final task completion, Padre discovery and release of house free-roam ownership.
  Its scene is MaestroBenHouse, while this checkpoint targets DraftWorld/MaestroSpawn.
  Capture therefore waits for the normal player-directed return to DraftWorld, clearing
  the epilogue return context and completing incoming presentation. Immediate saving in
  MaestroBenHouse is unsupported; no additional checkpoint is invented.

SessionStoryState.ProgressValueChanged observes accepted Boolean/integer/string changes.
Task changes, inventory transactions and scene entry also request work. A single pending
request coalesces events and retries presentation blockers every 0.5 seconds, only while
there is pending work in DraftWorld. Safety comes from actual state/ownership validation,
not the delay. There is no Update-based global manager polling. Scene changes and destroyed
coordinator cancel an in-flight checkpoint commit. Account logout/relogin (even to the
same ID), Title ID change, run change or synthetic marking invalidate the captured lease.

Typing is not saved: reflection submission installs response and selected follow-up strings.
These changes defer capture until the journal/presentations close at a supported checkpoint.
Answers accepted elsewhere remain session-only until returning to an approved DraftWorld
boundary; quitting beforehand can still lose them. Part 3/credits/white-void/chase states
never create a new checkpoint.

## APIs and tests

`TryOpenAuthenticated`, `SaveCheckpointAsync`, `InspectSaveAvailabilityAsync`,
`ReadValidatedSnapshotAsync`, and `RecoverBackupAsync` form the later Phase 2B boundary.
Returned snapshots are detached validated data; reads alone never hydrate gameplay.
LastResult and sanitized `[AlaalaSave]` diagnostics expose failures without passwords,
tokens, reflection text or private paths. Failed writes do not retry endlessly: a new
progression event/scene entry may retry. No player-facing error UI is added.

In Edit Mode invoke `AlaalaLocalSaveTests.Start()` and poll `Status()`. Tests allocate a
random `alaala-storage-tests-<id>` directory under OS temp, use fixture owners, and remove
the directory in finally. The Editor factory cannot open the production root. Tests
exercise real file replacement, locking and recovery, then rerun the 83 Phase 1 checks.
No production save directory or live PlayFab account is used. Play Mode has not been used;
actual production checkpoint capture, transition ordering, shutdown interruption and
performance still need manual player-build verification. Phase 2B restoration is connected,
but application-restart restoration still requires a real authenticated player-build test.
