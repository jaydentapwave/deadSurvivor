using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// 그림 넣기 AC8 — MemberView가 방향대로 앞·뒤 스프라이트와 flipX를 바꾸고, 번쩍임·오프셋은 몸 자식만, 그림이 없으면 F1 크기
public class MemberViewSpriteTests
{
    private GameObject _root;
    private MemberView _view;
    private SpriteRenderer _body;
    private SpriteRenderer _flash;
    private SpriteRenderer _hpBack;
    private Sprite _front;
    private Sprite _back;
    private Sprite _bar;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Member");
        _body = CreateChild("Body", _root.transform);
        _flash = CreateChild("Flash", _body.transform);
        _hpBack = CreateChild("HpBack", _root.transform);
        var hpFill = CreateChild("HpFill", _root.transform);
        _view = _root.AddComponent<MemberView>();
        var serialized = new SerializedObject(_view);
        serialized.FindProperty("bodyRenderer").objectReferenceValue = _body;
        serialized.FindProperty("flashRenderer").objectReferenceValue = _flash;
        serialized.FindProperty("hpBackRenderer").objectReferenceValue = _hpBack;
        serialized.FindProperty("hpFillRenderer").objectReferenceValue = hpFill;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        _front = CreateSprite(64, 64);
        _back = CreateSprite(64, 64);
        _bar = CreateSprite(14, 2);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
        foreach (var sprite in new[] { _front, _back, _bar })
        {
            Object.DestroyImmediate(sprite.texture);
            Object.DestroyImmediate(sprite);
        }
    }

    [Test]
    public void ArtStartsFrontRightWithBarAboveCharacterHeight()
    {
        _view.Initialize(_front, _back, _bar, true);

        Assert.IsTrue(_view.HasArt);
        Assert.AreSame(_front, _body.sprite);
        Assert.IsFalse(_body.flipX);
        Assert.IsFalse(_flash.enabled);
        Assert.AreEqual(Vector3.one, _body.transform.localScale);
        Assert.AreEqual((48 + 2) / (float)Const.PixelsPerUnit, _hpBack.transform.localPosition.y, 1e-6f);
    }

    [Test]
    public void PlaceholderKeepsF1BarHeight()
    {
        _view.Initialize(_front, _front, _bar, false);

        Assert.IsFalse(_view.HasArt);
        Assert.AreEqual((14 + 2) / (float)Const.PixelsPerUnit, _hpBack.transform.localPosition.y, 1e-6f);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void FacingPicksSpriteAndFlip(bool isBack, bool isFlipped)
    {
        _view.Initialize(_front, _back, _bar, true);

        _view.ApplyMotion(new SpriteFacing(isBack, isFlipped), Vector2Int.zero, true);

        Assert.AreSame(isBack ? _back : _front, _body.sprite);
        Assert.AreSame(_body.sprite, _flash.sprite);
        Assert.AreEqual(isFlipped, _body.flipX);
        Assert.AreEqual(isFlipped, _flash.flipX);
        Assert.IsTrue(_flash.enabled);
    }

    [Test]
    public void OffsetMovesBodyOnlyInWholePixels()
    {
        _view.Initialize(_front, _back, _bar, true);
        var barPosition = _hpBack.transform.localPosition;

        _view.ApplyMotion(SpriteFacing.Front, new Vector2Int(-1, 1), false);

        Assert.AreEqual(-1f, _body.transform.localPosition.x * Const.PixelsPerUnit, 1e-5f);
        Assert.AreEqual(1f, _body.transform.localPosition.y * Const.PixelsPerUnit, 1e-5f);
        Assert.AreEqual(barPosition, _hpBack.transform.localPosition);
        Assert.IsFalse(_flash.enabled);
    }

    private static SpriteRenderer CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name).AddComponent<SpriteRenderer>();
        child.transform.SetParent(parent, false);
        return child;
    }

    private static Sprite CreateSprite(int width, int height)
    {
        var texture = new Texture2D(width, height);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), Const.PixelsPerUnit);
    }
}
