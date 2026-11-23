using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class DQSceneExitIntegrationTests
{
    [UnityTest]
    public IEnumerator QuitStopsEntryPointsBeforeNativeViewsAreDestroyed()
    {
        EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        yield return new EnterPlayMode();
        yield return VerifyAfterEnter();
        yield return new ExitPlayMode();
        LogAssert.NoUnexpectedReceived();
    }

    // Allocate captured assertion locals after the EnterPlayMode domain reload.
    private IEnumerator VerifyAfterEnter()
    {
        yield return null;
        var scope = Object.FindAnyObjectByType<GameSceneLifetimeScope>();
        Assert.That(scope, Is.Not.Null);
        var container = scope.Container;
        var run = (RunService)container.Resolve(typeof(RunService));
        LogAssert.Expect(LogType.Log, "[RunService] Run 1 seed 7 enemyCountScale 0");
        run.StartRun(7, 0);
        yield return null;
        var hud = Object.FindAnyObjectByType<UI_BattleHudView>();
        var member = Object.FindAnyObjectByType<MemberView>();
        Assert.That(hud, Is.Not.Null);
        Assert.That(member, Is.Not.Null);
        var quit = typeof(GameSceneLifetimeScope).GetMethod("OnApplicationQuit", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(quit, Is.Not.Null);
        quit.Invoke(scope, null);
        Assert.That(scope.Container, Is.Null, "Existing scope owner disposes its container before native destruction.");
        Assert.DoesNotThrow(() => quit.Invoke(scope, null), "Unity quit followed by base OnDestroy must be idempotent.");
        Object.Destroy(hud.gameObject);
        Object.Destroy(member.gameObject);
        for (var frame = 0; frame < 4; frame++) yield return null;
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTearDown]
    public IEnumerator ExitIfInterrupted()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
