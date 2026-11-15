# Error Handling Rules

## Principles
- Fail fast on real bugs; do not silently swallow errors
- Trust SerializeField / DI assignments — do not re-check what the wiring guarantees
- A conditional return that hides a programmer error is worse than a NullRef

## Null Check Rules
- Do not null-check SerializeField fields wired in the prefab/scene
- Do not null-check DI-injected dependencies
- Do not fall back to `GetComponent<T>()` when a SerializeField field exists
- Null-check only at system boundaries:
  - External user input
  - External API / SDK responses
  - Runtime-spawned objects that may be destroyed (`if (go == null) continue;`)
  - Optional fields explicitly documented as nullable

## Conditional Return Logging Rule
If a method has a conditional return that indicates an unexpected or error state, **a warning or error log must accompany the return** so the failure is observable.

```csharp
// Good
if (prefabs == null || prefabs.Length == 0)
{
    GameLogger.LogWarning("[FruitSpawner] FruitPrefabs is empty.");
    return;
}

// Bad — silent return hides the bug
if (prefabs == null || prefabs.Length == 0)
{
    return;
}
```

### When a log is required
- Returning early because a required dependency is missing
- Returning early because input is invalid
- Returning early because the system is in an unexpected state

### When a log is NOT required
- Early return as normal control flow (e.g. `if (selected == _isSelected) return;` idempotent guard)
- Early return because an optional feature is disabled (e.g. `if (!_settings.SpawnOnStart) return;`)
- Early return on a runtime-destroyed object iteration (`if (go == null) continue;`)

## Logging Channel
- Always use `GameLogger` — never `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` directly
- `GameLogger` respects `Const.IsLogEnabled` so builds can strip logs uniformly
- All log messages must start with `[ClassName]` prefix for traceability
  - Good: `GameLogger.LogWarning($"[BottlePresenter] No bottle at index {i}")`
  - Bad: `GameLogger.LogWarning("Missing bottle")`

## Anti-Patterns
- Null check + silent return on SerializeField fields
- `?.` null-conditional on SerializeField fields that should always be wired
- `GetComponent<T>() ?? fallback` — prefer SerializeField + log if missing
- Try/catch that swallows exceptions without logging
- Returning a default value from an unexpected state without logging
