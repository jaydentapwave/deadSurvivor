# 022 실제 파티 연결

- L2, 사용자 요청 두 번째 단계. [설계](../design/map-playable-021-023.md). 독립 설계 PASS에 따라 opt-in DSMapSession과 DSGame의 단일 상태 소유를 유지했다.
- 구현: 3D 바닥 위 실제 캐릭터 빌보드, 기존 DSUI, 안전한 실제/예약 슬롯 이동, 진형 선택·시작·다음 웨이브·부활의 변경 전 검사. 개발 장면에서는 전투를 끄고 실제 정비 흐름을 시험한다. 원본 DeadSurvivor 장면은 그대로다.
- 독립 코드/계산 검토 지적 해소: 비직렬화 타이머·공개 시간·입력·장비 상태·난수 호출 지문, 0이 아닌 타이머 반례, 중심 안전/예약 슬롯 차단/사망 예약/실제 위치 차단/사망 과거 위치 반례, 정비 타이머 정지, 런타임 도형·테두리·비공유 폰트 해제, 파괴 순서와 무관한 소유 bool, 실제 D 입력 진행 확인.
- 2026-10-31 22시 KST Unity: `output/map-blockout-018/playable-22-02deb921c0db41cdbef7e0ca9e305f7d/summary.json` PASS. 79개 검사, 프레임4→8, elapsed .05→.25, 중심 x7→35. 실제 UI 버튼 리스너를 통한 출전·부활·진형·다음 웨이브, 거부 명령의 원자성 PASS. 콘솔 새 오류0, 캡처 전후 카메라·소스 지문 일치, 파일·장면·선택·설정 복원 PASS. 원본 로그/PNG/해시는 같은 폴더.
- 장면 생성 CLI: `output/map-blockout-018/playable-22-build/summary.json` PASS. 기존 대상 파일 변경 없이 새 장면만 생성, 편집 상태 복원.
- 전체 기존 게임: `output/verification/20261031-221558-f8682121/summary.json` PASS. 컴파일·695규칙·입력·24캡처·프레임. 기존 장면/선택 복원 PASS.
- 사용: `Assets/DeadSurvivor/Scenes/Map021/Map022_PartyExperimental.unity` 열기→Play→생존자 선택→출전. WASD 이동, 1로 정비 시험, 기존 UI로 진형·부활·다음 웨이브. 이 단계는 전투 비활성이다.
- 전체 G2/G3/G4 및 최종 미술·지도 정확성 인수 NOT_RUN. 실제 UI 포인터 좌표 자동 클릭 대신 실제 Button.onClick 경로로 동작을 검사했다.
