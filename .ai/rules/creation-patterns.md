# Creation Patterns

## Creation Rules
- Separate creation from business logic
- Use Factory when creation has dependencies, setup steps, or reuse
- Service may request creation, but non-trivial creation should live in Factory
- Use Registry when created objects must be tracked

## Factory Pattern
- Factory encapsulates complex object construction
- Factory calls PoolManager to get or create instances
- Factory sets up slots, positions, and initial state
- Service calls Factory — Factory does not call Service

### Example flow
```
BottleSpawnService
  → BottleFactory.CreateBottle()
    → PoolManager.Get<BottleController>()
    → BottleSlotController setup
    → BottleRegistry.Register()
```

## Registry Pattern
- Registry tracks live instances by key (usually index)
- Register on spawn, unregister on despawn
- Used for fast index-based lookup without scene traversal

## Pool Pattern
- PoolManager wraps Unity ObjectPool per resource key
- Get: pull from pool or instantiate if empty
- Release: return to pool (do not destroy)
- Reset state on Get, not on Release
