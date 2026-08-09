### ScriptableObject Bag

This example shows how to define a bag as a **ScriptableObject**. This can be useful when you need to persist the data between scenes or when exiting Play Mode.

It reuses the same `Minimal_Store` from previous examples.

We create a `ListBagSO` instance by going to `Create > SO > Core > ListBag`.

The controller wires up the bag data and the view. 

> Note the use of the `InlineEditor` attribute — it allows the **ScriptableObject** to be viewed and edited directly in the Inspector alongside the referencing field.