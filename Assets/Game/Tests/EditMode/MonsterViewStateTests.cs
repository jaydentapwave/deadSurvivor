using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// F4 AC19 — MonsterView가 Windup에서 돌진 방향 선, Fuse에서 폭발 반경 원을 켜고 다른 상태에서 끈다
public class MonsterViewStateTests
{
    private GameObject _root;
    private MonsterView _view;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("MonsterView");
        var body = _root.AddComponent<SpriteRenderer>();
        var line = new GameObject("DashLine").AddComponent<SpriteRenderer>();
        var area = new GameObject("BlastArea").AddComponent<SpriteRenderer>();
        line.transform.SetParent(_root.transform, false);
        area.transform.SetParent(_root.transform, false);
        _view = _root.AddComponent<MonsterView>();
        var serialized = new SerializedObject(_view);
        serialized.FindProperty("bodyRenderer").objectReferenceValue = body;
        serialized.FindProperty("dashLineRenderer").objectReferenceValue = line;
        serialized.FindProperty("blastAreaRenderer").objectReferenceValue = area;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        _view.Initialize(CreateSprite(8, 8, new Vector2(0.5f, 0f)), null, false, CreateSprite(4, 2, new Vector2(0f, 0.5f)),
            CreateSprite(64, 64, new Vector2(0.5f, 0f)), 322f, 90f, 0.0113f);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [Test]
    public void StartsWithBothOff()
    {
        Assert.IsFalse(_view.IsDashLineVisible);
        Assert.IsFalse(_view.IsBlastAreaVisible);
    }

    [Test]
    public void WindupShowsDashLineOnly()
    {
        _view.UpdateState(MonsterPhase.Windup, Vector2.right);

        Assert.IsTrue(_view.IsDashLineVisible);
        Assert.IsFalse(_view.IsBlastAreaVisible);
    }

    [Test]
    public void FuseShowsBlastAreaOnly()
    {
        _view.UpdateState(MonsterPhase.Fuse, Vector2.zero);

        Assert.IsFalse(_view.IsDashLineVisible);
        Assert.IsTrue(_view.IsBlastAreaVisible);
    }

    [Test]
    public void OtherPhasesTurnBothOff()
    {
        foreach (var phase in new[] { MonsterPhase.Approach, MonsterPhase.Dash, MonsterPhase.Recover, MonsterPhase.Swarm })
        {
            _view.UpdateState(MonsterPhase.Windup, Vector2.up);
            _view.UpdateState(MonsterPhase.Fuse, Vector2.zero);
            _view.UpdateState(phase, Vector2.zero);

            Assert.IsFalse(_view.IsDashLineVisible, phase.ToString());
            Assert.IsFalse(_view.IsBlastAreaVisible, phase.ToString());
        }
    }

    // 그림 넣기 AC8 — 그림 몸은 배율 1·Sort Point Pivot, 반전·오프셋은 ApplyMotion, 풀에서 다시 Initialize하면 초기화
    [Test]
    public void ArtBodyFlipsAndResetsOnReinitialize()
    {
        var bodyRenderer = _root.GetComponent<SpriteRenderer>();
        var art = CreateSprite(64, 64, new Vector2(0.5f, 0f));
        _view.Initialize(art, null, true, CreateSprite(4, 2, new Vector2(0f, 0.5f)), CreateSprite(64, 64, new Vector2(0.5f, 0f)), 0f, 0f,
            0.0113f);

        Assert.IsTrue(_view.HasArt);
        Assert.AreSame(art, bodyRenderer.sprite);
        Assert.AreEqual(Vector3.one, bodyRenderer.transform.localScale);

        _view.ApplyMotion(true, new Vector2Int(0, -1));
        Assert.IsTrue(_view.IsFlipped);
        Assert.AreEqual(-1f, _view.BodyOffset.y * Const.PixelsPerUnit, 1e-5f);

        _view.Initialize(art, null, true, CreateSprite(4, 2, new Vector2(0f, 0.5f)), CreateSprite(64, 64, new Vector2(0.5f, 0f)), 0f, 0f,
            0.0113f);
        Assert.IsFalse(_view.IsFlipped);
        Assert.AreEqual(Vector3.zero, _view.BodyOffset);
    }

    // 쓰러짐·예고 T2·T3 — 예고 그림이 있으면 Windup·Fuse 동안 예고 그림, 끝나면 평소 그림. 다시 꺼내면 평소 그림
    [TestCase(MonsterPhase.Windup, MonsterPhase.Dash)]
    [TestCase(MonsterPhase.Fuse, MonsterPhase.Approach)]
    public void WarningPhaseSwapsBodyAndBack(MonsterPhase warningPhase, MonsterPhase nextPhase)
    {
        var bodyRenderer = _root.GetComponent<SpriteRenderer>();
        var art = CreateSprite(64, 64, new Vector2(0.5f, 0f));
        var warning = CreateSprite(64, 64, new Vector2(0.5f, 0f));
        _view.Initialize(art, warning, true, CreateSprite(4, 2, new Vector2(0f, 0.5f)), CreateSprite(64, 64, new Vector2(0.5f, 0f)), 0f,
            0f, 0.0113f);

        _view.UpdateState(MonsterPhase.Approach, Vector2.zero);
        Assert.IsFalse(_view.IsWarning);
        Assert.AreSame(art, bodyRenderer.sprite);

        _view.UpdateState(warningPhase, Vector2.right);
        Assert.IsTrue(_view.IsWarning);
        Assert.AreSame(warning, bodyRenderer.sprite);

        _view.UpdateState(warningPhase, Vector2.right);
        Assert.AreSame(warning, bodyRenderer.sprite);

        _view.UpdateState(nextPhase, Vector2.zero);
        Assert.IsFalse(_view.IsWarning);
        Assert.AreSame(art, bodyRenderer.sprite);

        _view.UpdateState(warningPhase, Vector2.right);
        _view.Initialize(art, warning, true, CreateSprite(4, 2, new Vector2(0f, 0.5f)), CreateSprite(64, 64, new Vector2(0.5f, 0f)), 0f,
            0f, 0.0113f);
        Assert.IsFalse(_view.IsWarning);
        Assert.AreSame(art, bodyRenderer.sprite);
    }

    [Test]
    public void NoWarningSpriteKeepsBody()
    {
        var bodyRenderer = _root.GetComponent<SpriteRenderer>();
        var body = bodyRenderer.sprite;

        _view.UpdateState(MonsterPhase.Windup, Vector2.right);
        Assert.IsFalse(_view.IsWarning);
        Assert.AreSame(body, bodyRenderer.sprite);

        _view.UpdateState(MonsterPhase.Fuse, Vector2.zero);
        Assert.IsFalse(_view.IsWarning);
        Assert.AreSame(body, bodyRenderer.sprite);
    }

    [Test]
    public void PrefabBodyIsChildWithPivotSortPoint()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<MonsterView>("Assets/Game/03.Resources/Resources/Prefab/MonsterView.prefab");
        var body = (SpriteRenderer)new SerializedObject(prefab).FindProperty("bodyRenderer").objectReferenceValue;

        Assert.AreNotSame(prefab.gameObject, body.gameObject);
        Assert.AreSame(prefab.transform.Find("Upright"), body.transform.parent);
        Assert.AreEqual(0f, Quaternion.Angle(Quaternion.Euler(30,0,0), body.transform.parent.localRotation), 1e-3f);
        Assert.AreEqual(SpriteSortPoint.Pivot, body.spriteSortPoint);
        Assert.IsNull(prefab.GetComponent<SpriteRenderer>());
    }

    [Test]
    public void GroundWarningCircleAndDashUseFloorMetres()
    {
        var area = _root.transform.Find("BlastArea");
        var renderer = area.GetComponent<SpriteRenderer>();
        var size = Vector3.Scale(renderer.sprite.bounds.size, area.localScale);
        Assert.AreEqual(90f * 2f * .0113f, size.x, 1e-5f);
        Assert.AreEqual(size.x, size.y, 1e-5f);
        Assert.That(Quaternion.Angle(Quaternion.Euler(90f, 0f, 0f), area.localRotation), Is.LessThan(.001f));
        _view.UpdateState(MonsterPhase.Windup, Vector2.up);
        var line = _root.transform.Find("DashLine");
        Assert.That(Vector3.Distance(Vector3.forward, line.right), Is.LessThan(1e-5f));
        Assert.AreEqual(.001f, line.localPosition.y, 1e-6f);
    }

    private static Sprite CreateSprite(int width, int height, Vector2 pivot)
    {
        var texture = new Texture2D(width, height);
        return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, Const.PixelsPerUnit);
    }
}
