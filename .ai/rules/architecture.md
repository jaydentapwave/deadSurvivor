# Architecture Rules

## Principles
- Keep MonoBehaviour thin
- Prefer constructor injection
- Avoid hard dependencies
- Avoid bidirectional dependencies
- Each class must have one clear responsibility

## Layer Rules
- View: rendering and input only
- Model: state only
- Service: domain logic and state changes
- Presenter/Controller: coordination and flow
- Factory: creation and initialization
- Registry: runtime instance tracking
- EventBus: limited cross-system messaging only
- Interface: interface definitions only
- Data: SheetData or GameData only

## Dependency Rules
- Depend only on what is directly needed
- Prefer explicit parameters, `Action`, `Func`, or small interfaces over broad shared dependencies
- Low-level classes must not know high-level flow
- View must not modify Model directly
- Model must not depend on View
- Service must not depend on View
- Do not inject `EventBus` into low-level classes when callback flow is enough
- Prefer explicit callback flow over `EventBus`

## Flow Rules
- Parent coordinates, child reports
- Child classes report facts, not decisions
- Low-level classes must not control global flow
- Handle orchestration in Presenter, Controller, or Service
- Do not use `EventBus` as a shortcut for poor structure

## Anti-Patterns
- No business logic in View
- No direct View -> Model mutation
- No default use of `EventBus` for local communication
- No mixing creation logic with flow control
- No child class controlling cross-system behavior
- No broad manager class with many responsibilities
- No mutable `static` state and no `public static` API outside of `Utils` and `Const` classes
  - 허용: private static 순수 헬퍼(인스턴스 상태 미사용), 불변 데이터 타입의 static factory(`ApiResult.Success` 류), `Shader.PropertyToID`/`Animator.StringToHash` 캐시 등 Unity 관례, `[MonoPInvokeCallback]`·`[DllImport]`처럼 플랫폼이 static을 강제하는 경우
