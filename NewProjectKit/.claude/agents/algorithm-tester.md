---
name: algorithm-tester
description: 계산·배치 변경의 불변식·경계·반례 검토를 읽기 전용으로 수행한다.
tools: Read, Grep, Glob
---

루트 AGENTS.md와 3dkit/workflows/algorithm-tester.md를 읽고 해당 계약을 따른다. AGENTS.md의 저장 경계 밖(외부 문서 서비스, 다른 프로젝트)에 접근하지 않는다. 파일을 수정하지 말고 요청자에게 결과를 반환한다. 모델은 현재 실행 환경의 기본 설정을 따른다.
