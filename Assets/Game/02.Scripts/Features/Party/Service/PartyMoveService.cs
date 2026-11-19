using UnityEngine;

// 원정대 이동(F2 기획서 A·C절). 원정대 중심(PartyModel.Center)과 대원 바닥 좌표를 쓰는 유일한 곳.
public class PartyMoveService
{
    private readonly PartyModel _partyModel;
    private readonly ViewRulesData _viewRules;
    private readonly PartyMoveModel _partyMoveModel;
    private readonly FormationService _formationService;
    private readonly MoveRulesData _moveRules;
    // 들판 마름모 |x| + |y| ≤ 반대각선(바닥 단위). 들판은 원점 가운데 fieldCells칸, 칸 폭 1유닛(Const.PixelsPerUnit)
    private readonly float _fieldHalfDiagonal;

    // 중심이 움직일 수 있는 마름모(들판 − 보이는 영역). 보이는 영역을 받기 전에는 들판 전체
    private float _centerLimit;

    public PartyMoveService(PartyModel partyModel, PartyMoveModel partyMoveModel, FormationService formationService,
        MoveRulesData moveRules, RunRulesData runRules, ViewRulesData viewRules)
    {
        _partyModel = partyModel;
        _viewRules = viewRules;
        _partyMoveModel = partyMoveModel;
        _formationService = formationService;
        _moveRules = moveRules;
        _fieldHalfDiagonal = runRules.FieldCells / (2f * runRules.UnitScale);
        _centerLimit = _fieldHalfDiagonal;
    }

    // 카메라가 보는 바닥 영역의 반 크기(바닥 단위). 들판 밖이 보이지 않게 중심의 이동 범위를 줄인다
    public void SetViewHalfExtent(Vector2 floorHalfExtent)
    {
        _centerLimit = _fieldHalfDiagonal - floorHalfExtent.x - floorHalfExtent.y;
        if (_centerLimit <= 0f)
        {
            GameLogger.LogWarning($"[PartyMoveService] View {floorHalfExtent} is larger than field {_fieldHalfDiagonal}; party stays at center");
        }
    }

    // 화면 기준 이동 명령(x 오른쪽, y 위). 길이 1로 자른다
    public void SetMoveInput(Vector2 moveInput) => _partyMoveModel.SetMoveInput(Vector2.ClampMagnitude(moveInput, 1f));

    // 한 판 시작: 들판 가운데, 입력 0, 네 명 모두 자리로(FormationService.ResetForRun 뒤에 부른다)
    public void ResetForRun()
    {
        _partyModel.SetCenter(Vector2.zero);
        _partyMoveModel.SetMoveInput(Vector2.zero);
        var memberList = _partyModel.MemberList;
        for (var i = 0; i < memberList.Count; i++)
        {
            memberList[i].Position = _partyModel.Center + _formationService.GetSlot(i);
        }
    }

    // 웨이브 시작: 입력 0, 살아 있는 대원만 자리로. 중심은 그대로(FormationService.ApplyPending 뒤에 부른다)
    public void StartWave()
    {
        _partyMoveModel.SetMoveInput(Vector2.zero);
        var memberList = _partyModel.MemberList;
        for (var i = 0; i < memberList.Count; i++)
        {
            var member = memberList[i];
            if (member.IsAlive)
            {
                member.Position = _partyModel.Center + _formationService.GetSlot(i);
            }
        }
    }

    public Vector2 ClampCenter(Vector2 center) => PartyMoveMath.ClampToDiamond(center, _centerLimit);

    // 한 걸음(A절 1~4): 중심 이동 → 들판 마름모 안으로 → 살아 있는 대원이 자리 쪽으로 다가감. 쓰러진 대원은 그대로
    public void Step(float deltaSeconds)
    {
        var moveInput = _partyMoveModel.MoveInput;
        var direction = PartyMoveMath.ScreenToFloorDirection(moveInput, _viewRules.SinTilt);
        var center = _partyModel.Center + direction * (moveInput.magnitude * _moveRules.MoveSpeed * deltaSeconds);
        center = ClampCenter(center);
        _partyModel.SetCenter(center);

        var ratio = _moveRules.FollowRate * deltaSeconds;
        var memberList = _partyModel.MemberList;
        for (var i = 0; i < memberList.Count; i++)
        {
            var member = memberList[i];
            if (!member.IsAlive)
            {
                continue;
            }

            member.Position = PartyMoveMath.FollowStep(member.Position, center + _formationService.GetSlot(i), ratio);
        }
    }
}
