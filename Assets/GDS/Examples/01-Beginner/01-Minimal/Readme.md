### Minimal Example

> Note: Use your editor's **Markdown Preview** to read this document (`Ctrl+Shift+V` in **VS Code**)

This example contains the minimal setup required to display an inventory.

Required scripts and assets:
- UI Document Asset (`Minimal_Document`, aka UXML asset)- a VisualTreeAsset which contains a `ListBagView`
- Store (`Minimal_Store`) - a custom Store which listens for events and updates the ListBag
- Controller (`Minimal_Controller`) - links the Store to the `ListBagView` and adds a basic drag and drop behavior as well as a tooltip.
  
`Minimal_Store` is a **ScriptableObject** that defines the logic of adding and removing items from `bags`.

> Note: The **ScriptableObject** asset has a custom asset label - `ResetOnEnterPlay`. This label is used in an editor script (see `Core/Editor/ResetAssetsOnEnterPlay.cs`) to reset **ScriptableObjects** when entering play mode.

The scene has a **GameObject** (named UI Document) with a `UI Document` a `Minimal_Controller` components. All examples use more or less the same structure.

The UI Document component references `GDS Default Panel Settings` asset and `Minimal_Document` uxml asset.

`GDS Default Panel Settings` is a shared **panel settings asset** used by most examples. It uses a custom runtime **uss theme** (`GDSDefaultRuntimeTheme`), which scales with screen size and has a reference resolution of `1920x1080`.

In `Minimal_Controller` we declare a `ListBag` and link it to the `ListBagView` and the `Store`. We also add **drag and drop** and **tooltip** behaviors in form of **Manipulators** attached to the **root visual element**.

You can adjust the `ListBag` size in the inspector.

You can add new items by dragging `ItemBase` scriptable objects into the **item field** (or use the select target button). This will create an instance of an `Item` with said base.

To create a new `ItemBase` right-click in the project window and go to `Create > SO > Core > ItemBase`.

Creating a custom `ItemBase` is covered in a later example.

