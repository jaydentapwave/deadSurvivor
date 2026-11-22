public static class GameEvent
{
    public struct MonsterAttackEvent
    {
        public int MonsterId;
        public UnityEngine.Vector2 Direction;
    }

    public struct MemberAttackHitEvent
    {
        public int MemberIndex;
        public DQHitRecord Hit;
    }
    public struct MemberVisualResetEvent
    {
        public int MemberIndex;
    }

    public struct RunEndedEvent
    {
        public RunOutcome Outcome;
    }
}
