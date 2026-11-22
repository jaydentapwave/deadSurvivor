# 8단계 수정 확인 — 기획서 비교 기준 명시

승인 기획서 비교 기준은 **df10136**이다. 코드의 수정 확인 범위 기준 **a843198**과 구분한다. 이 문서는 수용 기준이나 리뷰 규칙의 예외를 추가하지 않는다. 승인 브리핑은 복구한 그대로 고정한다.

## 비교할 범위

| 대상 | 기준 | 이유 |
|---|---|---|
| 승인 기획서 불변 검사 | df10136…HEAD | design-reviewer가 PASS한 승인본 시점. `.ai/reviewers/spec-reviewer.md:16`의 `<base>`에 사용 |
| 앞 FAIL에 대한 코드 수정 확인 | a843198…682618a | 타격 audit·렌더/수명 시험·배속/idle/경계 수정 범위 |
| 이번 추가 변경 | 682618a…HEAD | 이 비교 기준 설명 문서만 추가. 코드·시험·승인 기획서·자산 변경 없음 |

아래는 역할 규칙4의 명령을 승인 기준으로 그대로 실행한 결과다.

```sh
git diff df10136...HEAD -- docs/features/dq-apply/characters-brief.md
```

출력 **0바이트**. 승인본과 HEAD의 SHA256은 모두 `dfdc5298db594629852f548e7923c8ae5a844d5ac3a5a44b5147997a9133a669`다. 근거는 `output/hd2d-dq/apply/8/r4/plan-comparison.json`과 기존 r3 `approved-brief-check.json`이다.

앞 수정 확인은 a843198…682618a라는 **코드 수정 범위**를 기획서 불변 검사의 기준으로도 사용했다. a843198에는 되돌려야 했던 미승인 문서 편집이 들어 있으므로, 그 범위에서 승인본 복구 diff가 나타나는 것은 예상한 결과다. 이 diff를 다시 없애려고 복구를 되돌리면 오히려 승인본과 달라진다. 승인 기준을 df10136으로 고정하면 역할 규칙을 그대로 적용하면서 복구 완료를 검증할 수 있다. 현재 기획 불일치를 허용하거나 앞 편집을 소급 승인하는 뜻이 아니다.

## 받은 판정과 재확인 범위

- spec-reviewer, job20261123-045838-368930: `VERDICT: FAIL`. C8-03/04/05/07 met, 승인본 복구 자체와 바이트 일치 확인, C8-08은 복구 diff가 있다는 역할 조건만으로 partial. 위 비교 기준 명시로 **C8-08만 spec-reviewer 재확인** 요청한다. 복구를 되돌리거나 리뷰 기준을 낮추지 않았다.
- validator-client, job20261123-045840-52c5a9: `VERDICT: PASS`. 코드 변경이 없으므로 재호출하지 않고 이어 받는다.
- algorithm-tester, job20261123-045834-1bbdac: `VERDICT: PASS`. 독립 실행10개·trace14개, 총24개 PASS. 코드 변경이 없으므로 재호출하지 않고 이어 받는다.
- design-reviewer, job20261123-034539-34753d: `VERDICT: PASS` 유지.

## review_open — 수정 대상 아님

- spec-reviewer 후속 제안: 두 무기 pending과 표시1회는 검사하지만 피해는 감소 여부만 검사한다. 정확한 피해·발사 건수 불변 검사는 후속 보강 대상. 필수 수정으로 승격하거나 이번에 검사 기준을 변경하지 않음.
- spec-reviewer·validator-client·algorithm-tester: 기존 전체 EditMode 실패5건, 원근 BLOCKED, terrain N9 FAIL 유지. 이번 단계 판정 밖.
- validator-client: 대원16개·몬스터5종·보완3개 시트를 직접 열어 보이는 표본의 프레임 변화·발 기준·쓰러짐 끝 자세 확인. boar windup/dash 등 가려진 표본의 전체 몸·발·그림자 정렬은 화면 미확인. 화면 배치 diff는 적용 대상 아님.
- validator-client·algorithm-tester: 제출된 Unity 실행 기록을 확인했지만 Unity 컴파일·시험·Play·atlas packing을 독립 재실행하지 않음. 앞 자체 실행과 독립 재실행을 구별한다.

최종 핸드오프는 spec-reviewer의 비교 기준 재확인 판정 후 작성한다. 기존 r3 증거는 변경하지 않는다. 이번 작업은 문서의 링크·기준·해시 일관성만 확인했으며 컴파일·시험을 반복하지 않았다.
