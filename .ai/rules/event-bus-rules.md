# EventBus Rules

## When to Use EventBus
Use EventBus only for cross-system events where direct callback wiring is impractical:
- `BottleClickedEvent` — input layer to presenter layer
- `ItemUsedEvent` — UI layer to item logic layer
- `ResetGameEvent` — UI button to game flow

## When NOT to Use EventBus
- Communication within the same layer
- Parent-to-child or child-to-parent within one feature
- Any case where an `Action` or interface callback is sufficient

## Declaration Rule
- All event structs must be declared inside `GameEvent.cs`
- Never create a separate file per event struct

## Structure Rule
```csharp
// GameEvent.cs
public static class GameEvent
{
    public struct BottleClickedEvent { public int BottleIndex; }
    public struct ItemUsedEvent { public ItemType Type; }
    public struct ResetGameEvent { }
}
```

## Injection Rule
- Do not inject `EventBus` into Service or low-level classes
- Only Presenter and Controller may subscribe to EventBus
- Prefer callback flow (Action/Func) over EventBus for local wiring

## Handler Signature
- Subscribe handlers receive events by `in` reference: `void OnItemUsed(in ItemUsedEvent e)`
- Event structs are immutable, so `in` is always safe and avoids struct copy on each dispatch.

```csharp
// Good
private void OnItemUsed(in GameEvent.ItemUsedEvent e) { ... }

// Bad — value copy on every dispatch
private void OnItemUsed(GameEvent.ItemUsedEvent e) { ... }
```
