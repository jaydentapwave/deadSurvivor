// 대원 하나의 능력치 묶음(기획서 B3). 대원 전체 또는 발동 아이템 하나를 위한 계산 결과
public struct CombatStats
{
    // 공격 증가(더하기)
    public float Attack;
    // 유효 방어(상한까지)
    public float Defense;
    public float MaxHp;
    // 자기 회복량
    public float Heal;
    // 가속(상한까지)
    public float Haste;
    // 사거리 증가
    public float Range;
    // 폭발 반경 증가
    public float Radius;
    // 보호막 상한 = max(보호막, 시작 보호막)
    public float Shield;
    public float StartShield;
    public int Pierce;
    public int ExtraShots;
    public int Chain;
}
