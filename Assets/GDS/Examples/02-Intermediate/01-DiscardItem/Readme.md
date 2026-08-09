### Drop Items into the World

This example shows how to drop items into the World and collect them back into the inventory.

It requires an `EventSystem` component somewhere in scene hierarchy.

It uses a custom **Store** which listens to `PickWorldItem` and `DropWorldItem` events in addition to the usual `Pick` and `Place` events. The Store also keeps a reference to the "Inventory" bag.

Inside `Items` folder we create a couple of **Item Bases** using `Create > SO > Core > ItemBaseWithPrefab` context menu. Notice that we can specify an item prefab in these Scriptable Objects. This prefab will be used to spawn the item into the world. It should include a Collider component.

The Controller listens for PointerUp events on the Backdrop element and publishes a DropWorldItem when all conditions are met.

To spawn item prefabs we attach the `SpawnDespawnItemPrefab` script (`GDS/Common/Scripts`) to the **UI Document** GameObject in the scene.

> Note that this script is located in a different Assembly Definition and must be added as a dependency to your current Assembly Definition.

The script requires the following:
- The **Store** - we use the same **Custom Store** (`DiscardItem_Store`)
- Spawn point Transform - items will spawn around this point in the world, the scene includes a cube object for that purpose
- Wrapper prefab - a prefab that wraps the item prefab and handles mouse interaction (use the provided prefab as reference)
- Default prefab - used when an item does not define a prefab. By default, it's a **Billboard** that renders a sprite oriented towards the camera.
- Drop radius and offset - items will spawn at a random point on the circle with said radius and be translated by offset value.
- Despawn VFX - A Particle System prefab that will spawn when the item is collected

Additionally, this example includes an `InputAction` asset and `Player Input` component in the scene to toggle the UI (similar setup to a previous example).

