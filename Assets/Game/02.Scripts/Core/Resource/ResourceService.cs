using System.Collections.Generic;
using UnityEngine;

public class ResourceService
{
    public T Load<T>(string path) where T : Object
    {
        var asset = Resources.Load<T>(path);
        if (asset == null)
        {
            GameLogger.LogError($"[ResourceService] No resource at {path}");
        }

        return asset;
    }

    public T LoadJson<T>(string path) => JsonUtility.FromJson<T>(Load<TextAsset>(path).text);

    public List<T> LoadAllJson<T>(string folder)
    {
        var textAssets = Resources.LoadAll<TextAsset>(folder);
        if (textAssets.Length == 0)
        {
            GameLogger.LogError($"[ResourceService] No data files in {folder}");
        }

        var list = new List<T>(textAssets.Length);
        foreach (var textAsset in textAssets)
        {
            list.Add(JsonUtility.FromJson<T>(textAsset.text));
        }

        return list;
    }
}
