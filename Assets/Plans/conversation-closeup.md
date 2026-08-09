# Project Overview
- Game Title: Open World Conversation
- High-Level Concept: An open-world exploration game with a conversation system.
- Players: Single player
- Inspiration / Reference Games: RPGs with cinematic dialogue transitions.
- Tone / Art Direction: Realistic/Stylized 3D.
- Target Platform: Standalone Windows.
- Render Pipeline: Built-in.

# Game Mechanics
## Core Gameplay Loop
The player explores the world and interacts with NPCs using the 'E' key.

## Controls and Input Methods
- Movement: Standard WASD/Controller.
- Interaction: 'E' key to start/stop conversations.

# UI
- A prompt "Press E to Talk" appears when near an NPC.
- A dialogue UI (handled by `DialogueEditor`) appears during conversation.

# Key Asset & Context
- `womanconversation.cs`: The script managing the interaction.
- `AlingSela@Breathing Idle`: The NPC GameObject.
- `Player3D`: The Player GameObject.
- `CM_CloseUp_AlingSela`: A new Cinemachine Virtual Camera for the cutscene effect.

# Implementation Steps
1. **Modify `womanconversation.cs`**:
    - Add a `[SerializeField] private GameObject closeUpCamera;` field.
    - In `Update()`, set `closeUpCamera.SetActive(true);` when the conversation starts.
    - In `OnTriggerExit()` and when the conversation ends, set `closeUpCamera.SetActive(false);`.
    - Ensure `isTalking` is reset correctly.

2. **Create and Configure `CM_CloseUp_AlingSela`**:
    - Create a new GameObject named `CM_CloseUp_AlingSela`.
    - Add `CinemachineCamera` component.
    - Set `Priority` to `20` (higher than the default `10`).
    - Set `Follow` and `LookAt` targets via script or Inspector. For a cutscene feel, I'll position it near the NPC's face.
    - Add `CinemachineFollow` with an offset like `(0, 1.6, 2)` relative to the NPC.
    - Add `CinemachineRotationComposer` to frame the NPC.

3. **Wire Assets**:
    - Assign the new camera to the `closeUpCamera` field in the `womanconversation` component on the `Trigger` object.

# Verification & Testing
- Move the player to the NPC.
- Press 'E' to start the conversation.
- Verify that the camera smoothly transitions to the close-up view.
- Walk away or end the conversation and verify the camera transitions back.
