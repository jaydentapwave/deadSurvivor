using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[Category("DQScreen")]
public class FieldOcclusionPresenterTests
{
    [Test]
    public void MonsterOnlyBehindSceneryNeverFadesItAndCannotKeepItFaded()
    {
        var root = new GameObject("Monster occlusion test");
        var normal = new Material(Shader.Find("HD2D/HD2D_SpriteLit"));
        var faded = new Material(normal);
        var texture = new Texture2D(2, 2);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 2);
        try
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            var field = root.AddComponent<FieldView>();
            var data = new SerializedObject(field);
            data.FindProperty("cardMaterial").objectReferenceValue = normal;
            data.FindProperty("ditherSpriteMaterial").objectReferenceValue = faded;
            data.ApplyModifiedPropertiesWithoutUndo();
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.transform.SetParent(root.transform);
            prop.transform.localScale = new Vector3(5, 5, 1);
            var renderer = prop.GetComponent<Renderer>();
            renderer.sharedMaterial = normal;
            field.RegisterOccluder(prop.transform, new[] { renderer });
            var monster = new GameObject("Slime").AddComponent<MonsterView>();
            monster.transform.SetParent(root.transform);
            monster.transform.position = new Vector3(0, 0, 2);
            var body = monster.gameObject.AddComponent<SpriteRenderer>();
            body.sprite = sprite;
            data = new SerializedObject(monster);
            data.FindProperty("bodyRenderer").objectReferenceValue = body;
            data.ApplyModifiedPropertiesWithoutUndo();
            var registry = new MonsterViewRegistry();
            registry.Register(1, monster);
            Assert.IsTrue(OcclusionMath.Covers(OcclusionMath.ProjectBounds(camera, renderer.bounds),
                camera.WorldToViewportPoint(prop.transform.position).z,
                OcclusionMath.ProjectBounds(camera, body.bounds), camera.WorldToViewportPoint(monster.transform.position).z),
                "The monster really is behind and overlapping this prop");
            var presenter = new FieldOcclusionPresenter(field, camera, new MemberView[0]);
            presenter.Tick();
            Assert.AreEqual(0, presenter.FadedCount);
            Assert.AreSame(normal, renderer.sharedMaterial);
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.AreEqual(0, properties.GetInt("_OcclusionRectCount"));
            field.Occluders[0].SetFaded(true);
            presenter.Tick();
            Assert.AreSame(normal, renderer.sharedMaterial, "A remaining monster must not keep the prop faded after the party leaves");
            body.enabled = false;
            presenter.Tick();
            Assert.AreSame(normal, renderer.sharedMaterial);
            body.enabled = true;
            monster.transform.position = new Vector3(0, 0, -2);
            presenter.Tick();
            Assert.AreSame(normal, renderer.sharedMaterial, "Foreground monster must not fade background scenery");
            monster.transform.position = new Vector3(0, 0, 2);
            monster.gameObject.SetActive(false);
            presenter.Tick();
            Assert.AreSame(normal, renderer.sharedMaterial);
            monster.gameObject.SetActive(true);
            presenter.Tick();
            Assert.AreSame(normal, renderer.sharedMaterial);
            registry.Unregister(1);
            presenter.Tick();
            Assert.AreSame(normal, renderer.sharedMaterial);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(normal);
            Object.DestroyImmediate(faded);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void AnyOfFourMembersFadesAndDepartureRestoresTheSameSharedMaterial()
    {
        var root = new GameObject("Occlusion test");
        var normal = new Material(Shader.Find("HD2D/HD2D_SpriteLit"));
        var faded = new Material(normal);
        try
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(0,0,-10);
            camera.orthographic = true;
            camera.orthographicSize = 5;
            var field = root.AddComponent<FieldView>();
            var serialized = new SerializedObject(field);
            serialized.FindProperty("cardMaterial").objectReferenceValue = normal;
            serialized.FindProperty("ditherSpriteMaterial").objectReferenceValue = faded;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.transform.SetParent(root.transform);
            prop.GetComponent<Renderer>().sharedMaterial = normal;
            field.RegisterOccluder(prop.transform, new[] { prop.GetComponent<Renderer>() });
            var members = new MemberView[4];
            for (var i = 0; i < 4; i++)
            {
                var go = new GameObject("Member" + i);
                go.transform.SetParent(root.transform);
                go.transform.position = new Vector3(10,0,2);
                var body = go.AddComponent<SpriteRenderer>();
                var texture = new Texture2D(2,2);
                body.sprite = Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,2);
                members[i] = go.AddComponent<MemberView>();
                var data = new SerializedObject(members[i]);
                data.FindProperty("bodyRenderer").objectReferenceValue = body;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var presenter = new FieldOcclusionPresenter(field,camera,members);
            presenter.Tick();
            Assert.AreEqual(0,presenter.FadedCount);
            members[3].transform.position = new Vector3(0,0,2);
            presenter.Tick();
            Assert.AreEqual(1,presenter.FadedCount);
            Assert.AreSame(faded,prop.GetComponent<Renderer>().sharedMaterial);
            var properties = new MaterialPropertyBlock();
            prop.GetComponent<Renderer>().GetPropertyBlock(properties);
            Assert.AreEqual(1, properties.GetInt("_OcclusionRectCount"));
            var window = properties.GetVectorArray("_OcclusionRects")[0];
            Assert.Greater(window.z, window.x);
            Assert.Less(window.z - window.x, .2f, "A member window must not dither the whole wide prop");
            members[0].transform.position = new Vector3(0,0,2);
            presenter.Tick();
            Assert.AreEqual(1,presenter.FadedCount,"A shared prop counts once even when covering two members");
            members[3].gameObject.SetActive(false);
            members[0].transform.position = new Vector3(10,0,2);
            presenter.Tick();
            Assert.AreEqual(0,presenter.FadedCount);
            Assert.AreSame(normal,prop.GetComponent<Renderer>().sharedMaterial);
            var restoredBlock = new MaterialPropertyBlock();
            prop.GetComponent<Renderer>().GetPropertyBlock(restoredBlock);
            Assert.AreEqual(0,restoredBlock.GetInt("_OcclusionRectCount"));
            members[0].transform.position = new Vector3(0,0,-2);
            presenter.Tick();
            Assert.AreEqual(0,presenter.FadedCount,"A prop behind the member stays opaque");
        }
        finally
        {
            foreach (var body in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var texture = body.sprite.texture;
                Object.DestroyImmediate(body.sprite);
                Object.DestroyImmediate(texture);
            }
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(normal);
            Object.DestroyImmediate(faded);
        }
    }

    [Test]
    public void SeparateActorWindowsKeepTheGapOpaqueAndOverflowRetainsEveryActor()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var material = new Material(Shader.Find("HD2D/HD2D_FieldSurface"));
        try
        {
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var prop = new FieldOccluder(root.transform, new[] { renderer }, m => m);
            var rects = new System.Collections.Generic.List<Rect>
            {
                new Rect(.1f, .4f, .1f, .1f), new Rect(.8f, .4f, .1f, .1f)
            };
            prop.SetOcclusionRects(rects);
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.AreEqual(2, properties.GetInt("_OcclusionRectCount"));
            Assert.Less(properties.GetVectorArray("_OcclusionRects")[0].z, .5f);
            Assert.Greater(properties.GetVectorArray("_OcclusionRects")[1].x, .5f);
            for (var i = 0; i < FieldOccluder.MaxOcclusionRects; i++)
            {
                rects.Add(new Rect(.1f + i * .00001f, .4f, .1f, .1f));
            }
            prop.SetOcclusionRects(rects);
            renderer.GetPropertyBlock(properties);
            var count = properties.GetInt("_OcclusionRectCount");
            var windows = properties.GetVectorArray("_OcclusionRects");
            Assert.AreEqual(FieldOccluder.MaxOcclusionRects, count);
            foreach (var rect in rects)
            {
                var contained = false;
                for (var i = 0; i < count; i++)
                {
                    var window = windows[i];
                    Assert.IsFalse(window.x < .5f && window.z > .5f, "Empty gap must remain opaque");
                    contained |= window.x <= rect.xMin && window.y <= rect.yMin
                        && window.z >= rect.xMax && window.w >= rect.yMax;
                }
                Assert.IsTrue(contained, "Dense crowds must retain every covered actor");
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void MultiRendererPropSwapsWithoutLosingItsDistinctOriginalMaterials()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        child.transform.SetParent(root.transform);
        var a = new Material(Shader.Find("HD2D/HD2D_FieldSurface"));
        var b = new Material(a);
        var fadeA = new Material(a);
        var fadeB = new Material(a);
        try
        {
            var ra = root.GetComponent<Renderer>(); var rb = child.GetComponent<Renderer>();
            ra.sharedMaterial=a; rb.sharedMaterial=b;
            var prop = new FieldOccluder(root.transform,new[] {ra,rb},m=>m==a?fadeA:fadeB);
            prop.SetFaded(true); prop.SetFaded(true);
            Assert.AreSame(fadeA,ra.sharedMaterial); Assert.AreSame(fadeB,rb.sharedMaterial);
            prop.SetFaded(false);
            Assert.AreSame(a,ra.sharedMaterial); Assert.AreSame(b,rb.sharedMaterial);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            Object.DestroyImmediate(fadeA); Object.DestroyImmediate(fadeB);
        }
    }
}
