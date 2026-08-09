### Crafting Bench Example

This example demonstrates how to extend a `ListBag` to create a simple crafting bench similar to **Minecraft**.

#### How it works

The bench behaves as follows:
- items can be placed into ingredient slots
- when item positioning matches a certain predefined `Recipe`, the **Outcome** slot is populated
- an item can be crafted by dragging it from the **Outcome** slot

This example reuses the `Move_Store` from a previous example to allow moving items between bags.

#### Recipes

The `Recipe` **ScriptableObject** contains a list of ingredients and an outcome. Item position within a recipe matters.

Some `Recipes` have been added to  `Recipes` folder. 

#### Crafting Bench

`CraftingBench` class extends `ListBag`. It has a fixed size of 3 and includes a list of recipes and an **Observable** outcome slot. When the ingredient collection changes, it is compared to the recipe list and if there's a match we update the outcome **Observable**.

Additionally it overrides the `Remove` method from `ListBag`. Removing an item from the outcome slot will perform the craft operation. Crafting an item will decrement one from each ingredient and publish an event on the event bus.

The list of recipes can be populated from the inspector.

#### Crafting Bench View

The `CraftinBenchView` loads the UXML Document from the `Resources` folder and wires it to local references. 

It also includes preview support for UI Builder.

#### Controller

In addition to the usual setup, the Controller also adds a `HighlightSlotManipulator` to the **root visual element**. 

```cs
root.AddManipulator(new HighlightSlotManipulator(Store));
```

This Manipulator will highlight the slot in green when the operation is legal and red when the operation is illegal.