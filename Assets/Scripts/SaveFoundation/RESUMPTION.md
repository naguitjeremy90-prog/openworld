# Phase 2B: authenticated Continue and bounded restoration

No Unity Play Mode was used. This is implementation plus Edit Mode verification, not a
claim that application-restart restoration has succeeded in a player build.

## Startup and menu

The active build configuration is the global EditorBuildSettings scene list. Its existing
FinalLoginPage entry moves to the front; every other entry retains its enabled state and
relative order. No scene is serialized. Existing PlayFab login still loads SceneMenu.
No authentication provider, account credential handling or login UI is changed.

MainMenuManager attaches AlaalaContinueMenu at runtime only in SceneMenu. It finds the
existing Button by its persistent ContinueGame callback, disables it immediately, and
discovers the authenticated owner's validated local save. No button callback references
are removed. A small runtime TMP feedback label is the only added menu UI. Editor and
development builds cannot access production saves or perform genuine Continue. Direct
unauthenticated production menu entry returns to the existing login scene.

Discovery validates the primary/backup metadata and full snapshot/owner. Foreign,
unsupported-version, unreadable or newer-backup conflicts remain disabled. Continue also
requires completed morning arrival navigation (phase 4) and inventory introduction/item
teaching history. These are actual startup dependencies, not inferred defaults. A snapshot
without this context is rejected with feedback rather than edited to look complete.

An active live session in SceneMenu is not replaced. Continue supports a fresh authenticated
menu, including after application restart. Existing progress in memory requires a fresh
launch; there is no silent reset or merging when Continue is selected.

Menu discovery carries a version and authentication-session identity. Disabled/destroyed
menus and old accounts cannot publish availability. Duplicate Continue requests share the
coordinator's single active lease. New Game cannot run during a Continue transaction.

## Recovery and ordering

1. Re-read validated data for the current verified PlayFab Title ID and player ID.
2. If the menu announced a recoverable copy, explicitly repair it on the player's Continue
   action. Recovery preserves corrupt copies in quarantine; it never guesses foreign or
   future formats. Re-read and validate again.
3. Verify a fresh menu and authorize the snapshot's original run ID. Suspend autosave.
4. Create missing empty managers from the approved TaskManager template and the existing
   InventoryManager class. Install all typed session state, tasks, inventory and world
   values through Phase 1 silent APIs while still in SceneMenu.
5. Set only the approved MaestroSpawn route. Acquire a runtime loading Story Sequence
   owner and overlay, then load DraftWorld by verified scene path.
6. In sceneLoaded (after Awake/OnEnable, before Start/physics/input), establish the authored
   player movement/physics/collider fence. Required state was already installed before
   Awake: day/night/NPC selection, scan completion, reward visibility, journal categories
   and task stages therefore see the restored values.
7. Complete destination startup under that fence. Supplement only saved/discovered journal
   entries from the approved catalog where the scene list lacks an existing asset. In
   particular, DraftWorld lacked the existing final-reflection reference. No educational
   content is generated or copied into the snapshot, and no scene asset is changed.
8. Silently hydrate/rebuild journal views, verify spawn/day state and require exact canonical
   snapshot equality across session/task/inventory/world data. Verification ignores only
   the coordinator's own valid Story Sequence owner; any additional owner blocks it.
9. Release the movement/physics/collider fence and loading owner. Rebind autosave to the
   verified saved run and current authentication session. There is no new run ID for Continue.

No StartTask, CompleteTask, AddItem, discovery/reward or ordinary unlock method is called
to rebuild saved progress. Existing first-time inventory reveal readiness is installed
before InventoryUI initializes. Task trackers bind to the already-hydrated persistent
manager. The normal HUD ownership mechanism handles visibility; shared HUD alpha/style,
input fields and gameplay movement code are unchanged.

## Audited legacy startup details

DraftWorld contains two active PlayerSpawners for the same player. The gate verifies a
single distinct authored player and a single active MaestroSpawn, and waits for the existing
spawner to consume SpawnData. Player placement is checked against the live authored route,
never stored arbitrary coordinates.

The legacy TalkTutorial always restarts from Start and has no durable API. It is suppressed
only on this post-chapter restoration path. The one-shot ArrivalStart trigger's local
completion is restored from the already-required completed opening chapter, without
invoking its conversation. All other triggers keep their normal authored eligibility.

The scene's old CutsceneManager director autoplays but has only unbound Cinemachine shots,
no markers or story bindings. The restore gate stops/disables only that verified legacy
shape before its Start handler. Changed/bound content is rejected rather than skipped.
Chapter 1–13 and Maestro Ben Timeline assets remain untouched.

## Failure boundaries

Discovery and scene waits have real-time bounds. Destination errors/exceptions/assertions
are observed during loading and reject the attempt. Account changes invalidate every
stage. There is no fallback to Makamisa or IntroScene.

Pre-load rejection leaves SceneMenu and its prior state intact. After staging, failure
returns to SceneMenu, resets transient presentation caches, silently discards the staged
session, restores the previous menu world values and releases owned loading resources.
Valid save data is never rewritten by hydration/failure cleanup. If cleanup itself cannot
complete, saving remains inhibited and Continue requires a fresh launch; partial gameplay
is not released. Emergency safe-scene recovery is restricted to SceneMenu/FinalLoginPage.

The scene loader cannot interrupt arbitrary code that never returns from a Unity callback;
the deadline governs cooperative asynchronous waits. Catastrophic engine/build failures
still require restarting the application.
Recovery checks that a safe menu is actually loaded before releasing ownership; a frame
delay alone is not considered successful recovery. If neither safe scene can activate,
an explicit restart message replaces the loading screen and gameplay remains fenced.

## New Game coexistence

Simulan retains its existing reset order and IntroScene destination. It never deletes the
saved file. A new run receives a different ID, and the storage layer now refuses to replace
another saved run. No overwrite popup is invented here. Consequently an existing saved
run must receive explicit overwrite/archive policy in a later UI phase before another
new run can replace its slot. This limitation is deliberate and reported to the developer.

## Required manual player-build checks

- Non-development Windows build starts at FinalLoginPage; real PlayFab login reaches SceneMenu.
- No-save, valid-primary, recoverable-backup, foreign/corrupt/future-version and account-switch cases.
- Restart from each registered checkpoint; compare tasks/objectives, inventory/read/history,
  journal answers/follow-ups, Padre Agaton, scans, tutorials, morning/NPC state and MaestroSpawn.
- No task/reward/discovery/first-time reveal or completed opening dialogue replays.
- Force manager/startup failure; verify return to menu, released ownership and unchanged saves.
- Continue then autosave retains the same run and advances revision; Simulan preserves the old slot.

Unsupported: arbitrary positions, partial tutorial/cinematic checkpoints, Part 3 chase or
white-void entry, mid-Timeline resumption, live-playthrough replacement, cloud sync and
cross-device/conflict migration. Local schema and ownership are suitable inputs for a
future cloud adapter; no PlayFab Player Data/cloud calls are introduced.
