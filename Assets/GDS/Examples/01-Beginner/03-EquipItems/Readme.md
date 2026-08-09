### Equip Items

> This example reuses the `Minimal_Store` ScriptableObject.

This example shows how to use the `SetBag` to create an equipment screen.

When an item is placed into a slot, their **Tags** will be compared and if there are no matches the operation will fail. **Tags** are empty **ScriptableObjects**.

The **HighlightSlotManipulator** will add proper styling to show **legal/illegal** states.

We create custom **ItemBases** with proper **Tags** and also add **Tags** to **Equipment** slots. This example uses tags from `Examples/Common/Tags`.

Finally, we add some styling to display equipment slot labels and make the slots a little larger (see `EquipItems_Styles.uss`).