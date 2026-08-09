### Add a Custom Tooltip

This example demonstrates how to display a custom Tooltip when hovering over an item.

To enable the default **Tooltip**, we add a `TooltipManipulator` to the **root visual element** in the **Controller**. 

The default tooltip is mainly useful for debugging and basic inspection.

To create our own tooltip we need to add a **UXML Document** and wire it up in a C# class that extends the `TooltipView`.

`CustomTooltipView` **UXML document** defines labels for name, stack size, the stackable field and an image for item icon. The associated C# class updates the labels' text and item image on render.

Styling is defined in `CustomTooltip_Styles.uss` and attached to the **UXML document**.

The controller wires up the custom tooltip and the TooltipManipulator:

```cs
root.AddManipulator(new TooltipManipulator(new CustomTooltipView(TooltipViewAsset)));
```

> Note: 
> 
> In this example we use a serialized field to pass the **UXML document** from the inspector down to the `CustomTooltipView`. Alternatively we could load the asset from the **Resources** folder. You can see this in action in later examples and demos.
