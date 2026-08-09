### Right-Click and Double-Click to Move Items

This example demonstrates how to move items between inventories using **Right-Click** and **Double-Click**.

In order to handle **Right and Double Click** we create custom **Manipulators** for each interaction. These **Manipulators** are simple `PointerManipulators` with custom activation rules. They will publish `CustomRightClickEvent` and `CustomDoubleClickEvent` on the event bus when certain conditions are met.

> Note that these events are **not** part of the **core framework** and instead are defined specifically for this example (see `CustomEvents.cs`).

A custom **Store** is used to listen for `CustomRightClickEvent` and `CustomDoubleClickEvent` on the event bus.

As usual, the **Controller** wires up the **bag data**, the **views** and the **Store**. It also adds the **Manipulators** to the **root visual element**:

```cs
root.AddManipulator(new RightClickManipulator(store));
root.AddManipulator(new DoubleClickManipulator(store));
```