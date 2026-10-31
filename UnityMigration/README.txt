데드 서바이버 — 장비 프로토타입 v0.4

실행 프로젝트: /Users/hyeongwoo/Desktop/Workspace/Deadsurvivor
Unity 6000.4.3f1, Assets/DeadSurvivor/Scenes/DeadSurvivor.unity.

WASD/방향키 또는 가상 조이스틱 이동, SPACE/ESC 일시정지, 1 웨이브 건너뛰기.
정비에서 아이템 드래그 또는 선택→대상 칸 미리보기→재탭 적용.
R/회전 버튼은 임시 방향, 적용 성공 시 저장. 중앙 보관함으로 탈착.
같은 아이템/등급 합성, 주무기 교체, 사망 장비 잠금, 정비 무료 부활.

32종 아이템, 8종 주무기. 전사/궁수/마법사/사제는 주무기로 결정.
고정4×4와 모든 아이템 90도 회전. 기본 효과와 조건부 직업 효과 분리.
사제 보조 회복은 최고 제공자1명 기준 파티 공유, 본인 중복 제외.

기본 전투 시간 합435초, 10웨이브, 중간 전리품33개. 정비 제한시간 없음.
균형/쐐기/횡대 선택은 다음 웨이브부터 적용. 능력치 보너스 없음.

검증: 작업 저장소에서 python3 Tools/Verify/run.py all.
현재 코드/콘텐츠 단일 원본은 Assets/DeadSurvivor이며 Git과 Unity 루트가 같다.
과거 Runtime/Editor/PrototypeData 사본은 output/repository-unify-020에 보관했다.
sync.py --apply는 폐기되어 현재 Assets를 덮어쓰지 않는다. 이 폴더에는 독립 검증 템플릿·폰트/이미지 저작 원본만 남긴다.
자세한 기획/구조/검증은 작업 저장소 README.md 및 docs 참조.
원본 DOCX와 이전 할일은 docs/sources에 보존. 최신 규칙이 이전 기획보다 우선.

검사 통과는 실제 모바일 성능/터치, 기본 난이도10분 완주, 최종 아트/밸런스 검증이 아니다.
