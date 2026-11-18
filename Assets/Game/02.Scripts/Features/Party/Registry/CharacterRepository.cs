using System.Collections.Generic;
using System.Linq;

public class CharacterRepository
{
    private const string Folder = "Data/Character";

    private readonly List<CharacterData> _characterDataList;
    private readonly Dictionary<string, CharacterData> _characterDict;

    public IReadOnlyList<CharacterData> CharacterDataList => _characterDataList;

    public CharacterRepository(ResourceService resourceService)
    {
        _characterDataList = resourceService.LoadAllJson<CharacterData>(Folder).OrderBy(data => data.Order).ToList();
        _characterDict = _characterDataList.ToDictionary(data => data.Id);
    }

    public CharacterData Get(string id)
    {
        if (!_characterDict.TryGetValue(id, out var data))
        {
            GameLogger.LogError($"[CharacterRepository] No character {id}");
        }

        return data;
    }
}
