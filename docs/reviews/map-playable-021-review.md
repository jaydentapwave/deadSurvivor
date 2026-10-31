# 021 크기·카메라 보정

- 요청: 1–3 단계별 구현·검증·커밋 중 첫 단계. L2, [설계](../design/map-playable-021-023.md).
- 설계 독립 검토 PASS: D1 고정 비교·D2 소유권·D3 생성 실패·D4 격리 실험 범위를 보완했다.
- 구현 독립 검토 PASS: V1 비가림 상태 원본 재질 사용, V2 캡처 후 카메라 재검증을 수정했다. reviewer play_validator가 현재 소스 지문·6뷰·복원 증거를 독립 확인했다.
- 2026-10-31 22시 KST 실행: `output/map-blockout-018/playable-21-947694f1aceb42a2a678333a3871acaa/summary.json` PASS. 6뷰, 실제 프레임, 가림 발생/해제/해제 시 원본 재질 복구, 소스 지문 일치, 파일·편집 상태 복원 PASS. 원본 로그/PNG/캡처별 전후 카메라·해시는 해당 폴더.
- 018 메시 단위 회귀: `output/map-blockout-018/playable-21-build/018-units.json` 35개 PASS.
- 전체 게임: `output/verification/20261031-220854-4e2ae025/summary.json` PASS. 컴파일·695규칙·입력·24캡처·프레임·화면 검사. 원래 장면/선택/설정 복원 PASS.
- 장면 저장의 공유 폰트 캐시 부수 변경은 저장 직전 HEAD의 사용자 원본 fde81941546290831f56390ecc9438e1b919e9f23fbddb3f0e9bf4df00a9d02c로 복원했다. 원본 캐시 부수 변경은 output에 보존했다. 그 외 기존 자산 변경 없음.
- 기본값 s4/55도는 개발 편의를 위한 가역적 선택. 원본 지도 최적 비율·전체 G2/G3/G4·최종 미술 인수는 NOT_RUN.
- 사용: Unity의 `Assets/DeadSurvivor/Scenes/Map021/Map021_Calibration.unity`. 이동 WASD, Tab 전체/추적, C 각도, V 바위/파티. 018 원본과 별도 장면이다.
