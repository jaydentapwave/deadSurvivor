# HD-2D 3D 배경 나머지 — 나무·덤불·바위·돌·돌담·풀·꽃 (2026-11-18)

- 근거: 사용자 "나무까지 전부 3D", 기준서 r7 승인 "승인, 이 방향으로 진행"(시범 방향 포함). 시범 `../hd2d-3d-pilot/` 방법을 그대로 이음(크레딧 0 Blender 손 모델 + 도트 알베도). Unity·`Assets/`·git은 건드리지 않음.
- 쓴 크레딧 **0**(Tripo 안 씀 — 손 모델로 판독 통과). 카탈로그 예산 `hd2d_3d_full` cap 300 기록.
- 개수는 지금 배치 `field_layout.json`(seed 8)의 카드 종류: tree_02(51) · bush_01/02(35) · boulder_01(5) · rock_01/02(stone 26, shore 91, wall_0~8 33) · grass_tuft_01/02(293) · flower_01~03(160).
- 다시 만들기(작업 폴더 = 이 폴더): `Blender -b --factory-startup --python-exit-code 1 -P tools/build.py -- <kind> $PWD/<kind>` → `(cd tools; /usr/bin/python3 paint.py ../<kind>)` → `Blender ... -P tools/export.py -- $PWD/<kind>` → `/usr/bin/python3 tools/preview.py` → `/usr/bin/python3 tools/manifest.py judge/r1-a/anchor-results.json judge/r1-b/anchor-results.json judge/r2/anchor-results.json`.
- 시범 원본 복사: `pilot-copy/`(tree_v1_pilot·bush_v1_pilot·boulder_r2_pilot). 1회차 판독에서 떨어진 것의 1회차 결과: `r1-archive/`.

## 결과 [측정: mesh.json·palette.json]

| id | 삼각형(예산) | 폭×깊이×높이 m | 알베도 | 색 |
|---|---|---|---|---|
| tree_v1 | 472(600) | 1.505×1.098×1.690 | 256² | 잎 3 + 줄기 2 |
| tree_v2 | 532(600) | 1.096×0.988×2.062 | 256² | 같음 |
| tree_v3 | 482(600) | 1.990×1.094×1.599 | 256² | 같음 |
| bush_v1 | 280(300) | 1.205×0.882×0.662 | 256² | 잎 3 |
| bush_v2 | 200(300) | 0.816×0.622×0.494 | 256×128 | 잎 3 |
| boulder_v1 | 70(300) | 1.197×0.803×0.797 | 128² | R0·R1·R2 |
| boulder_v2 | 28(300) | 0.649×0.473×0.475 | 128×64 | 같음 |
| stone_v1 | 27(120) | 0.498×0.363×0.247 | 64² | 같음 |
| stone_v2 | 30(120) | 0.560×0.342×0.268 | 64² | 같음 |
| wall_a | 180(200) | 1.000×0.300×0.513 | 256×128 | 같음 |
| wall_b | 170(200) | 1.000×0.300×0.521 | 256×128 | 같음 |
| grass_v1 | 20(40) | 0.228×0.225×0.191 | 64² | #3a3721 #4f7a2e #7fa040 #a8c828 |
| grass_v2 | 26(40) | 0.271×0.277×0.258 | 128×64 | #3a3721 #4f7a2e #7fa040 #b3a954 |
| flower_white | 38(40) | 0.375×0.356×0.277 | 128×64 | 줄기 #4f7a2e #7fa040, #fff9ed #f8b828 |
| flower_yellow | 38(40) | 0.375×0.350×0.277 | 128×64 | 줄기 2 + #ffe08a #f8b828 #d5833a |
| flower_purple | 38(40) | 0.375×0.358×0.277 | 128×64 | 줄기 2 + #b07fd0 #7a4a9e #f8b828 |

- 잎 #b3ca7b·#5c8d73·#49746b, 줄기 #774c3b·#5c493c(시범·K23), 바위·돌·돌담 #d8d3a9·#9d9778·#403d2a(K22). 풀·꽃은 지금 `grass_tuft_01/02`·`flower_01~03`에서 잰 색.
- UV는 48텍셀/m로 만든다. palette.json의 `density_texel_per_m`(50~74)은 가는 섬 가장자리 텍셀까지 세는 측정이라 작은 에셋일수록 부풀려진다.
- 피벗 바닥 가운데(돌담은 x 가운데 = 토막 가운데, 양 끝 x=±0.5 → 1.0m 간격으로 이으면 맞물림, `wall_*/preview_run.png`), +Y 위, FBX 축 시범·다리와 같음. 재질 1, 알베도 Closest.
- 매끈 음영: 잎(꼭짓점 법선), 바위·돌(각도 48° 넘는 꺾임만 날카롭게 — `set_sharp_from_angle`). 줄기·돌담·풀·꽃은 각진 음영.

## 시범에서 바뀐 것

- 잎 칠: 연두 잎(L0)은 하늘을 보는 덩어리 윗부분에서만(시범의 0.17m 무작위 구역 = 판독 "위장 무늬 노란 얼룩" → 뺌). 나무 v1 형태는 시범 그대로, 칠만 바뀜.
- 덤불: r1 = 큰 덩어리 + 꼭짓점 톱니 → 판독 Claude style 2("이끼 낀 바위, 매끈한 둥근 윤곽") → **r2(방법 바꿈) = 작은 잎 뭉치(20삼각) 14/10개 무더기**. 높이가 시범(0.745)보다 낮아짐(0.66/0.49) — 2D bush_01(0.98m)보다 낮다.
- 큰 바위: r3(점 14, 매끈 음영, 바탕 한 색 + 금) → 판독 style 2/1("넓고 매끈한 로우폴리 면") → **r4(방법 바꿈) = 점 24 + 금 밀도 14 + 땅 닿는 2텍셀 R1 띠** → 4/3. 알베도에서 칠 단계를 나누지 않아 셰이더 3단 띠만 보인다(판독 r2 "4단 넘는 잔 질감" 대응). 작은 큰 바위·돌 v2는 1회차에 통과.
- 작은 돌 v1: 1회차 Claude 2 → 점 12·금 10·아래 띠로 바꿈 → 4/3.
- 돌담: 1회차 wall_a 2/1("검은 얼룩" = 앞뒤로 튀어나온 벽돌의 자기 그림자, R2 줄눈) → 앞뒤 들쭉날쭉 빼고 줄눈 한 색(R1), 꼭대기 들쭉날쭉 크게 → 둘 다 3/3.
- 풀·꽃: 처음 가는 잎(0.04m)은 렌더에서 전부 갈색 외곽선만 남아 판독 전에 바꿈(가운데 뿔 + 넓은 잎 0.07m+, 꽃송이 지름 0.17~0.19m).

## 판독 (두 모델 눈가림, `Tools/hd2d_judge.py anchor`)

- 입력 `*/judge_view.png`: 영웅 bolt_down(발밑 하드 그림자 타원) + 3D 요0°·40°, 회색 올리브 땅(#8e8c6a) + 하드 땅 그림자. 결과 `judge/r1-a`, `judge/r1-b`(1회차 16개), `judge/r2`(바꾼 6개). 기록 `judge-*.log`.
- 최종 style(Codex/Claude): 나무 3/3·3/3·3/3, 덤불 3/3·3/3, 큰 바위 4/3·4/3, 돌 4/3·3/3, 돌담 3/3·3/3, 풀 3/3·3/3, 꽃 4/3·4/3·4/3. **16개 모두 두 판독자 style ≥ 3.**
- 남은 큰 결함 표시(Claude 1): tree_v2·tree_v3·stone_v1·wall_a·grass_v1/2·flower_white — 판독 문장의 큰 결함은 모두 "바닥이 단색 평면/디오라마 바닥 없음"·"인물 발밑 그림자"(미리보기 바탕), 에셋 자체 지적은 작은 결함으로 남음.
- 에셋 자체에 남은 작은 지적(후속 후보): 잎 청록이 올리브 들판에서 차갑다(K23 색 그대로), 외곽선이 검게 보인다(실제는 #34140e 한 색 — 어두운 잎·그늘 옆, 추정), 요40° 돌담 윗모서리 계단·벽돌 줄 기울어 보임, 바위 앞면 명암이 띠보다 조금 부드럽게 보임, 덤불 윗면 일부 다각형 결.

## 본 그림(직접 열어 봄)

- `compare.png`(최종, 16행: 영웅 · 3D 요0° · 요40° · 지금 2D 판, 땅·그림자 포함): 나무 3종은 2D보다 덩어리가 둥글고 줄기가 굵게 드러남, 크기는 영웅 대비 2D와 비슷. 덤불은 잎 뭉치 톱니로 깨짐. 바위·돌은 밝은 윗면/중간/그늘 3단. 돌담 벽돌 줄 보임. 풀·꽃은 2D 판과 크기 비슷, 색 구분됨.
- `wall_a/preview_run.png`: 3토막 이음 — 요0°·40° 모두 이음매 안 보임.
- 판독 입력 묶음(덤불·바위·돌·돌담 r2, 꽃·풀 r2)을 크게 열어 봄.

## 미확인

- Unity 배경 도트화 패스에서의 실제 모습(텍셀–픽셀 정렬, 실제 해 방향, FieldSurface 셰이더 띠가 미리보기와 같은지, FBX의 각도 기준 매끈 법선이 그대로 들어가는지, 외곽선 문턱 — 미리보기는 시범과 같은 깊이 차 0.25m, 기준서 K19는 0.055).
- 풀·꽃 약 450개 인스턴싱 비용, 바람 흔들림, 여러 개 배치·돌림·배율, 물가 돌로 돌려 쓰기, 사용자 채택.
- 컨셉 다른 계열 검수 없음(유료 생성 입력 아님).

## 돌담 r3 — 4부 (2026-11-18)

- 사용자 지정 wall_a·wall_b만 재제작. 이전 두 자산 전체는 `r2-wall-archive/`에 보존. 나머지14종 바이트 불변.
- 원인 실측: atlas256×128 전체가 벽 폭1m에 대응하는 구조가 아니며 실제 UV밀도는 두 담 모두48texel/m, 바위·돌47.34~47.40texel/m. 기존 앞뒤 수직면 법선72/68삼각이 모두 안쪽이었다. 텍셀 밀도를 낮추지 않고 같은48/m를 유지하며 바깥 법선·두 줄 큰 돌·넓은 윗면 모따기로 교체했다. 외곽선은 기존 배경 깊이 패스 #34140e이며 셰이더는 변경하지 않았다.
- r3: wall_a94삼각·128²·1×.30×.513m, wall_b114삼각·128²·1×.30×.521m. 바닥 중앙 피벗, x끝±.5m. 팔레트R0/R1/R2 동일. wall_b 첫 판독 Codex style2(넓은 단색 면) → 큰2텍셀 계단 밝은 조각·짧은 금 추가, 수정 확인 Codex/Claude3/3. wall_a는첫3/3유지. 최종 결속 `judge/r3-wall/anchor-results.json`, wall_b원문 `judge/r3-wall-fix/anchor-results.json`.
- 이 실행 환경의 Blender5.2는Metal초기화에서 종료하고 임시bpy4.2도 시작하지 못해 **wall-only CPU 경로**를 사용했다. 같은 `tools/build.py`/`paint.py`/`export.py`/`preview.py` 진입점을 쓰며 CPU기하·내보내기만 `tools/wall_cpu.py`. FBX는 보존한Blender7400 계층·축·단위를 유지하고 기하/법선/UV만 바꾼다. 새 CPU 원본은 `mesh.json`; 현 폴더의 `work.blend`는이전r2참고이며 새출력원본이 아니다.
- 재현(작업폴더=this): `/usr/bin/python3 tools/build.py --cpu-wall wall_a "$PWD/wall_a"` → `/usr/bin/python3 tools/paint.py wall_a` → `/usr/bin/python3 tools/export.py --cpu-wall "$PWD/wall_a"`; wall_b도같음 → `/usr/bin/python3 tools/preview.py wall_a,wall_b` → `/usr/bin/python3 tools/manifest.py judge/r1-a/anchor-results.json judge/r1-b/anchor-results.json judge/r2/anchor-results.json judge/r3-wall/anchor-results.json`.
- 게임 반영은 기존T0 `HD2DSceneryMigration.Import()` 두 번. 비대상14종/166임포트파일 반복해시 대조, 실제Unity크기·삼각형 확인. 성능·전체검사는이단계범위밖.
