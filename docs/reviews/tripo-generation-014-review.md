# TRIPO-GENERATION-014 문서 검토

2026-10-31 KST · L1 문서/워크플로우. [변경 브리핑](../design/tripo-generation-014.md) · [생성 기준](../workflows/tripo-generation-standard.md) · [작성 양식](../templates/ai-3d-asset-package.md).

다음 정적 소품 요청부터 TRIPO-STATIC-PROP/r001을 A0 필수 작성 기준으로 연결했다. 대상/개수·실루엣·색/재질·크기·기하/텍스처 예산·조명·구조·모델/시드·비용/공개 근거를 기록한다. 프롬프트 요구, 실제 지원 옵션, Unity 인수 기준을 별도로 동결한다. 자산별 기본값/예외와 실제 적용 확인을 구분한다. 동일 값을 여러 절에 다르게 중복 작성하지 않는다.

공식 기능 근거/확인일은 생성 기준에 모았다. 최신 모델/계정/CLI/UI 지원과 비용은 실제 요청 전에 재확인한다. 과거 바위의10,000삼각형·2K·2단위는 기존 TRANSFER_TEST 전용으로 유지했다. 문서 기준을 API 제출 파일이나 자동 gate·직행 importer 구현으로 보고하지 않는다.

변경은 생성 기준/작업 양식/A0·개발 입구/현재 상태와 이번 브리핑·검토 기록이다. 기존 도구·JSON 계약·처리본·게임/Unity·hook/baseline은 변경하지 않았다. 새 생성/업로드/과금0이며 실제 옵션 적용·Unity 실측/시각 인수는 NOT_RUN이다. 이번 PASS는 문서와 기존 gate 범위다.

독립 validator-client `/root/tripo014_validator`: 스펙 준수 PASS / 동작 계약(문서) PASS, 필수 수정 지적0. 읽기 전용 검토로 A0 라우팅·세 축 분리·시험 수치 격리·지원/조합·시드·비용/공개 근거·미구현 범위를 확인했다. L1이며 게임/계산/스키마 변경이 없어 L2 사전 설계 검토와 algorithm-tester 대상은 아니다.

초기 local `20261031-152853-31158bca`는 문서 링크/데이터/H1 분석 PASS다. 이후 최종 문서와 검토 기록에 대해 local 및 `/usr/bin/python3 Tools/Verify/gate.py check`를 실행하고 같은 독립 검토자에게 결과를 확인받는다. 최종 실행 시각·현재 소스 지문·보고서/해시·검토 상태는 `output/tripo-generation-014/final-checks.json`에 기록한다. 현재 런타임 의존성이 일치하는 기존 all 증거를 gate가 확인하며 이번 문서 변경에서 Unity를 새로 실행한 것으로 보고하지 않는다.
