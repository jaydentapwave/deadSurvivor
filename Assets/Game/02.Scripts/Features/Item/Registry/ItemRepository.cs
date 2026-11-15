using System.Collections.Generic;
using System.Linq;
using VContainer;

public class ItemRepository
{
    private const string Folder = "Data/Item";

    private readonly Dictionary<string, ItemData> _itemDict;

    public IEnumerable<ItemData> ItemDataList => _itemDict.Values;

    [Inject]
    public ItemRepository(ResourceService resourceService) : this(resourceService.LoadAllJson<ItemData>(Folder))
    {
    }

    // 시험이 만든 아이템 목록으로 만든다
    public ItemRepository(IEnumerable<ItemData> itemDataList)
    {
        _itemDict = itemDataList.ToDictionary(data => data.Id);
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
