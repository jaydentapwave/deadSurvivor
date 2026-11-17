using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// Main.unity의 모든 등록(기획서 A절). 등록만 하고 다른 일은 하지 않는다.
public class GameSceneLifetimeScope : LifetimeScope
{
    private const string RunRulesPath = "Data/Run/run_rules";
    private const string GroundPathPath = "Data/Field/ground_path";
    private const string PlaceholderCombatPath = "Data/Run/placeholder_combat";
    private const string MoveRulesPath = "Data/Party/move_rules";

    [SerializeField] private MemberView[] memberViews;
    // 투사체·고리 그림의 부모(F3)
    [SerializeField] private Transform combatEffectRoot;
    [SerializeField] private EnemyShotView enemyShotPrefab;
    [SerializeField] private Transform enemyShotParent;
    // 피해 숫자·몬스터 HP 막대 프리팹과 풀 부모(F6)
    [SerializeField] private DamageNumberView damageNumberPrefab;
    [SerializeField] private Transform damageNumberParent;
    [SerializeField] private MonsterHpBarView monsterHpBarPrefab;
    [SerializeField] private Transform monsterHpBarParent;

    protected override void Configure(IContainerBuilder builder)
    {
        // Core
        builder.Register<ResourceService>(Lifetime.Scoped);
        builder.Register<PoolManager>(Lifetime.Scoped);
        builder.Register<EventBus>(Lifetime.Scoped);
        builder.Register<PlaceholderSpriteFactory>(Lifetime.Scoped);
        builder.Register<SpriteAtlasService>(Lifetime.Scoped);

        // 데이터
        builder.Register(resolver => resolver.Resolve<ResourceService>().LoadJson<CombatEffectData>("Data/Combat/combat_effects"), Lifetime.Scoped);
        builder.Register(resolver => resolver.Resolve<ResourceService>().LoadJson<RunRulesData>(RunRulesPath), Lifetime.Scoped);
        builder.Register(resolver => resolver.Resolve<ResourceService>().LoadJson<GroundPathData>(GroundPathPath), Lifetime.Scoped);
        builder.Register(resolver => resolver.Resolve<ResourceService>().LoadJson<PlaceholderCombatData>(PlaceholderCombatPath),
            Lifetime.Scoped);
        builder.Register<CharacterRepository>(Lifetime.Scoped);
        builder.Register<ItemRepository>(Lifetime.Scoped);
        builder.Register<WaveRepository>(Lifetime.Scoped);
        builder.Register<CombatRulesRepository>(Lifetime.Scoped);
        builder.Register<FormationRepository>(Lifetime.Scoped);
        builder.Register(resolver => resolver.Resolve<ResourceService>().LoadJson<MoveRulesData>(MoveRulesPath), Lifetime.Scoped);
        builder.Register<MonsterRepository>(Lifetime.Scoped);
        builder.Register<GradeRepository>(Lifetime.Scoped);
        builder.Register<BagRulesRepository>(Lifetime.Scoped);
        builder.Register<LootRulesRepository>(Lifetime.Scoped);
        builder.Register<FeedbackRulesRepository>(Lifetime.Scoped);
        builder.Register(resolver => resolver.Resolve<FeedbackRulesRepository>().Rules.Haptic, Lifetime.Scoped);

        // 모델
        builder.Register<RunModel>(Lifetime.Scoped);
        builder.Register<RunSettingsModel>(Lifetime.Scoped);
        builder.Register<PartyModel>(Lifetime.Scoped);
        builder.Register<MonsterModel>(Lifetime.Scoped);
        builder.Register<CombatModel>(Lifetime.Scoped);
        builder.Register<FormationModel>(Lifetime.Scoped);
        builder.Register<PartyMoveModel>(Lifetime.Scoped);
        builder.Register<BagModel>(Lifetime.Scoped);
        builder.Register<CombatFeedbackModel>(Lifetime.Scoped);

        // 서비스
        builder.Register<RunService>(Lifetime.Scoped);
        builder.Register<PartyService>(Lifetime.Scoped);
        builder.Register<IItemPlacementInfo, BagPlacementInfo>(Lifetime.Scoped);
        builder.Register<EquipmentStatService>(Lifetime.Scoped);
        builder.Register<CombatService>(Lifetime.Scoped);
        builder.Register<ReviveService>(Lifetime.Scoped);
        builder.Register<MonsterService>(Lifetime.Scoped);
        builder.Register<FormationService>(Lifetime.Scoped);
        builder.Register<PartyMoveService>(Lifetime.Scoped);
        builder.Register<MonsterSpawnService>(Lifetime.Scoped);
        builder.Register<MonsterRecycleService>(Lifetime.Scoped);
        builder.Register<MonsterBehaviorService>(Lifetime.Scoped);
        builder.Register<ContactBehavior>(Lifetime.Scoped).As<IMonsterBehavior>();
        builder.Register<ProjectileBehavior>(Lifetime.Scoped).As<IMonsterBehavior>();
        builder.Register<ChargeBehavior>(Lifetime.Scoped).As<IMonsterBehavior>();
        builder.Register<BombBehavior>(Lifetime.Scoped).As<IMonsterBehavior>();
        builder.Register<EnemyShotService>(Lifetime.Scoped);
        builder.Register<BagService>(Lifetime.Scoped);
        builder.Register<BagPreviewService>(Lifetime.Scoped);
        builder.Register<LootService>(Lifetime.Scoped);
        // 판정 봇(PlayableBot)과 시험이 쓰는 자동 장착 계획(F5 E절)
        builder.Register<AutoEquipPlanner>(Lifetime.Scoped);
        builder.Register<HapticService>(Lifetime.Scoped);

        // 생성·등록부
        builder.Register<MonsterViewFactory>(Lifetime.Scoped);
        builder.Register<MonsterViewRegistry>(Lifetime.Scoped);
        builder.Register<ProjectileViewFactory>(Lifetime.Scoped).WithParameter(combatEffectRoot);
        builder.Register<ProjectileViewRegistry>(Lifetime.Scoped);
        builder.Register<EnemyShotViewFactory>(Lifetime.Scoped).WithParameter(enemyShotPrefab).WithParameter(enemyShotParent);
        builder.Register<EnemyShotViewRegistry>(Lifetime.Scoped);
        builder.Register<DamageNumberViewFactory>(Lifetime.Scoped).WithParameter(damageNumberPrefab).WithParameter(damageNumberParent);
        builder.Register<DamageNumberViewRegistry>(Lifetime.Scoped);
        builder.Register<MonsterHpBarViewFactory>(Lifetime.Scoped).WithParameter(monsterHpBarPrefab).WithParameter(monsterHpBarParent);
        builder.Register<MonsterHpBarViewRegistry>(Lifetime.Scoped);

        // 장면 컴포넌트
        builder.RegisterComponentInHierarchy<Camera>();
        builder.RegisterComponentInHierarchy<FieldView>();
        builder.RegisterComponentInHierarchy<UI_LobbyView>();
        builder.RegisterComponentInHierarchy<UI_BattleHudView>();
        builder.RegisterComponentInHierarchy<UI_PrepView>();
        builder.RegisterComponentInHierarchy<UI_ResultView>();
        builder.RegisterComponentInHierarchy<UI_JoystickView>();
        builder.RegisterComponentInHierarchy<UI_FormationView>();
        builder.RegisterComponentInHierarchy<CameraShakeView>();
        // 같은 형 컴포넌트 여럿은 따로 등록할 수 없어(VContainer 형 충돌) 장면 순서 목록 하나로 넘긴다
        builder.RegisterInstance<IReadOnlyList<MemberView>>(memberViews);

        // 진입점(판정 진입점이 꺼낼 수 있게 자기 형으로도 등록)
        builder.RegisterEntryPoint<ScreenPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<LobbyPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<BattlePresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<BattleHudPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<PrepPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<ResultPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<WorldPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<BattleItemListPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<RevivePresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<CombatWorldPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<MoveInputPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<FormationPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<MonsterStatePresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<EnemyShotPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<BagPresenter>(Lifetime.Scoped).AsSelf();
        // F6 피드백. 흔들림은 카메라를 옮기는 WorldPresenter 뒤(같은 프레임 LateTick 순서)
        builder.RegisterEntryPoint<DamageNumberPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<HpLossPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<HitReactionPresenter>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<MonsterHpBarPresenter>(Lifetime.Scoped).AsSelf();
    }
}
