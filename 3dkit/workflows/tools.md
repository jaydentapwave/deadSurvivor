# 도구 연결 — 에이전트가 쓰는 외부 도구

이전 프로젝트 Deadsurvivor에서 2026-11-02~03에 이 컴퓨터로 확인한 사용법과 함정이다. 버전·경로가 바뀌면 이 표를 고친다. 프로젝트별 경로(설치 위치, 키 파일)는 `3dkit.config.json`과 프로젝트의 `docs/개발환경.md`에 둔다.

| 도구 | 사용법 | 주의 |
|---|---|---|
| Unity CLI (1.0.0-beta.15 확인) | 열린 편집기를 제어한다. `unity status --until-ready --project-path <루트>`, `unity command script eval --project-path <루트> -- --code '<C#>'`, `unity command script run ...`, `unity recompile --project-path <루트>`. 편집기 열기: `unity open <루트>` | **편집기가 여러 개 열려 있을 수 있으니 항상 `--project-path`를 준다.** Claude Unity 플러그인 0.1.6-beta는 옛 명령 이름(`unity command eval`, `editor_play`)을 안내하니 실제 CLI 이름을 쓴다. 로그인하지 않아도 `unity open`은 열린다(경고만) |
| Unity 편집기 화면 | 캡처·성능 측정은 메인 탭이 **도킹된 Game 창**이어야 한다. Device Simulator 모드면 Game 창이 없어 실패한다(`Sequence contains no elements`). 되돌리기: `script eval`에서 `PlayModeView.SwapMainWindow(GameView 타입)` | 새로 연 떠 있는 Game 창은 크기가 맞지 않는다. 게임을 그리는 창이 둘이면 통계가 두 번 잡힌다. 캡처 저장 경로(`capture game_view --save_path`)는 `Assets/` 기준이다 |
| Unity 6.4 API | `FindFirstObjectByType`은 폐기(컴파일 오류) → `FindAnyObjectByType`. `UnityStats`에 `batches`·`trianglesLong`은 없고 `drawCalls`·`setPassCalls`·`triangles`·`shadowCasters`·`frameTime`은 리플렉션으로 읽힌다 | `script run`은 같은 이름의 정적 메서드가 둘이면 진입점을 거부한다 |
| Unity Play 측정 | `PlayerSettings.runInBackground`를 켜고 재고 끝나면 되돌린다 | 끄고 재면 백그라운드에서 3fps 같은 값이 나온다 |
| Tripo CLI (0.8.1 확인) | npm으로 프로젝트 `output/tools/tripo/`에 설치(Git 제외). 환경변수 `TRIPO_HOME=<루트>/output/tripo-state TRIPO_ANALYTICS_ENABLED=0 TRIPO_NO_UPDATE_CHECK=1`. 키는 `.env.local`의 `TRIPO_API_KEY` | 키를 출력·기록하지 않는다. `.env.local`은 Git 제외. 유료 생성은 [Tripo 생성 기준](tripo-generation-standard.md)과 [핵심 규칙](core-rules.md) 7절. 네트워크 오류 뒤 재전송 금지 |
| Codex | `codex exec -s read-only`(검토) 또는 `-s workspace-write`(생성)로 대화 없이 실행. 이미지 생성 도구는 `image_gen__imagegen` | 생성 이미지는 `~/.codex/generated_images/<스레드>/`에 저장된다. 결과 폴더로 복사하라고 지시하고 경로·해시를 보고받는다. 백그라운드 실행 때 `< /dev/null`로 입력을 닫는다. 닫지 않으면 "Reading additional input from stdin..."에서 멈춘다(13분 대기 확인) |
| Blender | `/Applications/Blender.app/Contents/MacOS/Blender`(배경 실행 `-b -P <스크립트>`) | 감량·아틀라스 등 모델 자체를 바꿀 때만 쓴다. 그림자·인스턴싱·묶음은 Unity에서 끝난다 |
| Claude 셸 파일 접근 | 프로젝트 폴더 안은 읽기·쓰기 가능 | `~/Documents` 읽기가 막혀 있다(EPERM). 그곳의 원본 파일이 필요하면 사용자가 프로젝트 안으로 복사한다 |
| 외부 연결 | Codex·Claude 모두 Notion 등 연결이 있을 수 있다 | 프로젝트 AGENTS.md의 저장 경계를 따른다. 개인 프로젝트에서는 쓰지 않았다 |
