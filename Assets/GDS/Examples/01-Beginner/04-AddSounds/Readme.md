### Add Sounds on Event Trigger

This example demonstrates how to use the event bus to play sounds when specific events are triggered.

It reuses the same `Minimal Store` from the first example. 

When an item is successfully picked or placed, the **store** publishes a **Pick/Place Success** event on the **event bus**:

```cs
Bus.Publish(result);
```

We add a new script (`AddSounds_Sfx`) and attach it to the UI Document in the scene. This script defines a list of sounds, listens to events on the event bus, and plays a matching sound if it exists.

> In the future, there might be a way to attach sound clips to events directly in the inspector, but for now, this is the recommended workflow.
 

