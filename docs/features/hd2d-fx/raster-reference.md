# 효과 래스터 비교 기준 고정 — F0 보정 채택본 r003

이 문서는 서로 다른 두 raster.py를 같은 기준으로 취급한 혼동을 해소한다. 실행 중 래스터의 정식 픽셀 비교 기준은 아래 **r003 한 개**다. 기존 기준을 통과시키려고 문턱을 낮추거나 C# 결과를 정답으로 내보낸 것이 아니다.

| 구분 | 절대 경로 | SHA256 | 역할 |
|---|---|---|---|
| 정식 비교 기준 r003 | `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/.director/worktrees/hd2d-fx/output/assets/hd2d-fx/tools/raster.py` | `b3416286051712c3a5c3c207b0588c717c09d68a030539ca620b1b2c59eadd30` | C#과 동일 입력·동일 픽셀 비교 |
| 보정 전 외부 원본 | `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/output/assets/hd2d-fx/tools/raster.py` | `d01c5405573bf52621d059cf8bfbc3fab981c1f7ed04696ff6818a706247ae5b` | 최초 입력의 출처 보관, 현재 비교 기준 아님 |

근거는 사용자 작업 지시 마지막의 “넣기 전에 고칠 것(F0에서 그림부터)”다. 고리3단 띠/종류별 모양, 번개2도트 심·가지·끝 불꽃, 별 크기, 돌진 색 보정을 요구했다. 이를 구현한2c0267e의 r003과 새 PNG를 `f0-art-review.md`로 제출했고, 사용자가 반환한 새 컨텍스트 Claude 판독은 다음과 같다.

VERDICT: PASS

F0-A1~A6 모두 PASS, 눈가림7/7. 그 판정 후94a6e39에서68장 임포트·후속 구현을 진행했다. 따라서 사용자 지시의 최초 외부 원본8건 selftest는 보정 전 입력이고, 현재 비교는 그 후 보정·판독을 거친 r003(기존8+추가14=22건 selftest)이다. 메인 폴더 원본을 덮어쓰지 않는다. 설계2절의 번개 심1도트 설명도 이 후속2도트 지시로 대체됐다.

## 재현

`output/hd2d-fx/tools/canonical_cases.py`는 위 r003을 import하여 C#과 무관하게51개 PNG·입력표를 만든다. 기준 원본은 수정하지 않는다. 결과는 `output/hd2d-fx/verification/r002/reference/manifest.json`에 절대 소스 경로/지문과 입력·프레임별 PNG 지문으로 고정한다. GroundMarkRasterTests.All51PinnedCanonicalFramesMatchPixelForPixel은 소스/PNG 지문을 확인한 다음 C# 출력을 RGBA 픽셀 전부 비교한다(허용차0).

- 고리21장: 작은 회복0.1m·실제 회복1.8984m 각5, 파동1.13m5, 자폭1.017m2, 폭발0.8475m4.
- 돌진15장: 길이3.6386m, 각도0/35/90/180/270도 각3.
- 번개15장: 최소(1,0,1), 일반(110,28,7), 음수(-110,-28,7), 수직(0,80,7), 최대시드(80,0,4294967295) 각3. 일반 캔버스130×48은 보정된 끝 불꽃/테용 여백10을 포함하는 r003 계약이다.

알고리즘 리뷰의 “워크트리 r003에 대해서51프레임 차이0” 결과와 같은 비교 대상을 명시적으로 고정했다. 보정 전 원본과의4,179픽셀 차이/126×44 캔버스 차이는 현재 구현의 불일치를 뜻하지 않는다. 이번 수정에서 GroundMarkRaster/ChainBoltRaster 출력·그림은 바꾸지 않았다. 다른 기준이 필요하다는 판정이면 감독 결정 사항으로 보고하며 원본을 몰래 교체하지 않는다.
