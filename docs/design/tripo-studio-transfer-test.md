# Tripo Studio 바위 1개 전송 시험

- 상태: 생성·다운로드·Unity 가져오기·실제 화면 시험 완료. [실행 결과](../reviews/tripo-studio-transfer-20261031.md)에 근거와 한계를 기록한다. 최종 게임용 자산 제작 완료를 의미하지 않는다.
- 규모: L1. Studio 생성부터 파일 보관, Unity 가져오기까지 새 자산 전송 흐름을 시험한다. 게임 구조·맵 설계 변경은 없다.
- 요구: 사용자가 보유한 Studio 200크레딧으로 일반 바위 1개를 생성하고 내려받아 Unity에서 확인한다. 사용자는 이번 바위 1개에 한해 공개 생성과 표시 비용 40크레딧을 승인했다.
- 제외: 추가 생성·유료 구매·프로젝트 자료 업로드·최종 맵 적용·상업 배포. 이 바위는 레퍼런스 재현과 맵 하네스 검증 대상이 아니다.
- 관련 기준: [개발 절차](../workflows/development.md), [검증 gate](../workflows/verification-gates.md), [기존 CLI 연결 기록](../reviews/tripo-cli-install-20261031.md). Studio 크레딧과 API 크레딧은 별개다. 이번 시험은 Studio 화면에서 수행한다.
- 기존 소유처: 문서·증거는 이 저장소, 실제 Unity 대상은 `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor`. 기존 게임 코드는 변경하지 않는다.
- 변경 계획: `output/tripo-studio-test`에 화면·다운로드·지문을 보관하고 `docs/reviews`에 결과를 기록한다. 내보내기 성공 시 Unity의 독립 시험 자산으로 가져오며 기존 씬과 게임 자산을 덮어쓰지 않는다. 실제 변경 경로는 결과 기록에 명시한다.
- 재사용: 기존 Unity CLI와 검증 도구를 사용한다. 새 자동 생성기·API 결제 흐름·맵 검증기를 구현하지 않는다.
- 실패 경계: 생성은 한 번만 제출한다. 작업이 느리거나 오류가 나도 추가 크레딧 요청을 반복하지 않는다. 내보내기가 유료로 차단되면 이를 BLOCKED로 기록하고 Unity 단계를 NOT_RUN으로 남긴다. 결제나 유료 구독을 진행하지 않는다.
- 복구: 기존 파일을 덮어쓰거나 동기화하지 않는다. Unity 시험 전 대상·씬 저장 상태를 확인하고 시험을 위해 변경한 뷰/플레이 설정은 원복한다. 외부 공개 예외는 이번 일반 바위 하나에만 적용된다.
- 검증: 생성 전후 잔액·작업 ID·화면 확인 → 실제 다운로드 파일과 해시 확인 → 가능하면 Unity 모델·재질·실제 렌더 화면 확인 → 독립 검토 → 최종 gate. 문서 검사로 Unity 성공을 대신하지 않는다.
- 확인 결과: 이 계정의 H2.5 모델은 FBX와 텍스처를 추가 결제 없이 내보낼 수 있었다. Unity의 `Assets/DeadSurvivor/Art/Tests/TripoRock21300150`에 별도 시험 자산·프리팹·씬을 추가했다. 다른 모델 버전·계정·미래 정책까지 보장하는 결과는 아니다.
- 설계 검토: L1이므로 별도 사전 L2 검토는 해당 없음. 시험 후 validator-client 독립 검토를 수행한다.

## 생성 입력

- 방식: HD 모델, H2.5 레거시, 텍스트 입력, 지오메트리 및 텍스처, 공개.
- 부가 생성·8K·T-포즈: 사용하지 않음.
- 승인 범위: 프로젝트 이름·지도·원본 자료가 없는 일반 회색 바위 1개, 40크레딧.

```text
One standalone stylized low-poly gray stone boulder for an isometric game. Broad irregular facets, a squat asymmetrical silhouette, matte desaturated gray stone, a few subtle moss patches near the base, and a flat stable bottom. One solid object only. No ground plane, no scenery, no characters, no text, no logos. Simple clean geometry and a restrained painted texture.
```
