# F0 그림 보정 — Claude 판독 요청

- 상태: **needs-review / F0 그림 투입 전 관문**. F0 임포트·데이터, F1~F7은 NOT_RUN. 최종 핸드오프는 리뷰 판정 뒤 작성한다.
- 사용자 지시: “고친 그림은 같은 시트로 다시 열어 보고, 다른 모델 판독은 고친 행만 수정 확인.” Codex가 리뷰 에이전트를 부를 수 없으므로 이 관문에서 Claude 리뷰 세션에 넘긴다. 구현 전 design-reviewer 관문과는 다르다.
- 시작: `d3b01b15badf81361fcbfbe7d12f31cada741cf7`, `claude/hd2d-fx`. 현재 워크트리에서만 썼으며 메인 폴더 그림·코드는 읽기만 했다.
- 적용: `using-unity`, `unity-cli`(integration-advanced.md), `director-run`, `unity-feature`, `2d-art-produce`를 읽었다. ds-workflow의 예전 3D 검증 대신 사용자 지정 2D 단계별 관문을 적용한다.

## 판독 담당에게

담당: **Claude 시각 판독자(읽기 전용, validator-client의 시각 검증 역할)**. 이번에는 F0 아래 기준만 본다. F7 마감 리뷰 묶음(validator-client·spec-reviewer·algorithm-tester)은 아직 요청하지 않는다.

1. 먼저 `output/assets/hd2d-fx/review/decoded-01.png`~`decoded-07.png`와 `motion-01.png`~`motion-07.png`만 본다. 번호별로 무엇으로 읽히는지 첫째·둘째 해석, 움직임을 한 줄씩 기록한다. `blind-map.json`과 파일명에 종류가 담긴 원본은 첫 판독 뒤에만 연다.
2. 다음으로 대응표 `output/assets/hd2d-fx/review/blind-map.json`을 열어 의도와 비교한다. 기준: `docs/features/hd2d-fx/design.md` R1~R5·2절, 사용자 지시의 “디렉터가 본 것” ①~④.
3. 기준별 PASS/FAIL을 적고 마지막에 `VERDICT: PASS` 또는 `VERDICT: FAIL`. 결과 저장 위치는 `output/assets/hd2d-fx/review/hd2d-fx-corrected-read-r001.md`. 범위 밖 새 지적은 후속 제안으로 구분한다.

| ID | 확인할 것 |
|---|---|
| F0-A1 | 회복·파동·폭발 고리가 색 외의 모양으로도 갈리는가(잎·십자 / 겹 고리 / 톱니·점선). 2~3도트 띠와 세 단계 색이 인물 도트 결과 맞는가 |
| F0-A2 | 번개가 흰 심 2도트·색 테·가지·끝 불꽃으로 연결 공격처럼 보이는가 |
| F0-A3 | 피격 별 정점이 인물 키의 약 1/3로 읽히고, 작은 시작→별→쪼개짐이 읽히는가 |
| F0-A4 | 돌진 길이 투사체나 화면 UI가 아닌 바닥의 방향 예고로 읽히며, 따뜻한 낮은 채도 빨강 계열인가 |
| F0-A5 | 추가로 함께 보정된 자폭 고리는 범위 경고로 읽히고 폭발 고리와 구별되는가 |
| F0-A6 | 모든 수정 행의 시간 순서가 자연스럽고, 사라짐이 알파 페이드가 아닌 쪼개짐으로 보이는가 |

## 만든 것과 근거

- Git 밖 메인 폴더의 원본 68 PNG를 워크트리 `output/assets/hd2d-fx/frames/`, `raster/`로 복사. 25장(7종)만 바꿈. 메인 원본 전체 SHA256 불변을 다시 확인했다(`review/state.json`, `input-sha256.json`).
- `tools/raster.py` **r003**: 3단 띠·잎/십자·겹 고리·톱니, 번개 심 2도트와 가지/끝 불꽃. 번개 RNG를 C#에 그대로 옮길 수 있는 xorshift32로 명시. 원본 PCG64와 픽셀이 달라지므로 **판독 채택 뒤 F4/F5의 비교 기준은 이 수정본과 생성 PNG**다. 기존 기대식을 낮춘 것이 아니라 사용자 지정 그림 보정을 반영한 새 기준 후보이며 아직 채택을 주장하지 않는다.
- 돌진/자폭/폭발 바닥의 따뜻한 빨강은 기준서 기존 팔레트 `#77211d #c44334 #e4b67e`. 노랑·원색 빨강을 제거했다. 세운 폭발 그림은 바꾸지 않았다.
- 피격 별은 20×20 캔버스에서 순수 래스터로 재작성. 정점 불투명 상자 16×16 = 48/3. 시작은 2×2에서 8×8로 보강, 끝 장은 이진 알파 파편. 런타임 확대 없음.
- 기준서·킷 도구는 수정하지 않았다. 새 생성 호출·유료 도구 호출 0.

재현(설치된 읽기 전용 `/private/tmp/pyart/bin/python`, Pillow 13.0.0 / numpy 2.5.4):

```sh
/private/tmp/pyart/bin/python output/assets/hd2d-fx/tools/raster.py all output/assets/hd2d-fx/raster
/private/tmp/pyart/bin/python output/assets/hd2d-fx/tools/raster.py star output/assets/hd2d-fx/frames/hit_star
/private/tmp/pyart/bin/python output/assets/hd2d-fx/tools/raster.py selftest
python3 -B Tools/check_sprites.py --palette docs/art/아트-기준서.md --binary-alpha output/assets/hd2d-fx/frames/*/*.png output/assets/hd2d-fx/raster/*.png
/private/tmp/pyart/bin/python output/assets/hd2d-fx/tools/prepare_review.py
```

## 실행 증거와 미실행

| 관문 | 결과 | 근거 |
|---|---|---|
| doctor unity-client.implement | PASS | configured, findings=[] / 설정 확인만 |
| 워크트리 에디터·시작 컴파일 | PASS | editor_ready.py “에디터 준비됨”; unity status: 프로젝트 절대 경로 일치, 6000.4.3f1 / ready / port 7800 / pid 57018 |
| 팔레트·이진 알파 68 PNG | PASS | `review/palette.log`, 실패 0 |
| 래스터 자체 시험 | PASS | `review/raster-selftest.log`, 기존 8 + 보정 기준 14 = 22건. 피격 16×16, 번개 6방향 끝점 보존·결정성, 돌진 원색 제거 포함 |
| APNG 내보내기·디코딩 | PASS | `review/export.log`, 7종 전 프레임 디코딩 색 차 0, 손실 0. 12fps→12.05, 16fps→16.13은 APNG ms 반올림; 게임 fps 변경 아님 |
| 독립 판독 F0-A1~A6 | NOT_RUN | 현재 Claude 리뷰 요청 대상. 자기 시각 확인을 독립 판독으로 세지 않음 |
| F0 임포트·데이터·대상 EditMode | NOT_RUN | 보정 그림 판독 뒤 진행 |
| F1~F7·전체 판정 경로·전체 EditMode | NOT_RUN | 앞 단위 미완료. 전체 검사는 F7에서만 |
| 성능 | NOT_RUN | 사용자 지정 범위 밖 |

**직접 본 것**: 원본 `preview/frames_x4.png`, 수정 `review/row-01.png`~`row-06.png`, APNG를 디코딩한 `review/decoded-01.png`~`decoded-07.png`를 열었다. 파동은 작은 겹 고리가 퍼진 후 호 조각, 돌진은 갈매기가 화살촉 쪽으로 흐름, 피격은 십자→별→파편, 폭발 고리는 톱니가 퍼져 어두운 조각, 번개는 두 갈래가 흔들리고 청록 장 뒤 연결이 끊김, 회복은 잎/십자가 붙은 고리가 퍼진 뒤 끊김, 자폭 예고는 디더 점과 테의 두 장 교대다. **게임 캡처가 아니며 깊이·Bloom·게임 가독성은 미확인**이다. 시트·APNG는 `.ai/2d-art/tools/sheet.py`로만 내보냈다.

## 설계 줄 번호·환경 차이

- `CombatWorldPresenter.DrawBurst`는 설계의 :132가 아니라 :134. Chain은 :141~154, 회복 높이 :170, 범위 :173/:180, 폭발 확대 :177. 기존 동작 설명은 유지된다.
- `MonsterView` 범위 :86~87, 돌진 :192~198. :91은 B6가 넣은 `InitializeShadow`이므로 합칠 때 주의.
- `ProjectileView` 각도 :20, `MonsterViewFactory` 임시 도형 :50~51, `EnemyShotViewFactory` 원 :33, `PlayableProbe` :658~699/:1202~1213은 설계와 대응.
- 데이터 실제 위치: `Assets/Game/03.Resources/Resources/Data/Combat/combat_effects.json`.
- 워크트리에 `.director/outbox/20261118-hd2d-screen-1.md`는 없다. 최종 핸드오프에서 누락 입력으로 기록하고 관련 화면 구조 설계·실제 코드로 대응한다.
- CLI 설치 1.0.0-beta.17, 최신 조회 1.0.1. 전역 도구·설정 변경 금지에 따라 업데이트하지 않았다. 상태 조회·컴파일은 설치 버전에서 성공했다.
- 새 C# 작성 전 검색 결과 `SpriteAtlasService`는 아틀라스 복제 캐시, `PlaceholderSpriteFactory`는 도형 생성/Dispose 책임. 런타임 크기별 효과 캐시는 `Features/Combat/Factory/EffectSpriteCache` 후보. 실제 C#는 아직 만들지 않았다.

## X 결정 대조 / 다음 작업

| 결정 | 사용자 원문 대조(기획-검증-개선.md 5절) | 적용 |
|---|---|---|
| X-1 워크트리 나란히 | “워크트리를 만들어서 서로 병렬작업을해도 되고” | 이 워크트리·에디터만 사용 |
| X-2 투사체 그림자 | HD-2D 전부 교체 + 화면 구조 설계의 바닥 점 | 넣음, F3/F6에서 구현 예정 |
| X-3 설계 발광 값 | 효과 모두 교체, 성능은 마지막 | 2.4 / 바닥 1.6~2.2 / 적 1.6 유지 예정, 볼륨 안 바꿈 |
| X-4 옛 파일 보관·게임 참조 0 | “이펙트나 프로젝트타일등도 모두 교체” | 옛 그림 유지, 게임 참조 0은 F7에서 실제 계수 예정 |

리뷰 PASS 뒤 F0 임포트·데이터→컴파일·지정 EditMode→F0 커밋, F1~F7 순서로 계속한다. F7 리뷰까지 끝난 뒤 지정 outbox 핸드오프를 쓴다. F0 그림 보정 커밋을 F0 전체 완료로 세지 않는다.

## 놓친 것·비용 기록

- 원본 피격 정점은 이미 바깥 불티 포함 18×18이지만 첫 장이 2×2였다. 캔버스 크기만으로 가독성을 판정하면 놓친다. 중심 덩어리·단계별 실루엣과 실제 게임 연속 캡처를 함께 본다(워크플로우 후보: 설계 9절의 기존 프레임 판정에 이 원인을 합치기, 최종 핸드오프에서 디렉터에 전달 예정 / 아직 미반영).
- `check_sprites.py` 최초 호출에 `frames/*`를 주어 `cut_meta.json`까지 PNG로 검사해 1건 실패했다. PNG만 지정해 68/68 PASS. 검사 기준 변경 없음.
- 기본 python3에 PIL 없음. 새 `/tmp` venv ensurepip 실패 뒤, 이미 설치된 `/private/tmp/pyart/bin/python`으로 변경. 프로젝트 패키지·전역 설정은 바꾸지 않았다.
- `Tools/Harness/session_time.py --markdown` 실행 FAIL: 이 Codex 워크트리에 Claude 세션 jsonl이 없어 `max() arg is an empty sequence`. 현재 Codex 토큰 집계는 제공되지 않아 NOT_AVAILABLE, 하위 에이전트 0. 수치 추정 금지. 그림 수정·검사 활동의 파일 생성 시각부터 경과는 `review/state.json`에 기록. 그 앞 규칙 읽기·preflight 시간과 호스트 토큰은 자동 집계 불가다.
- 검사 추가 14건은 보정한 도트 크기·색·끝점만 대상이다. 전체 EditMode/플레이/성능 반복을 추가하지 않았다. 기존 8 자체 시험·전체 F7 관문은 유지.
