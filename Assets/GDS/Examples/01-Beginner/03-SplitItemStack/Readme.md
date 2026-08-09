### Split item stacks

This example demonstrates how to split item stacks. 

By default, all **stackable** items can stack up to their `MaxStackSize`. 

We define a custom **Store** and extend the stacking behavior to support splitting a stack in half and "unstacking" one.

To achieve this, the store defines an event modifier (Shift by default) and uses the built-in functions: 
- `SplitHalf` in `OnPickHandler`
- `TransferOne` in `OnPlaceHandler`

This way, `Shift + Click`-ing a stackable item will pick half a stack, and while dagging a stack, and `Shift + Click`-ing an empty slot or an item of the same type, will transfer one from dragged item to target slot.