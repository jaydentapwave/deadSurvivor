# Code Style

## Rules
- Use Allman braces
- Always use braces for `if`, `else`, `for`, `foreach`, `while`
- Never use single-line control statements
- Add spaces after keywords and around operators
- Use `_camelCase` for private members only
- Use `camelCase` for local variables and parameters
- Use PascalCase for types, methods, and public members
- Collection suffix rules:
  - `List<T>` -> `List`
  - `HashSet<T>` -> `Set`
  - `Dictionary<TKey, TValue>` -> `Dict`
- Prefer expression-bodied members for simple one-line properties and methods when readability is clear

## Naming
- UI classes (MonoBehaviour View) must use `UI_` prefix (e.g. `UI_ItemView`, `UI_StageView`)
- `UI_` View는 갱신 진입점을 `UpdateUI(...)` 하나로 둔다. 표시에 필요한 데이터를 인자로 받아 한 번에 모든 표시 요소를 갱신하고, presenter는 `UpdateUI(...)` 호출 후 팝업을 연다. 표시 항목별 `SetXxx` 메서드를 늘리지 말 것. (단순 reactive 바인딩이 더 자연스러운 경우는 예외)
- Event structs must be declared inside `GameEvent.cs` — never in separate files
- Async methods end with `Async` suffix
- Event handler methods must use `On` prefix and name the source event, not the action they perform (e.g. `OnPurchaseClicked`, `OnHomeClicked`, `OnStageCleared`) — applies to Unity callbacks, UI button handlers, `EventBus` subscribers, and `Action`/`Func` callbacks wired to a presenter/controller. The `On` prefix marks "this runs in response to something"; the body decides what to do.

## Vocabulary Consistency

새 변수·함수·타입 이름을 짓기 전, 코드베이스에서 같은 의미가 어떻게 표현되는지 grep으로 확인한다. 같은 의미면 기존 어휘를 그대로 사용.

### Principles
- 외부 도메인 용어 (industry generic, 학술 자료 표현, 일반 영어 단어)를 무비판적으로 도입하지 않는다
- 같은 의미를 표현하는 기존 어휘가 있으면 그것을 그대로 사용
- 새 용어 도입은 의미상 명확한 차별점이 있을 때만

### Anti-Patterns
- 자기 사고 흐름에서 자연스럽게 떠오른 외부 용어를 그대로 코드에 도입 (예: "palette" — 코드베이스에 없는 일반 용어)
- 같은 개념을 두 단어로 동시에 표현 (`variantCount` ↔ `paletteSize` 혼재)
- 학술·외부 자료의 용어를 검토 없이 가져오기
- 약어/풀네임 혼용 (`ctx` ↔ `context` 혼재)

### Required Behavior
- 새 이름 짓기 전 grep: "같은 의미를 가진 단어가 이미 코드에 있는가?"
- 동의어 후보가 떠오르면 (palette/variant, stage/level, play/run, session/context, item/entry 등) grep으로 어느 쪽이 이미 쓰이는지 확인 후 결정
- 의미상 차별점 없으면 기존 어휘 따름. 새 용어는 진짜 다른 개념일 때만

### Good
```csharp
// 코드베이스가 SlotVariantData / Theme.Variants / ResolveVariant 어휘 사용 중
var variantCount = stage.Theme.Variants.Count;   // 기존 어휘 그대로
```

### Bad
```csharp
// 같은 코드베이스에서 "palette" 신조어 도입
var paletteSize = stage.Theme.Variants.Count;    // 외부 도메인 용어 (코드베이스에 'palette' 없음)
```

## Boolean Naming
Bool-returning methods, properties, and local variables must use a positive predicate prefix and read as "true = expected/normal state".

### Prefix rules
- `Is*` — state or attribute (`IsBottleSorted`, `isFromFull`, `isCleared`)
- `Can*` — capability or permission (`CanPour`, `CanContinue`, `canReversePour`)
- `Has*` — possession or membership (`HasItem`, `hasGimmick`)
- `Should*` — policy or decision (`ShouldRetry`, `shouldRebuild`)

### Positive bias
- True must mean the **expected/normal/desired** state. Callers usually negate at the call site (`if (!CanContinue) Fail()`), which reads more naturally than the inverted form.
- Avoid negative-named bools (`IsDeadlocked`, `isInvalid`, `hasNoMove`). They produce double negatives at the call site (`if (!IsDeadlocked)`) and obscure intent.

### Specificity
- Avoid vague qualifiers like `Valid`, `OK`, `Ready` unless the surrounding type makes them unambiguous. Prefer the concrete predicate (`IsProgressingPour` over `IsValidPour`, `CanContinue` over `HasAnyValidMove`).
- Match the noun to the actual subject. `isFromFull` is clearer than `oldFromFull` when `from` already refers to the pre-state in context.

### Good
```csharp
public bool CanContinue(IReadOnlyList<BottleData> bottleDataList)
public bool IsBottleSorted(BottleData data)
var canReversePour = ...;
var isFromFull = from.BottleSlotDataList.Count >= from.SlotCapacity;
var isToEmpty = to.BottleSlotDataList.Count == 0;
```

### Bad
```csharp
public bool HasAnyValidMove(...)        // "Valid" is vague
public bool IsDeadlocked(...)           // negative; forces double-negative at call sites
var oldFromFull = ...;                  // missing is/can/has prefix
var newFromCanFeedReverse = ...;        // verbose; redundant prefix when context implies subject
```

## Member Order
1. Properties
2. `[SerializeField]` fields
3. Public fields
4. Protected fields
5. Private fields

## Method Order
1. `Initialize`
2. Unity event methods
3. User-defined event methods
4. Event-related handler methods
5. Public methods
6. Private methods

## Good
```csharp
public IReadOnlyReactiveProperty<bool> IsCleared => _isCleared;
[SerializeField] private BottleView bottleView;
private List<ItemData> _itemDataList;
var firstColor = data.Slots[0].Color;
for (var i = 1; i < data.Slots.Count; i++)
```

## Bad
```csharp
private List<ItemData> itemDataList;
var _firstColor = data.Slots[0].Color;
for (var _i = 1; _i < data.Slots.Count; _i++)
```
