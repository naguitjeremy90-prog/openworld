### Move items between bags

This example shows how to move items between inventories (**bags**) either by **drag-and-drop** or by **Ctrl + Click**.

The UXML Document contains 2 `ListBagViews` named **Left** and **Right**.

In the controller we declare 2 `ListBags`, query the views by name and initialize them with the 2 `ListBags`. We also inject the Bags into the Store.

The logic of moving items is defined in the **custom Store**, in `OnPickItem` event handler. The target Bag is determined based on current event and its modifiers. You can adjust the event modifier in the inspector (**Ctrl** by default).

Under the hood, a move operation performs a series of checks and then removes the item from source bag and adds it to target bag.
