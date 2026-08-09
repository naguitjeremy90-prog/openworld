### Add a random item to inventory

This example shows how to add random items to an inventory.

It reuses the `Minimal Store` Scriptable Object.

The **UXML document** includes a **Create item** `Button` placed underneath the `ListBagView`. 

The **Controller** defines a "catalog" (**List**) of `ItemBases`. The **list** is populated by Scriptable Objects from `Examples/Common/Items`. You can define new ItemBases and add them here.

In **Awake**, we attach a **click listener** to the **Create Item Button**. The callback will pick a random `ItemBase` from the list, create a new `Item` with that base and add it to the **Inventory** (ListBag)

Additionally, we attach a `PointerUpEvent` listener to the **Backdrop** element. The callback will **"destroy"** the dragged element by setting it to null.