# Gather information source expansion

Implemented in DraftWorld, RandomHouse, and RandomHouse1. The objective still requires **any three unique sources**. No runtime scripts were changed for this pass.

## 1–4. Nena and Toto: configuration only

| NPC | Existing trigger path in DraftWorld | Progress ID |
| --- | --- | --- |
| Nena | NPC/Nena/LITTLEGIRL/Sphere | `nena` |
| Toto | NPC/Toto/boy@Sitting/Sphere | `toto` |

Neither NPC previously had a TaskProgressTrigger. Added one to each existing trigger object, with taskId `main_investigate_pili`, amount 1, once true, registerOnCompletion true, and its existing NPCConversationTrigger reference. Added their IDs to the existing gather_information stage's allowedProgressIds.

Nena retains her existing `Conversation` (six speech nodes) and `repeat convo` (one node, “Magandang umaga po!”). Toto retains his existing `Conversation` (eight nodes) and `Repeat convo` (one node, “Ang init sa pili!”). Neither conversation content, conversation references, nor first/repeat behavior was edited. Both continue using NPCConversationTrigger's existing local first-conversation completion state. Task deduplication remains independently stored by TaskManager in SessionStoryState.

As with existing working information sources, TaskProgressTrigger listens to ConversationFinished. The first qualifying completion registers the ID; subsequent completions are rejected centrally. This preserves existing behavior if a conversation was encountered before the task became active.

## 5–8. House residents and dialogue

RandomHouse uses the existing `Idle` resident, displayed as **Residente**, with progress ID `randomhouse_resident`. No NPC model was added.

First conversation:

1. Miguel: “May narinig po ba kayo tungkol sa Makamisa?”
2. Residente: “Makamisa? Narinig ko na rin ang pangalang iyan. Parang may kaugnayan daw iyon sa mga lumang kuwento tungkol sa Pili.”
3. Residente: “Pero hindi ko na maalala nang maayos. Iba-iba rin ang sinasabi ng mga tao rito.”
4. Miguel: “Ganoon po ba... Salamat.”

Repeat: “Pasensya na, iyon lang din ang alam ko tungkol sa Makamisa.”

RandomHouse1 uses the existing `ANDAY@Neutral Idle` resident, displayed as **Anday**, with progress ID `randomhouse1_anday`.

First conversation:

1. Miguel: “May alam po ba kayo tungkol sa Makamisa?”
2. Anday: “Kaunti lang. Naririnig ko ang pangalang iyan paminsan-minsan, pero hindi pare-pareho ang kuwento ng mga tao.”
3. Anday: “Kung gusto mo ng mas siguradong sagot, baka kailangan mo pang magtanong sa iba.”
4. Miguel: “Sige po. Salamat.”

Repeat: “Wala na rin akong ibang maidaragdag tungkol doon.”

Each resident has a child `Makamisa Information Trigger`, a trigger collider, NPCConversationTrigger, TaskProgressTrigger, and first/repeat NPCConversation objects. All four new EditableConversations serialize `Parameters = []`. House first/repeat selection persists through the existing SessionStoryState flags `randomhouse_resident_information_completed` and `randomhouse1_anday_information_completed`.

## 9–11. Infrastructure

Both houses already contained a player, output camera, task HUD, `[E] Kausapin` prompt, one EventSystem, journal/inventory canvases, FadeImageCanvas/FadeController, and scene-entrance infrastructure. These were reused. Neither house had an NPCConversationTrigger, DialogueCanvas, or ConversationManager.

Added one DialogueCanvas with its existing ConversationManager architecture, copied from MaestroBenHouse, to each house. Existing canvas sorting values were preserved. Each house also received one instance of the existing GatherInformationReactionController on TaskUI, matching its scene-local use in DraftWorld. No reaction code was added to NPCs.

Play Mode exposed missing camera wiring: both houses had disabled CinemachineBrains and no virtual camera. Added one fixed CinemachineCamera per house, matching the existing output camera's position, rotation, and lens, and enabled the existing Brain. This allows the unchanged generic reaction to own and restore its zoom. RandomHouse1 also had a pre-existing extra active Main Camera/AudioListener at the default origin; that unused GameObject was disabled, not deleted.

No EventSystem, fade system, StorySequenceCoordinator, or output camera was added. Runtime checks found exactly one active EventSystem and one ConversationManager in each tested scene.

## 12–15. Progress, reaction, and Journal

TaskManager's authoritative source IDs, once-only storage, allowed-source filtering, and stage transition remain unchanged. The existing seven allowed IDs were retained; four alternatives were appended. Required count remains 3. After the stage transition, further information-source contributions are rejected by the next stage's allowed-source filtering, preventing 4/3.

GatherInformationReactionController, TaskManager, MainInvestigationTaskController, NPCConversationTrigger, TaskProgressTrigger, StorySequenceCoordinator, and Journal scripts were not modified. The Observation `mga_bulung_bulungan_sa_pili` remains unlocked only by TaskManager's gather_information stage transition. No NPC directly unlocks it.

All four new sources were tested as the third source. The generic reaction waited for the NPC dialogue to finish, acquired the story sequence, played Miguel's self-dialogue, and released it. Town camera measurements were 45 → 41.4 → 45 degrees. House camera retests measured 40 → 36.8 → 40 degrees. Task HUD/buttons/prompt hid during the sequence and returned afterward. The Journal notification remained queued while the story sequence was active and completed afterward with the expected Observation ID.

## 16–17. Play Mode and persistence results

Automated Play Mode exercised actual NPCConversationTrigger starts and ConversationManager option advancement; task progress was not directly incremented in the four full route tests.

| Test | Result |
| --- | --- |
| Nena first / repeat | 0 → 1; repeat remains 1 and uses original repeat line |
| Toto first / repeat | 1 → 2; repeat remains 2 and uses original repeat line |
| RandomHouse third / repeat | Reaches milestone; reaction once; short repeat adds nothing |
| RandomHouse1 after completion | First and repeat work; no extra progress or reaction |
| RandomHouse1 → Nena → Toto | 1 → 2 → milestone; Toto triggers reaction |
| Toto → RandomHouse → Nena | 1 → 2 → milestone; Nena triggers reaction |
| Mang Kardo → Nena → RandomHouse1 | Existing/new source combination passes; Anday triggers reaction |
| RandomHouse1 repeat before milestone | Remains 1/3 |
| Cross-scene | Same TaskManager instance survives town/house loads; 1/3 and 2/3 survive scene changes |
| Prompt | Existing prompt becomes visible through trigger entry in both houses; RandomHouse1 physical Rigidbody proximity also checked |
| Camera wiring retests | Seeded 2/3 using TaskManager, then completed each actual house conversation and checked zoom/HUD/Journal release |

Scene changes used runtime SceneManager loads. Tests invoked conversation starts and UI options programmatically; physical E-key presses and walking through the existing doorway/fade transitions were not manually exercised. Their existing input and transition code was preserved.

## 18. Remaining issues / scope

No known unresolved source-counting or dialogue issue. Existing missing-script warnings on Point Light objects and an intermittent MCP relay timeout were observed; they are unrelated and were not changed. No new gameplay exception was found in the final house checks. Existing unrelated worktree changes were preserved.

The editor was returned to Edit Mode with DraftWorld open after testing.

## Present Pili Church NPC population pass

All seven ordinary NPC model instances in `Assets/Scenes/Present Pili Church.unity` were inspected. The actual second object name is `MANASEBIA@Sitting (1)`; the request's `MANASERIA@Sitting (1)` spelling did not match the scene. Every object was a scene instance under `NPC`, with its own imported model hierarchy and Animator. None had an NPCConversationTrigger, NPCConversation, TaskProgressTrigger, collider, or story-specific conversation behavior. No project C# script references any of the old instance names. The old names were therefore treated as imported model/team placeholders. The object formerly named `agaton@Breathing Idle` was treated as an ordinary present-day NPC, not Padre/Fra Agaton.

The scene instance renames are:

| Old scene instance | New scene instance | Animator/controller preserved | Unique source ID |
| --- | --- | --- | --- |
| `npcwoman2@Sitting Talking` | `NPC_AlingRosa` | `sittalkchurch` | `church_aling_rosa` |
| `MANASEBIA@Sitting (1)` | `NPC_AlingPilar` | `manasebia` | `church_aling_pilar` |
| `npcwoman@Sitting` | `NPC_AlingMarta` | `npcwoman` | `church_aling_marta` |
| `NPC3@Sitting Idle` | `NPC_MangTomas` | `npc3` | `church_mang_tomas` |
| `YSAGANI.1@Sitting` | `NPC_MangLando` | `ysagani` | `church_mang_lando` |
| `npcelder@Breathing Idle` | `NPC_MangPedro` | `npcelder` | `church_mang_pedro` |
| `agaton@Breathing Idle` | `NPC_AlingElena` | no controller was assigned before this pass | `church_aling_elena` |

Only scene GameObject names changed. Source FBX files, prefab/model assets, meshes, bones, animation clips, Animator Controllers, Animator parameters, Avatars, materials, textures, positions, rotations, scales, and existing animation references were left intact. The `agaton` object had an Avatar but no RuntimeAnimatorController before the pass; no animation asset was added.

All seven NPCs were made talkable with a child `Makamisa Information Trigger` containing a SphereCollider, NPCConversationTrigger, TaskProgressTrigger, and two child NPCConversation objects. All use the existing Canvas `TutorialUI/TalkPrompt/[E] Kausapin`, ConversationManager, and player reference. Each first conversation completion registers exactly one stable source ID through TaskManager; repeat conversations use the existing trigger's first-completed flag and cannot register again. The seven IDs were appended to the existing `gather_information` allowed source list in DraftWorld. Required count remains 3.

The full church dialogue is:

### NPC_AlingRosa — `church_aling_rosa`

First:

1. Miguel: “May narinig po ba kayo tungkol sa Makamisa?”
2. Aling Rosa: “Makamisa? Oo, narinig ko na ang pangalang iyan noon.”
3. Aling Rosa: “Pero hindi ko rin alam ang buong kuwento. Parang may kinalaman daw iyon sa mga lumang kuwento tungkol sa Pili.”
4. Miguel: “Salamat po.”

Repeat: “Iyon lang din ang alam ko tungkol doon.”

### NPC_AlingPilar — `church_aling_pilar`

First:

1. Miguel: “Narinig n'yo na po ba ang Makamisa?”
2. Aling Pilar: “Oo, pero pangalan lang halos ang naaalala ko.”
3. Aling Pilar: “Kung gusto mong malaman talaga kung ano iyon, kailangan mo pang magtanong sa iba.”
4. Miguel: “Sige po. Salamat.”

Repeat: “Pasensya na, iyon lang ang naaalala ko.”

### NPC_AlingMarta — `church_aling_marta`

First:

1. Miguel: “May alam po ba kayo tungkol sa Makamisa?”
2. Aling Marta: “Kaunti lang. May mga tao ritong nagbabanggit niyan paminsan-minsan.”
3. Aling Marta: “Iba-iba ang kuwento depende kung sino ang tatanungan mo.”
4. Miguel: “Ganun po ba... Salamat.”

Repeat: “Wala na rin akong ibang maidaragdag.”

### NPC_MangTomas — `church_mang_tomas`

First:

1. Miguel: “Manong, pamilyar po ba sa inyo ang Makamisa?”
2. Mang Tomas: “Pamilyar ang pangalan, pero hindi ko alam kung alin ang tamang kuwento.”
3. Mang Tomas: “Mas mabuti sigurong magtanong ka pa sa iba.”
4. Miguel: “Salamat po.”

Repeat: “Pangalan lang talaga ang alam ko tungkol doon.”

### NPC_MangLando — `church_mang_lando`

First:

1. Miguel: “May narinig po ba kayo tungkol sa Makamisa?”
2. Mang Lando: “Narinig ko na rin sa mga usapan ng mga tao.”
3. Mang Lando: “Pero hindi ko naunawaan kung ano talaga iyon.”
4. Miguel: “Sige po, salamat.”

Repeat: “Hindi ko pa rin alam ang buong kuwento.”

### NPC_MangPedro — `church_mang_pedro`

First:

1. Miguel: “Mang Pedro, may alam po ba kayo sa Makamisa?”
2. Mang Pedro: “Kaunti lang. May nagsasabing bahagi raw iyon ng mga lumang kuwento sa Pili.”
3. Mang Pedro: “Hindi ko alam kung mapagkakatiwalaan ang narinig ko.”
4. Miguel: “Salamat po.”

Repeat: “Iyon lang ang narinig ko tungkol sa Makamisa.”

### NPC_AlingElena — `church_aling_elena`

First:

1. Miguel: “Aling Elena, narinig n'yo na po ang Makamisa?”
2. Aling Elena: “Parang pamilyar ang pangalan, pero kakaunti lang ang naaalala ko.”
3. Aling Elena: “Baka mas malinaw ang sagot ng ibang taga-Pili.”
4. Miguel: “Sige po. Salamat.”

Repeat: “Wala na akong ibang naaalala tungkol doon.”

Every new EditableConversation has `Parameters = []`. The scene already contained exactly one EventSystem, one DialogueCanvas with one ConversationManager, the interaction prompt, player, InventoryCanvas, ReconstructionJournalCanvas, ClarityManager, FadeImageCanvas, IrisTransition, and ManuscriptTransportSequence. Those systems were reused. No duplicate EventSystem, DialogueCanvas, ConversationManager, fade system, or manuscript system was introduced. One existing `GatherInformationReactionController` was added to the church TaskUI because this scene can now contain the third source; its code remains generic and no NPC name was added to it.

The manuscript, ClarityDocumentSource/viewer, manuscript regions, manuscript content, camera behavior, ManuscriptTransportSequence, IrisTransition, Transport 1 flow, and church document progression were not modified.

Play Mode results for this pass:

- A fresh church route `NPC_AlingRosa → NPC_AlingPilar → NPC_AlingMarta` reached `1/3 → 2/3 → 3/3`; the existing generic reaction ran after Marta's conversation, and the Journal flag `mga_bulung_bulungan_sa_pili` unlocked once.
- The scene reported exactly one EventSystem and one ConversationManager throughout the route.
- After completion, Rosa's repeat dialogue played and stayed complete; Mang Lando's unused first dialogue remained talkable and did not add `4/3`, replay the reaction, or duplicate the Journal unlock.
- The reaction's existing HUD/story flow completed with the Journal notification queued after the story sequence. The source-independent reaction behavior and centralized TaskManager unlock were unchanged.
- Cross-scene persistence was verified by starting in DraftWorld, entering Present Pili Church, and completing church sources with the same TaskManager instance. The task stage carried across scene loads.
- A mixed route `Nena → Present Pili Church / NPC_AlingRosa → RandomHouse / Idle` reached `1/3 → 2/3 → find_knowledgeable_person` with the same TaskManager instance ID across all three scenes; the Journal flag was true after the third source.
- The existing [E] prompt was assigned to every trigger and was activated through the production trigger-entry path. Dialogue UI, Inventory, Journal, Clarity, fade, Iris, and manuscript objects remained present with their original single-instance counts.

No unresolved church source, dialogue, naming, duplicate infrastructure, or task-count issue is known. Existing unrelated Point Light missing-script warnings, Adaptive Performance initialization warning, and terrain warning remain outside this pass. The editor was returned to Edit Mode with Present Pili Church open and the scene clean.
