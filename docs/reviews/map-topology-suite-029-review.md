# 029 — 12조합 기하 연결성 진단 검증

- 범위: TOPOLOGY_SUITE_DIAGNOSTIC_029. 전체 N1/G2/G3·실제 추종·진형 전환·708경로 인수가 아니다.
- 설계: [029](../design/map-topology-suite-029.md), 현재 실행 입력 [native027/r002](../design/map-topology-runtime-profile-027.r002.json). 메인이 구현하고 독립 설계/코드/계산/증거 검토는 읽기 전용으로 수행했다.
- 실행: 2026-11-01 19:45:17~19:51:06 KST. [12조합 요약](../../output/map-full-026/topology-suite-029-df5a687d954f417ab4055deedc121d8f/summary.json) PASS. 1~4인×3진형을 전부 새 자식 실행으로 계산했고 이전 결과를 재사용하지 않았다.
- 결과: 모든 조합 certification/connectivity PASS, 유계 자유영역 성분1·P01 안전·미해결0. 각 자식의 새 nonce/PID/manifest SHA·SEALED 및 3종 산출물 SHA/크기를 검증했다.
- 판정 보완: 늦은 소스·파일쓰기 실패 뒤 승인 성분/연결성 PASS 잔류를 제거한다. 상위에서 피복/frame을 재생성하고 전체 AABB 쌍·positive edge3-clique 완전성을 확인한 뒤 별도 DFS/GF2로 성분 수를 대조한다. 원판-도형 incidence는 고정된027 kernel 신뢰 범위이며 해시만으로 기하 정답을 증명했다고 부르지 않는다.
- 자원/실패: pair30만/clique60만/시간/메모리 한도, 잘못된 행 타입, 최종 manifest/launch/모든 승인 child 증거 변화를 검사한다. 순수 계산 자식만 timeout 종료하며 Unity 강제종료 복구 증거는 아니다.
- 관련49단위 시험 PASS. [원자료 로그](../../output/map-full-026/observer-030-development/test_map_topologyall.py.log). 누락/중복/빈 집합/미해결/잘못된 rank/완전성·잘못된nonce·artifact변조·late failure를 포함한다.
- 독립 validator/algorithm 증거 검토 PASS. [standalone judge](../../output/map-full-026/topology-suite-029-df5a687d954f417ab4055deedc121d8f/judge-bf86b8e17c0e4d3485f1c017f6c11edb.json)도 PASS다. 원본 실행/판정 시각을 구분하고 원자료를 덮지 않았다. 현재 all/gate는030 런타임 통합 후 새로 수행할 예정이고 이 문서에서 PASS로 승격하지 않는다.
- 전체 Python272 시험에서는270통과/기존legacy Documents portal 읽기2오류가 발생했다. [환경 차단 기록](../../output/map-full-026/observer-030-development/full-python-test-blocked.json). 승인 경로/OS 권한/symlink 기준을 완화하지 않았다.

030에서 native 계측 소스가 바뀌기 전 입력/소스 사본을 같은 실행 폴더의 source-checkpoint에 SHA와 보존했다. 후속 소스에 과거 PASS를 자동 재사용하지 않으며 새 native 프로필을 읽어 변경 범위를 대조한다.
