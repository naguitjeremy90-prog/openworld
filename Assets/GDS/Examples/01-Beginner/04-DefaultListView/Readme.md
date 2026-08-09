### Default ListView

This example shows how to use `ListBagView` together with the default UI Toolkit `ListView`.

It reuses the `MoveItems_Store` defined in a previous example in order to move items between the two lists (Bags) via **drag-and-drop** and **Ctrl + Click**.

The UXML document contains a `ListView` (from the built-in UI Toolkit library) and a `ListBagView`. 

In the **Controller** we wire up the bags with their respective views and also inject them into the **Store**. The `ListView` item renderer needs to implement `IContextItem` interface in order to be draggable (see `CustomSlot.cs`). 

