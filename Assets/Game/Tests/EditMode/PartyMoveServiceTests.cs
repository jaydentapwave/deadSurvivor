using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// F2 AC16·AC17·AC18 — 진형 따라가기, 쓰러진 대원, 웨이브·판 시작 배치, 프레임 나누기
[Category("DQScreen")]
public class PartyMoveServiceTests
{
    private static readonly Vector2[] Directions =
    {
        new(1f, 0f),
        new(-1f, 0f),
        new(0f, 1f),
        new(0f, -1f),
    };

    // AC16(멈춘 경우): 진형 3종 × 4방향, 1초 이동 → 반대로 1초 → 멈추고 1초 뒤 자리 5 이내.
    // AC16의 "반대로 돌린 뒤 1초(이동 중) 5 이내"는 A절 수치와 양립하지 않아(이동 중 정상 오차 약 7) 기획자 결정 전까지 미해결이다
    // (설계 브리핑 DR-01, 핸드오프 열린 질문). 그 단정은 여기 넣지 않는다.
    [Test]
    public void FollowsSlotsInEveryFormation()
    {
        foreach (var formationId in FormationIds())
        {
            foreach (var direction in Directions)
            {
                var context = StartWithFormation(formationId);
                Move(context, direction, 1f);
                Move(context, -direction, 1f);
                Move(context, Vector2.zero, 1f);
                Assert.LessOrEqual(MaxSlotError(context), 5f, $"{formationId} {direction} stopped");
            }
        }
    }

    // A절 계산 검증(수용 기준 아님): 이동 중 자리 오차는 A절 식의 정상 상태 오차 moveSpeed·dt·(1 − k) ÷ k(k = followRate·dt)를 넘지 않는다
    [Test]
    public void MovingLagMatchesRuleA()
    {
        foreach (var formationId in FormationIds())
        {
            foreach (var direction in Directions)
            {
                var context = StartWithFormation(formationId);
                Move(context, direction, 1f);
                Move(context, -direction, 1f);
                Assert.LessOrEqual(MaxSlotError(context), SteadyMovingSlotError(context) + 0.01f, $"{formationId} {direction}");
            }
        }
    }

    // 한 걸음 대원 이동 ≤ 이동 속도 × 걸음 시간 + 0.01 (출발·반대로 돌리기·90도 꺾기·멈춤)
    [Test]
    public void StepMovementBounded()
    {
        foreach (var formationId in FormationIds())
        {
            var context = StartWithFormation(formationId);
            var limit = context.MoveRules.MoveSpeed * context.RunRules.SimStep + 0.01f;
            var maxStep = 0f;
            foreach (var input in new[] { Directions[0], Directions[1], Directions[2], Directions[0], Vector2.zero })
            {
                context.PartyMoveService.SetMoveInput(input);
                for (var i = 0; i < StepsPerSecond(context); i++)
                {
                    var before = Positions(context);
                    context.PartyMoveService.Step(context.RunRules.SimStep);
                    for (var m = 0; m < before.Count; m++)
                    {
                        maxStep = Mathf.Max(maxStep, Vector2.Distance(before[m], context.PartyModel.MemberList[m].Position));
                    }
                }
            }

            Assert.LessOrEqual(maxStep, limit, formationId);
        }
    }

    // 대원 사이 거리가 진형의 가장 가까운 자리 간격 − 0.1 아래로 줄지 않는다
    [Test]
    public void KeepsSpacing()
    {
        foreach (var formationId in FormationIds())
        {
            var context = StartWithFormation(formationId);
            context.FormationRepository.TryGet(formationId, out var formation);
            var minSlotGap = MinPairDistance(formation.Slots);
            var minGap = float.MaxValue;
            foreach (var input in new[] { Directions[0], Directions[1], Directions[3], new Vector2(0.6f, 0.8f), Vector2.zero })
            {
                context.PartyMoveService.SetMoveInput(input);
                for (var i = 0; i < StepsPerSecond(context); i++)
                {
                    context.PartyMoveService.Step(context.RunRules.SimStep);
                    minGap = Mathf.Min(minGap, MinPairDistance(Positions(context)));
                }
            }

            Assert.GreaterOrEqual(minGap, minSlotGap - 0.1f, formationId);
        }
    }

    // 멈추면 자리 오차가 늘지 않고 1초 뒤 0.01 미만
    [Test]
    public void StopConverges()
    {
        foreach (var formationId in FormationIds())
        {
            var context = StartWithFormation(formationId);
            Move(context, new Vector2(0.8f, -0.6f), 1f);
            context.PartyMoveService.SetMoveInput(Vector2.zero);
            var previousError = MaxSlotError(context);
            Assert.Greater(previousError, 0.01f, $"{formationId} lags while moving");
            for (var i = 0; i < StepsPerSecond(context); i++)
            {
                context.PartyMoveService.Step(context.RunRules.SimStep);
                var error = MaxSlotError(context);
                Assert.LessOrEqual(error, previousError + 1e-5f, $"{formationId} step {i}");
                previousError = error;
            }

            Assert.Less(previousError, 0.01f, formationId);
        }
    }

    // 쓰러진 대원은 걸음에서 좌표가 바뀌지 않고, 나머지 대원의 자리는 4명 기준 그대로다
    [Test]
    public void DeadMemberStaysAndSlotsKept()
    {
        var context = StartWithFormation("wedge");
        var members = context.PartyModel.MemberList;
        members[1].Hp = 0f;
        var deadPosition = members[1].Position;

        Move(context, new Vector2(1f, 0.5f), 1f);
        Assert.AreEqual(deadPosition, members[1].Position);
        Move(context, Vector2.zero, 1f);

        Assert.AreEqual(deadPosition, members[1].Position);
        for (var i = 0; i < members.Count; i++)
        {
            if (i == 1)
            {
                continue;
            }

            var slotPosition = context.PartyModel.Center + context.FormationService.GetSlot(i);
            Assert.Less(Vector2.Distance(members[i].Position, slotPosition), 0.01f, $"member {i}");
        }

        context.FormationRepository.TryGet("wedge", out var wedge);
        for (var i = 0; i < members.Count; i++)
        {
            Assert.AreEqual(wedge.Slots[i], context.FormationService.GetSlot(i), $"slot {i}");
        }
    }

    // 웨이브 시작: 입력 0, 살아 있는 대원(HP가 다시 찬 대원 포함)만 Center + 새 진형 자리로, 쓰러진 대원과 중심은 그대로
    [Test]
    public void StartWavePlacesAliveOnly()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);
        var members = context.PartyModel.MemberList;
        members[2].Hp = 0f;
        members[3].Hp = 0f;
        context.PartyMoveService.SetMoveInput(new Vector2(0.3f, 1f));
        context.StepUntilBattleEnds();
        Assert.AreEqual(RunPhase.Prep, context.RunModel.Phase.CurrentValue);

        context.FormationService.SelectPending("line");
        members[3].Hp = members[3].MaxHp;
        var deadPosition = members[2].Position;
        var center = context.PartyModel.Center;
        context.RunService.NextWave();

        Assert.AreEqual(RunPhase.Battle, context.RunModel.Phase.CurrentValue);
        Assert.AreEqual(Vector2.zero, context.PartyMoveModel.MoveInput);
        Assert.AreEqual(center, context.PartyModel.Center);
        Assert.AreEqual(deadPosition, members[2].Position);
        context.FormationRepository.TryGet("line", out var line);
        foreach (var i in new[] { 0, 1, 3 })
        {
            Assert.AreEqual(center + line.Slots[i], members[i].Position, $"member {i}");
        }
    }

    // 새 판: 가운데에서 네 명 모두 첫 진형 자리로 시작(지난 판의 진형·위치·사망과 상관없이)
    [Test]
    public void ResetForRunPlacesAll()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);
        context.PartyMoveService.SetMoveInput(new Vector2(-1f, 0.4f));
        context.StepUntilBattleEnds();
        context.FormationService.SelectPending("wedge");
        context.RunService.NextWave();
        context.PartyMoveService.SetMoveInput(Vector2.zero);
        context.PlaceKillingContact(1f);
        context.RunService.Step(context.RunRules.SimStep);
        Assert.AreEqual(RunPhase.Result, context.RunModel.Phase.CurrentValue);
        context.RunService.ReturnToLobby();

        context.RunService.StartRun(2, 0f);

        Assert.AreEqual(Vector2.zero, context.PartyModel.Center);
        Assert.AreEqual(Vector2.zero, context.PartyMoveModel.MoveInput);
        var first = context.FormationRepository.FormationDataList[0];
        Assert.AreEqual(first.Id, context.FormationModel.CurrentId.CurrentValue);
        var members = context.PartyModel.MemberList;
        Assert.AreEqual(4, members.Count);
        for (var i = 0; i < members.Count; i++)
        {
            Assert.IsTrue(members[i].IsAlive);
            Assert.AreEqual(first.Slots[i], members[i].Position, $"member {i}");
        }
    }

    // 같은 이동 입력이면 1/60초 60번이든 1초 1번이든 중심·대원 좌표가 같다(F1 SameSeedSameResultAcrossFrameSplits와 같은 나누기)
    [Test]
    public void SameInputSameResultAcrossFrameSplits()
    {
        var bySixtieth = new TestRunContext();
        var bySecond = new TestRunContext();
        bySixtieth.RunService.StartRun(7, 0f);
        bySecond.RunService.StartRun(7, 0f);

        var inputs = new[] { new Vector2(1f, 0f), new Vector2(0.3f, 0.9f), Vector2.zero, new Vector2(-0.7f, -0.7f) };
        foreach (var input in inputs)
        {
            bySixtieth.PartyMoveService.SetMoveInput(input);
            bySecond.PartyMoveService.SetMoveInput(input);
            for (var frame = 0; frame < 60; frame++)
            {
                bySixtieth.RunService.AdvanceFrame(1f / 60f);
            }

            bySecond.RunService.AdvanceFrame(1f);
        }

        Assert.AreNotEqual(Vector2.zero, bySecond.PartyModel.Center);
        Assert.AreEqual(bySecond.PartyModel.Center, bySixtieth.PartyModel.Center);
        for (var i = 0; i < bySecond.PartyModel.MemberList.Count; i++)
        {
            Assert.AreEqual(bySecond.PartyModel.MemberList[i].Position, bySixtieth.PartyModel.MemberList[i].Position, $"member {i}");
        }
    }

    private static IEnumerable<string> FormationIds()
    {
        foreach (var data in new TestRunContext().FormationRepository.FormationDataList)
        {
            yield return data.Id;
        }
    }

    // 한 판을 시작하고 진형을 바꿔 웨이브 시작처럼 자리에 놓는다(정비를 거치지 않는 시험 준비)
    private static TestRunContext StartWithFormation(string formationId)
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);
        context.FormationModel.SetPendingId(formationId);
        context.FormationService.ApplyPending();
        context.PartyMoveService.StartWave();
        return context;
    }

    private static int StepsPerSecond(TestRunContext context) => Mathf.RoundToInt(1f / context.RunRules.SimStep);

    // 웨이브 시간과 상관없이 이동만 seconds초 돌린다
    private static void Move(TestRunContext context, Vector2 input, float seconds)
    {
        context.PartyMoveService.SetMoveInput(input);
        var steps = Mathf.RoundToInt(seconds / context.RunRules.SimStep);
        for (var i = 0; i < steps; i++)
        {
            context.PartyMoveService.Step(context.RunRules.SimStep);
        }
    }

    private static float SteadyMovingSlotError(TestRunContext context)
    {
        var dt = context.RunRules.SimStep;
        var ratio = context.MoveRules.FollowRate * dt;
        return context.MoveRules.MoveSpeed * dt * (1f - ratio) / ratio;
    }

    private static float MaxSlotError(TestRunContext context)
    {
        var members = context.PartyModel.MemberList;
        var maxError = 0f;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i].IsAlive)
            {
                var slotPosition = context.PartyModel.Center + context.FormationService.GetSlot(i);
                maxError = Mathf.Max(maxError, Vector2.Distance(members[i].Position, slotPosition));
            }
        }

        return maxError;
    }

    private static List<Vector2> Positions(TestRunContext context)
    {
        var positionList = new List<Vector2>();
        foreach (var member in context.PartyModel.MemberList)
        {
            positionList.Add(member.Position);
        }

        return positionList;
    }

    private static float MinPairDistance(IReadOnlyList<Vector2> points)
    {
        var minDistance = float.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                minDistance = Mathf.Min(minDistance, Vector2.Distance(points[i], points[j]));
            }
        }

        return minDistance;
    }
}
