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
    private Sprite _downed;
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
        _downed = CreateSprite(64, 64);
        _bar = CreateSprite(14, 2);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
        foreach (var sprite in new[] { _front, _back, _downed, _bar })
        {
            Object.DestroyImmediate(sprite.texture);
            Object.DestroyImmediate(sprite);
        }
    }

    [Test]
    public void ArtStartsFrontRightWithBarAboveCharacterHeight()
    {
        _view.Initialize(_front, _back, _downed, _bar, _bar, true);

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
        _view.Initialize(_front, _front, _front, _bar, _bar, false);

        Assert.IsFalse(_view.HasArt);
        Assert.AreEqual((14 + 2) / (float)Const.PixelsPerUnit, _hpBack.transform.localPosition.y, 1e-6f);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void FacingPicksSpriteAndFlip(bool isBack, bool isFlipped)
    {
        _view.Initialize(_front, _back, _downed, _bar, _bar, true);

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
        _view.Initialize(_front, _back, _downed, _bar, _bar, true);
        var barPosition = _hpBack.transform.localPosition;

        _view.ApplyMotion(SpriteFacing.Front, new Vector2Int(-1, 1), false);

        Assert.AreEqual(-1f, _body.transform.localPosition.x * Const.PixelsPerUnit, 1e-5f);
        Assert.AreEqual(1f, _body.transform.localPosition.y * Const.PixelsPerUnit, 1e-5f);
        Assert.AreEqual(barPosition, _hpBack.transform.localPosition);
        Assert.IsFalse(_flash.enabled);
    }

    // 쓰러짐 T1 — HP 0이면 쓰러진 그림(반전은 쓰러질 때 방향), 0.3초 동안 어두워져 남고, 부활하면 그 방향의 서 있는 그림
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void DownedSwapsSpriteDimsAndRevives(bool isBack, bool isFlipped)
    {
        _view.Initialize(_front, _back, _downed, _bar, _bar, true);
        var facing = new SpriteFacing(isBack, isFlipped);
        _view.ApplyMotion(facing, Vector2Int.zero, false);
        _view.UpdateView(Vector2.zero, 1f, true, 0f);
        Assert.AreEqual(Color.white, _body.color);

        _view.UpdateView(Vector2.zero, 0f, false, 0f);
        _view.ApplyMotion(_view.Facing, Vector2Int.zero, false);
        Assert.IsTrue(_view.IsDowned);
        Assert.AreSame(_downed, _body.sprite);
        Assert.AreSame(_downed, _flash.sprite);
        Assert.AreEqual(isFlipped, _body.flipX);
        Assert.AreEqual(Color.white, _body.color);
        Assert.IsFalse(_hpBack.enabled);

        _view.UpdateView(Vector2.zero, 0f, false, 0.15f);
        var halfDim = _body.color.r;
        Assert.Less(halfDim, 1f);

        _view.UpdateView(Vector2.zero, 0f, false, 0.3f);
        var fullDim = _body.color;
        Assert.Less(fullDim.r, halfDim);
        Assert.AreEqual(1f, fullDim.a);

        _view.UpdateView(Vector2.zero, 0f, false, 5f);
        Assert.AreEqual(fullDim, _body.color);
        Assert.AreSame(_downed, _body.sprite);

        _view.UpdateView(Vector2.zero, 0.5f, true, 0f);
        Assert.IsFalse(_view.IsDowned);
        Assert.AreSame(isBack ? _back : _front, _body.sprite);
        Assert.AreEqual(isFlipped, _body.flipX);
        Assert.AreEqual(Color.white, _body.color);
        Assert.IsTrue(_hpBack.enabled);
    }

    [Test]
    public void ReinitializeStandsUpAgain()
    {
        _view.Initialize(_front, _back, _downed, _bar, _bar, true);
        _view.UpdateView(Vector2.zero, 0f, false, 1f);

        _view.Initialize(_front, _back, _downed, _bar, _bar, true);

        Assert.IsFalse(_view.IsDowned);
        Assert.AreSame(_front, _body.sprite);
        Assert.AreEqual(Color.white, _body.color);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void WalkLoopsAttackOverridesIdleAndDownedOverridesBoth(bool isBack, bool isFlipped)
    {
        var data = JsonUtility.FromJson<MemberMotionData>("{\"fps\":10,\"walkFrames\":2,\"attackFrames\":2,\"hitFront\":1,\"hitBack\":1}");
        var facing = new SpriteFacing(isBack, isFlipped);
        var attack = new MemberAttackMotion();
        _view.Initialize(_front, _back, _downed, _bar, _bar, true);
        _view.InitializeClips(data, new[] { _front, _bar }, new[] { _back, _bar },
            new[] { _downed, _bar }, new[] { _downed, _bar });
        _view.ApplyMotion(facing, Vector2Int.zero, false);
        _view.ApplyAnimation(true, 0.1f, attack);
        Assert.AreSame(_bar, _body.sprite);
        _view.ApplyAnimation(true, 0.2f, attack);
        Assert.AreSame(isBack ? _back : _front, _body.sprite);
        _view.ApplyAnimation(false, 0.1f, attack);
        Assert.AreSame(isBack ? _back : _front, _body.sprite);
        attack.Start(data, facing, 1f);
        _view.ApplyAnimation(true, 0.1f, attack);
        Assert.AreSame(_downed, _body.sprite); // 공격 0번(구분용 그림)
        attack.Advance(0.1f);
        _view.ApplyAnimation(false, 0f, attack);
        Assert.AreSame(_bar, _body.sprite);
        _view.UpdateView(Vector2.zero, 0f, false, 0f);
        _view.ApplyAnimation(true, 0.1f, attack);
        Assert.AreSame(_downed, _body.sprite);
        Assert.AreSame(_body.sprite, _flash.sprite);
        Assert.AreEqual(isFlipped, _body.flipX);
        attack.Stop();
        _view.UpdateView(Vector2.zero, 1f, true, 0f);
        _view.ApplyAnimation(false, 0f, attack);
        Assert.AreSame(isBack ? _back : _front, _body.sprite);
    }

    [Test]
    public void ShadowIsCreatedOnlyOnceAndStaysAtFeetDuringMotion()
    {
        _view.Initialize(_front, _back, _downed, _bar, _bar, true);
        _view.InitializeShadow(_bar);
        _view.InitializeShadow(_bar);
        Assert.AreEqual(1, _root.GetComponentsInChildren<FootShadowView>().Length);
        var shadow = _root.GetComponentInChildren<FootShadowView>();
        var position = shadow.transform.localPosition;
        _view.ApplyMotion(SpriteFacing.Front, Vector2Int.one, true);
        Assert.AreEqual(position, shadow.transform.localPosition);
        Assert.Less(shadow.GetComponent<SpriteRenderer>().sortingOrder, _body.sortingOrder);
        var scale = shadow.transform.localScale.x;
        Assert.AreEqual(Mathf.Round(scale), scale);
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
