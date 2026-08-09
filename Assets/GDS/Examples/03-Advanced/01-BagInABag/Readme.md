### Bag in a Bag

This example shows how items can be used as containers (Bags), as seen in games like **DayZ** and **Escape from Tarkov**.

It reuses the `Minimal_Store` from the first example.

To enable container-item behavior, the following custom components are introduced:
- A custom `Item` and `ItemBase` 
- A custom `SlotView`
- A custom `BagView`

`ContainerItemBase` extends `ItemBase` by adding a **Capacity** field (**int**). When a `ContainerItem` is created, it initializes an internal `ListBag` with the specified capacity.

`ContainerSlotView` extends `VisualElement` and adds `SlotView` and a `ListBagView`. When current slot item changes, it will trigger a re-render of the `ListBagView`.

`ContainerBagView` is a simplified version of `ListBagView`. It uses a `ListBag` as a data source and renders a list of `ContainerSlotViews`. It has listeners that will re-render a particular slot or the whole collection. It also includes preview support for UI Builder.

The UXML Document imports styles from `BagInABag_Styles.uss`.