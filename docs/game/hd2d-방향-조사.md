# 2.5D·HD-2D와 비스듬한 시점 액션 게임의 스프라이트 방향 수 조사

작성일: 2026-11-18 · 웹 조사(영문·일문 검색, 개별 페이지 확인)

## 표시 규칙

- **[확인]**: 출처 본문(개발자 인터뷰, 원본 코드, 제작 문서, 아트 분석 글)에서 직접 확인함.
- **[확인·요약]**: 검색 결과 요약에서만 확인함. 본문은 403 등으로 열지 못함.
- **[추정]**: 출처가 없음. 일반 관행이나 플레이 기억에서 끌어낸 것으로, 검증이 필요함.

## 먼저 알아 둘 한계

HD-2D 게임(Octopath 1·2, Triangle Strategy, Live A Live, DQ3 HD-2D, Eiyuden, Sea of Stars)의 **방향 수를 적은 개발자 발언이나 스프라이트 시트 문서는 찾지 못했다.**
- Spriters Resource는 자동 접근을 막았다(403).
- 일본어 인터뷰(gamer.ne.jp, famitsu, inside-games)에는 방향·패턴 수 이야기가 없다.

그래서 1절의 게임별 방향 수는 대부분 **[추정]**이다. 직접 확인하려면 플레이 영상에서 대각선으로 움직일 때 쓰이는 장면을 보거나 시트 아카이브를 사람이 열어 봐야 한다.

---

## 1. HD-2D·2.5D 게임

| 게임 | 카메라 | 캐릭터(필드) | 전투 | 적 | 근거 |
|---|---|---|---|---|---|
| Octopath Traveler 1 | **고정**(필드) | 추정 4방향 이상(대각선 장면은 불확실) | 옆에서 비스듬히 본 3/4 시점, 한쪽만 봄(아군은 왼쪽, 적은 오른쪽을 봄) | 전투용 한 장, 한쪽 방향 | 카메라 고정은 [확인]. 방향 수는 [추정] |
| Octopath Traveler 2 | 고정 | 추정 1편과 같음 | 1편과 같은 형식 | 한쪽 방향, 주요 적은 매우 크게 그림 | 큰 적은 [확인]. 방향은 [추정] |
| Triangle Strategy | **360° 회전** | 추정: 전술 RPG 관행대로 대각 4방향(그린 장면 2개 + 반전) | 같은 맵 위에서 싸움 | 추정 4방향 | 회전 카메라와 "모든 각도에서 보기 좋게 만드는 비용"은 [확인]. 방향은 [추정] |
| Final Fantasy Tactics(비교용) | 회전 | 향하는 방향을 4값으로 저장(E·N·W·S) | 같음 | 같음 | [확인·요약] |
| Live A Live HD-2D | 고정 | 원작처럼 직선으로 움직이고 90°로 꺾음(대각선 이동 없음) | 격자 전투 | — | 원작과 "똑같이 움직이고 행동한다"는 [확인·요약]. 4방향은 [추정] |
| Dragon Quest III HD-2D | 고정 | 추정 4방향 이상 | 정면을 보는 1인칭 전투. 적은 정면 한 장 | 정면 1방향 | DQ 전투 형식에서 나온 [추정] |
| Eiyuden Chronicle: Hundred Heroes | **자유롭게 움직임** | 미확인 | 전투 때 아군은 등을 보임 | 적은 도트가 아닌 일러스트 | 카메라 이동과 등 돌린 전투는 [확인·요약]. 방향은 미확인 |
| Sea of Stars(순수 2D 비교용) | 고정 | 미확인 | — | — | 자료 없음 |

정리
- **회전하는 카메라**(Triangle Strategy, FFT 계열): 그린 장면 2개(앞 3/4, 뒤 3/4)에 좌우 반전을 더한 **대각 4방향**이 업계 전통이다. FFT가 향하는 방향을 4값으로 저장하는 것이 그 근거다[확인·요약]. 다만 Triangle Strategy의 실제 장수는 [추정]이다.
- **고정 카메라**의 JRPG 전투(Octopath, DQ3): 적은 **한 방향**(옆 3/4나 정면)만 있다. 대신 크게 그리고 동작에 공을 들인다[추정 — 시점 관찰].
- 생산 관점의 근거[확인]: 같은 시점을 고정해 두면 그림에 더 많은 공을 들일 수 있다. Super Contra의 보스는 "고정 시점 하나라 공을 더 들였다"(cxong).

## 2. 적이 많은 탑다운·아이소 액션/서바이벌

| 게임 | 시점 | 플레이어 | 적 | 근거 |
|---|---|---|---|---|
| Diablo 1 | 아이소, 고정 | **8방향** | **8방향** | devilutionX 소스의 `Direction` 열거형에 8값(S, SW, W, NW, N, NE, E, SE)이 있음 [확인]. 도구로 CL2를 저장할 때 group=8 [확인·요약] |
| Diablo 2 | 아이소, 고정 | 8방향(일부 동작은 16방향이라는 말이 있으나 미확인) | **8방향**(DCC를 "directions 8"로 저장, 순서 LD·LU·RU·RD·D·L·U·R) | d2mods 변환 튜토리얼 [확인]. 플레이어 16방향은 [추정] |
| Diablo 계열 일반 | — | 3D로 렌더한 뒤 스프라이트로 구움 → 방향 수를 늘려도 그리는 비용이 거의 들지 않음 | 같음 | cxong [확인] |
| Hades | 고정 아이소풍 | 3D로 모델링·애니메이션한 뒤 **여러 각도에서 수천 장을 미리 렌더**. 실시간 3D가 아님 | 같은 파이프라인 | 개발사 설명 [확인·요약]. 정확한 각도 수는 미확인 |
| Vampire Survivors | 탑다운 | 추정 **좌우 2방향(반전)** | 추정 **2방향(반전)**, 일부는 방향이 고정됨(위키 항목 "Fixed Direction") | 위키 항목명만 [확인·요약]. 반전 방식은 [추정 — 플레이 관찰] |
| Nuclear Throne, Forager | 탑다운 | 좌우 한 줄 + 반전 | 같음 | 개발자 포럼 언급 [확인·요약] |
| Zelda: A Link to the Past(비교용) | 3/4 오블리크 | **4방향 그림, 8방향 이동** | — | slynyrd [확인] |
| Super Mario RPG(비교용) | 아이소 | 주인공 일행은 8방향, 대부분 인물은 아이소 4방향 | — | cxong [확인] |
| Gauntlet 2 | 8방향 | 8방향 다 그림(8방향으로 쏘는 것이 핵심이라서) | — | cxong [확인] |

적이 많을 때 방향을 줄이는 관행
- 서바이벌류(Vampire Survivors류)는 적을 **좌우 반전 2방향**으로 처리하는 것이 사실상 기본이다[추정 — 장르 관찰과 포럼 관행]. 적은 늘 플레이어 쪽으로 오므로 "가까이 다가오는 앞모습"만 있어도 어색하지 않다.
- 반전은 엔진의 FlipX 같은 기능으로 거의 공짜다. "좌우를 처리하는 가장 흔한 방법은 코드로 뒤집는 것"[확인·요약, itch.io].
- 8방향이 필요하면 손으로 그리지 않고 3D에서 렌더해 구웠다(Diablo, Hades)[확인]. 손으로 그리는 2D에서는 방향 수가 곧 비용이다.

## 3. 방향 방식별 장단점

비용 기준(cxong, 탑다운 1장 = 1배)[확인]
- 옆 시점 1장 + 반전: 1~1.5배
- 아이소 4방향(앞·뒤 2벌 + 반전): **2~2.5배**
- 오블리크 3/4, 정방향 4방향(옆·앞·뒤 3벌, 옆은 반전): **3~3.5배**
- 8방향(정방향 3벌 + 대각 2벌, 나머지 반전): **5~5.5배**
- 반전을 쓰지 않으면 8방향은 8벌이다. slynyrd는 비대칭 캐릭터라 8벌을 다 그렸다[확인].

총 작업량은 **방향 벌 수 × 동작 수 × 동작당 프레임 수**다. 예를 들어 동작이 대기·이동·공격·피격·사망 5개이고 동작당 6프레임이면:
- 2벌(대각 4방향): 60장
- 3벌(정방향 4방향): 90장
- 5벌(8방향): 150장

| 방식 | 그릴 벌 수 | 장점 | 단점 |
|---|---|---|---|
| 2방향(앞 3/4 + 반전) | 1 | 가장 쌈. 수백 마리 적에 적합 | 위로 올라가도 앞모습을 보임. 자유 이동에서 뒷모습이 없음 |
| 대각 4방향(앞3/4·뒤3/4 + 반전) | 2 | 아이소·비스듬한 시점에 자연스러움. 정방향 4방향보다 쌈 | 정확히 위·아래·옆으로 갈 때도 대각 모습. 방향을 가르는 경계(대략 0°·90°·180°·270°)에서 모습이 튐 |
| 정방향 4방향(앞·뒤·옆 + 반전) | 3 | 탑다운·3/4 RPG의 표준. 4방향 그림으로 8방향 이동도 가능(LttP) | 대각선으로 움직일 때 옆이나 위아래로 붙어 어색함. 비스듬한 카메라에서는 "정면"이 덜 입체적임 |
| 8방향 | 5(반전 시)~8 | 자유 이동에서 가장 매끄러움 | 비용 2.5배 이상. slynyrd는 레벨과 적 설계에도 더 많은 것을 요구한다고 함 |

반전의 문제[확인]
- 손잡이가 있는 캐릭터는 반전하면 칼·방패 손이 바뀐다. JRPG처럼 오래 보는 스프라이트는 무기 쪽을 지켜야 한다(cxong).
- 전투 중에는 넘어갈 수 있지만, 제자리에서 도는 장면에서는 이상해 보인다(slynyrd).
- 비대칭 머리·장비는 반전하면 어긋난다(slynyrd).

방향이 튀는 느낌을 줄이는 방법[추정 — 일반 구현 관행]
- 방향 경계에 히스테리시스를 둔다. 경계를 넘고도 ±10~15° 더 가야 바꾼다.
- 짧은 입력이나 넉백으로는 방향을 바꾸지 않는다.
- 이동 방향 대신 "공격 대상 쪽"으로 몸을 돌리는 규칙을 우선한다.

## 4. 이 게임 조건에 맞는 추천

조건: 고정 카메라로 30~40° 내려다봄, 가로 화면 모바일, 아군 4명이 화면 높이의 약 13%로 큼, 적은 최대 약 400마리, 조이스틱 자유 이동.

**캐릭터: 지금 방식(대각 4방향 = 2벌 + 반전)을 유지한다.**
- 비스듬한 고정 카메라는 아이소와 같은 조건이다. 업계에서 가장 흔한 조합이 앞 3/4·뒤 3/4 두 벌에 반전이고, 비용은 약 2~2.5배다[확인 — cxong, FFT 관행].
- 화면의 13%면 손잡이가 잘 보인다. 무기를 든 손이 바뀌는 것이 거슬리면 반전 대신 **무기만 따로 겹으로 그린다**. 좌우를 반전해도 무기 겹은 같은 손에 붙인다.
- 8방향(5벌)으로 올리면 비용이 2.5배 이상 든다. 자유 이동이 튀는 문제는 대부분 방향 경계에 히스테리시스를 두고 공격 대상 쪽을 보게 하면 줄어든다[추정].
- 8방향은 플레이 테스트에서 "정면 위·아래로 갈 때 어색하다"는 의견이 반복될 때만 검토한다.

**적: 2방향(앞 3/4 + 반전)을 유지한다.**
- 적은 늘 아군 쪽으로 다가오므로 앞모습이 거의 모든 장면을 덮는다.
- 서바이벌류의 관행이자 수백 마리를 처리하기 위한 최소 비용이다[추정 — 장르 관행].
- 정예·보스만 예외로 둔다. 뒤 3/4를 한 벌 더해 4방향으로 만들거나, Octopath처럼 한 방향으로 크게 그려 공을 들인다.

## 출처

- cxong, "How many sprites do different perspectives need" — https://cxong.github.io/2022/03/how-many-sprites-do-different-perspectives-need
- Raymond Schlitter (slynyrd), Pixelblog 55 Top Down Character Animation — https://www.slynyrd.com/blog/2025/3/24/pixelblog-55-top-down-character-animation
- devilutionX `Direction` 열거형 — https://raw.githubusercontent.com/diasurgical/devilutionX/master/Source/engine/direction.hpp
- d2mods, Animations Conversion Tutorial(DCC 8방향) — https://d2mods.info/forum/kb/viewarticle?a=171
- Lurker Lounge, D1 CL2 group 8(검색 요약) — https://www.lurkerlounge.com/forums/printthread.php?tid=15688
- Triangle Strategy 프로듀서 인터뷰(카메라 고정과 360° 회전) — https://www.theouterhaven.net/2022/05/triangle-strategy-had-challenges-in-making-hd-2d-art-style-work , https://www.nintendolife.com/news/2022/05/triangle-strategy-producers-talk-hd-2d-and-why-other-devs-havent-used-it
- FFT 유닛 데이터(향하는 방향 4값, 검색 요약) — https://ffhacktics.com/wiki/Miscellaneous_Unit_Data
- Octopath 2의 큰 적(의견 글) — https://funario.pages.dev/posts/i-like-that-important-enemies-are-really-big-in-octopath-traveler-2
- Live A Live 리메이크의 움직임(Polygon 미러, 검색 요약) — https://gamecytus.netlify.app/host-https-www.polygon.com/23278977/live-a-live-hd-2d-remake-square-enix-jrpg
- Eiyuden 카메라 자유 이동과 등을 보이는 전투(검색 요약) — https://gamerbraves.com/?p=180249 , https://digitaltrends.com/?p=3489041
- Eiyuden 도트 해상도 결정 — https://news.xbox.com/en-us/2024/04/23/talking-creative-minds-behind-eiyuden-chronicle-hundred-heroes/
- Hades 미리 렌더한 스프라이트(검색 요약) — https://steamcommunity.com/app/1145360/discussions/0/1738883810796606862 , https://kurgancompass.com/?p=1338
- Vampire Survivors 적 위키(검색 요약) — https://vampire-survivors.fandom.com/wiki/Enemies
- 좌우 반전 관행: Nuclear Throne, Forager(검색 요약) — https://www.lexaloffle.com/bbs/?pid=11163 , itch.io https://itch.io/post/5136329

## 열지 못한 곳

- Spriters Resource(403): 시트를 직접 보면 Octopath·Triangle Strategy의 방향 수를 확인할 수 있다.
- TV Tropes "Sprite Mirroring"(403)
- gamer.ne.jp 인터뷰(403)
