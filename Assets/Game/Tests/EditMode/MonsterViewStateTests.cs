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
        _view.Initialize(CreateSprite(8, 8, new Vector2(0.5f, 0f)), CreateSprite(4, 2, new Vector2(0f, 0.5f)),
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

    private static Sprite CreateSprite(int width, int height, Vector2 pivot)
    {
        var texture = new Texture2D(width, height);
        return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, Const.PixelsPerUnit);
    }
}
