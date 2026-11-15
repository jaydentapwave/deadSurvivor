using UnityEngine;

// 이동 명령(화면 기준 x 오른쪽·y 위, 길이 ≤ 1). 쓰는 쪽은 PartyMoveService뿐이다.
// 원정대 중심은 F1 PartyModel.Center가 원본이다(읽는 쪽이 여럿이라 한 곳에 둔다, F2 설계 브리핑 "기획서와 다른 점 1").
public class PartyMoveModel
{
    public Vector2 MoveInput { get; private set; }

    public void SetMoveInput(Vector2 moveInput) => MoveInput = moveInput;
}
