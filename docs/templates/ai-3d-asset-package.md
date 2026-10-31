# AI 3D 자산 작업 묶음 양식

실행 결과가 아닌 작성 양식. [작업 단계](../workflows/ai-3d-assets.md), [자료/검사 계약](../design/ai-3d-asset-harness.md)을 따른다. 빈 항목은 미정/NOT_RUN/해당 없음과 이유로 구분한다.

## 범위와 입력

- asset ID / 개정 / scope(FIXTURE·PRODUCTION) / 현재 A단계:
- 작업 ID / 개발 규모 / 이전 개정 / 변경 이유:
- 정적 소품 역할 / RF·OB / 원본 출처와 허용 범위:
- source_mode(existing·generated) / 실제 원본 경로·해시:
- 유지할 형태·팔레트 / 허용 변경 / 금지 변경:
- 모델·프롬프트·옵션·입력 해시 / 비용·공개·계정 근거(새 생성 시만):
- 중단 작업 attempt·task ID / 재개 여부 / 추가 제출 여부·이유:

## 제작·검증 계약

새 Tripo 요청은 [생성 기준 TRIPO-STATIC-PROP/r001](../workflows/tripo-generation-standard.md)을 적용한다. 아래 세 묶음을 먼저 작성하고 A0에서 동결한다. 이는 작성 양식이며 실행용 API JSON이 아니다.

### Tripo 요청 고정

- 생성 기준 ID/개정 / 자산별 예외·이유:
- 대상 종류·개수·용도 / 레퍼런스 ID·해시 / 유지할 실루엣·비율:
- 색·재질·광택·표면 디테일 / 제외 요소·고정 조명/카메라 비교 기준:
- prompt / negative prompt 원문·해시:
- 실제 서버·계정 구분·Studio/API/CLI 경로 / 설치 CLI 버전・인자 근거:
- geometry model / texture model의 정확한 버전 / 지원 기능 확인일·근거:
- 실제 전송 옵션: face_limit·quad·geometry_quality·texture·pbr·texture_quality·auto_size·generate_parts:
- smart_low_poly·delight·UV 관련 옵션: 지원/조합/비용 확인, 사용·생략 값과 이유:
- geometry/image/texture seed 정책과 실제 확인값(미확인은 UNKNOWN):
- 비용 한도·공개/업로드 허용 근거 / 추가 제출·재개 정책:

### Unity 인수 기준

- 삼각형 상한·메시/재질 수·인스턴스 수 / 수치 근거와 scope:
- 각 축 치수·단위·정면 / 바닥·원점 허용 오차:
- 텍스처 슬롯·최대 해상도 / UV·shader·허용 컴포넌트:
- 실제 전송/적용된 설정 확인·task ID・원본 지문:
- Unity CLI 실측/실제 프레임·화면/콘솔/복구 결과:

### 후처리·비교

- 필요한 후처리 사유·고정 절차 / 처리 단계 의존 지문 / 검증된 처리본 재사용 여부:
- 요청한 형태와 원본/처리본/Unity 화면 비교·차이·인수/보류 이유:

### 기존 A단계 공통 기록

Tripo 요청에서 앞 절에 작성한 동일 값은 해당 절을 참조하고 아래에는 빠진 항목과 단계 실행 증거를 보충한다. 같은 기준을 서로 다른 값으로 중복 작성하지 않는다.

- 단위·축·정면·원점·지면 접점 / 치수 허용 범위와 근거:
- 삼각형·재질·텍스처·성능 예산 / 환경·인스턴스 수 / 미정 차단:
- 처리 절차·Blender 버전·해시 / 원본 보존 / 처리 전후 관측:
- Unity 대상·버전·파이프라인·importer / 저장 후 재로드 실측:
- 카메라·조명·출력 조건 / S1·S2·S3 캡처와 검토 결과:
- 예상 AC·AFI 변형/정상 대조군 / 사전 제외와 이유:
- 단계별 결과·원자료·지문 / 독립 검토 / cleanup:

## 채택과 맵 연결

- 영구 보존 경로·의존 파일 해시 / acceptance·등록 상태:
- 맵 연결 전이면 미연결 사유; 연결 시 map/layout/C/변환 개정:
- OB별 asset/프리팹/배치 변환 / RF·RT·영향 MC:
- 변경 CH / 시각 V와 충돌 C의 차이 / 게임 카메라·성능 결과:
- 문서 검토 / 자산 채택 / 맵 G단계 상태를 각각 기록:
- 미실행·차단·남은 조치 / 복구와 기존 자산 보존 결과:
