using System.Collections.Generic;
using System.Linq;

public class ItemRepository
{
    private const string Folder = "Data/Item";

    private readonly Dictionary<string, ItemData> _itemDict;

    public ItemRepository(ResourceService resourceService)
    {
        _itemDict = resourceService.LoadAllJson<ItemData>(Folder).ToDictionary(data => data.Id);
    }

    public ItemData Get(string id)
    {
        if (!_itemDict.TryGetValue(id, out var data))
        {
            GameLogger.LogError($"[ItemRepository] No item {id}");
        }

        return data;
    }
}
