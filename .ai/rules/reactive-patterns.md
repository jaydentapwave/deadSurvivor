# Reactive Patterns (R3 / UniTask)

> 2026-08 UniRx → R3 마이그레이션 완료 (epic/migration-r3). UniRx API를 쓰지 말 것.

## R3 Rules

### ReactiveProperty vs Subject
| Use | When |
|-----|------|
| `ReactiveProperty<T>` | State that has a current value and emits on change |
| `Subject<T>` | One-time events with no persisted value |
| `ReadOnlyReactiveProperty<T>` | Expose state as read-only to outside classes — **클래스다** (UniRx의 `IReadOnlyReactiveProperty` 인터페이스는 없음). 파생 노출은 `.ToReadOnlyReactiveProperty()` |

### Read / Write
- 읽기는 **`.CurrentValue`** (UniRx `.Value` 상당 — R3 리네이밍의 최다 빈도 변경점)
- 쓰기는 `ReactiveProperty<T>.Value = x`
- `ReactiveProperty`는 `IDisposable` — 소유자(Model)가 Dispose 책임

### CompositeDisposable
- Declare `_disposables = new CompositeDisposable()` in each class that subscribes
- Add subscriptions with `.AddTo(_disposables)`
- Dispose in `Dispose()` or on scope cleanup
- 구독은 view 라이프타임에 바인딩 — view가 붙는 구독은 `.AddTo(_view)` (destroyed view 갱신 사고 방지)

### Observable Patterns
```csharp
// Model exposes read-only reactive state
public ReadOnlyReactiveProperty<bool> IsCleared => _isCleared;

// 파생 상태 노출
return reactive.Select(count => count >= total).ToReadOnlyReactiveProperty();

// Presenter subscribes and reacts
_model.IsCleared
    .Where(cleared => cleared)
    .Subscribe(_ => OnCleared())
    .AddTo(_disposables);
```

### 함정 (UniRx 시절과 동일하게 유효)
- 같은 값 재설정은 dedup되어 발화하지 않음 — 재진입 흐름 주의
- fire-and-forget 경로의 예외 침묵 — 고위험 경로 catch + 리포트

## UniTask Rules (R3 전환과 무관 — 그대로 유지)

### UniTask vs Coroutine
| Use | When |
|-----|------|
| `UniTask` | Async logic in non-MonoBehaviour (Service, Presenter) |
| `Coroutine` | Only inside MonoBehaviour |
| `async UniTaskVoid` | Fire-and-forget async methods |

### Cancellation
- Pass `CancellationToken` for long-running operations
- Use `this.GetCancellationTokenOnDestroy()` in MonoBehaviour
- Cancel on state exit in GameState classes

## Naming
- Async methods end with `Async` suffix
- Observable properties use noun or adjective names, not verbs
