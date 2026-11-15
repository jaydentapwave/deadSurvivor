// 아이템 효과 종류(기획서 B3). 데이터 "" = None
public enum ItemEffectKind
{
    None,
    // 더하기
    Attack,
    Def,
    Hp,
    Heal,
    Haste,
    Range,
    Radius,
    // 큰 값 하나
    Shield,
    StartShield,
    // 정수·큰 값 하나·등급 배수 안 곱함
    Pierce,
    ExtraShots,
    Chain,
    // 동작
    Pulse,
    Share,
}
