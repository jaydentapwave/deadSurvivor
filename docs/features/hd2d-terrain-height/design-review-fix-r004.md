# design-review-fix r004 — 걷는 땅 높낮이 (Codex gpt-6.1-sol medium, 2026-11-18 17:5x, job 20261118-175628-c0b336)

VERDICT: PASS — DR-03-B 설계상 해결(기존 HD2D_EffectGlow 조각 계산·알파 클립·강도·`_CoreIntensity > 2` 계약 유지, `_ZTest` 기본 4로 기존 재질 동작 유지, 큐 1995 그림자 뒤 1996 효과, 2440·1996 모두 불투명 구간).

후속 F-04: 배경 이동 뒤 최종 Bloom 모양·효과 겹침·반투명 소품 앞뒤를 B4·B5에서 비교(필수 아님).

정적 재확인. TC5·Unity 렌더 검증은 구현 단계.
