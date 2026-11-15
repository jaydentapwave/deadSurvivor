// 아이템 발동 방식(기획서 B5). 데이터 문자열은 첫 글자만 소문자(melee, healArea …)
public enum ItemMode
{
    Melee,
    Sweep,
    Projectile,
    Explosion,
    Chain,
    Heal,
    HealArea,
    Passive,
}

// 조건 있는 추가 효과의 대상(기획서 B3). 데이터 "" = Any, "attack" = 회복 두 가지를 뺀 발동 방식, 그 밖에는 발동 방식 이름
public enum ItemBonusTarget
{
    Any,
    Attack,
    Melee,
    Sweep,
    Projectile,
    Explosion,
    Chain,
    Heal,
    HealArea,
}
