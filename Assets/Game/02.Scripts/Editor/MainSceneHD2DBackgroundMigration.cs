using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MainSceneHD2DBackgroundMigration
{
    private const string Root = "Assets/Game/03.Resources/";
    private const string ScenePath = "Assets/Game/01.Scene/Main.unity";

    [MenuItem("DeadSurvivor/HD2D/Migrate Main Background")]
    public static void Apply()
    {
        throw new InvalidOperationException("This orthographic migration is retired. Use DeadSurvivor/DQ/Apply Main screen.");
    }

}
