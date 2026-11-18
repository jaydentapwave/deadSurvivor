# Dependency Injection Patterns (VContainer)

## Registration Rules
- Register all non-MonoBehaviour classes via constructor injection
- Register MonoBehaviour instances via `RegisterComponentInHierarchy` or `RegisterComponentOnNewGameObject`
- Entry points (classes that need lifecycle hooks) use `RegisterEntryPoint<T>()`

## Lifetime Rules
- **이 프로젝트 표준은 `Lifetime.Scoped`** — services, models, presenters, registries 전부 씬 LifetimeScope에 Scoped로 등록 (CLAUDE.md DI Root 규칙과 동일)
- `Lifetime.Singleton`은 씬 전환을 넘어 살아야 하는 루트 스코프 대상에만
- Do not use transient unless objects are truly stateless and short-lived

## VContainer Lifecycle Interfaces
| Interface | When to use |
|-----------|-------------|
| `IInitializable` | One-time setup after injection |
| `IStartable` | Start logic equivalent to Unity Start() |
| `ITickable` | Per-frame update logic |
| `IDisposable` | Cleanup on scope disposal |

Use `IInitializable` for most setup. Use `IStartable` only when Unity scene objects must exist first.

## Constructor Injection Rule
- Always prefer constructor injection over field/property injection
- Do not use `[Inject]` on fields unless injecting into MonoBehaviour
- MonoBehaviour uses `[Inject]` method injection when needed

## GameSceneLifetimeScope Pattern
```csharp
// Register models
builder.Register<BottleModel>(Lifetime.Scoped);

// Register services
builder.Register<BottleRuleService>(Lifetime.Scoped);

// Register presenters as entry points
builder.RegisterEntryPoint<BottlePresenter>();

// Register MonoBehaviour
builder.RegisterComponentInHierarchy<BottleController>();
```

## Dependency Direction
- LifetimeScope only knows about registration
- High-level classes (Presenter, Service) depend on low-level abstractions
- Low-level classes must not receive high-level classes as dependencies
