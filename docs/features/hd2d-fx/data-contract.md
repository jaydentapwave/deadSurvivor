# FX 표현 데이터 계약

`Assets/Game/03.Resources/Resources/Data/Combat/combat_effects.json`은 items와 clips 두 배열을 가진다. 기존 아이템 소비 경로 Get(itemId)는 items의 itemId/projectile/burst 문자열을 돌려준다. GetClip(효과 키)는 clips.key에서 찾는다.

clips 필드는 key(효과 식별자), sprite(그림 접두사), frames(기본1), fps(기본1), loop(기본false), directional(기본false), materialIndex(기본0)다. 여러 장은 `<sprite>_<프레임1부터>`, 방향 그림은 `<sprite>_<right|diag|up>_<프레임>`이다. 방향 없는1장 클립은 sprite를 접미사 없이 그대로 쓴다. 화살/볼트는1장이지만 directional=true여서 기존 방향별 접미사를 유지한다.

등록되지 않은 키 또는 clips 없는 옛 형식은 GetClip이 그 키를 sprite로 쓰는 단일 그림 클립을 돌려준다. 이 경로는 임의 키를 옛 단일 그림 이름으로 돌릴 때 번호 접미사를 강제하지 않는다. 프레임은0에서 고정되고 추가 래스터/폭발 바닥 고리 생성도 생략한다. 한 장으로 돌아갈 때는 해당 이름의 그림이 아틀라스에서 해석 가능해야 한다. 현재 게임에는 옛 효과 그림을 다시 연결하지 않았고 X-4 참조0을 유지한다. 되돌리기용 파일 보존과 현재 활성 아틀라스에 넣는 것은 별개다.

별도 clips 표를 둔 이유는 firestaff item 하나가 불탄3장12fps 반복과 폭발6장24fps 단발을 동시에 사용하기 때문이다. item의 단일 frames/fps로 두 표현을 공유할 수 없다. 세이브/게임 피해·속도·범위 계약은 바뀌지 않았다.
