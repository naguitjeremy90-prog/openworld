### Toggle UI

This example shows how to toggle the UI Document on and off. It reuses the `Minimal_Store` ScriptableObject.

In addition to **Controller** and **Uxml Document** it also contains an `Input Actions` asset (from Unity's **New Input System**).

The `Input Actions` defines 2 actions:
- `ToggleUI` - bound to **Tab** key
- `CloseUI` - bound to **Esc** key

In the scene, the **UI Document** object  has a `Player Input` component that references the `Input Actions` asset. The chosen **Behavior** is `SendMessages` which means our controller needs to define **action listeners** named `OnToggleUI` and `OnCloseUI`. These listeners update a **visibility flag** and show or hide the **root visual element**.

The scene contains a second **UI Document** used only to display a **help text** which remains visible at all times.