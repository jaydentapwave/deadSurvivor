using System.Collections.Generic;
using System.Linq;

public class WaveRepository
{
    private const string Folder = "Data/Wave";

    private readonly List<WaveData> _waveDataList;

    // 웨이브 수 = 웨이브 파일 수(기획서 C절)
    public IReadOnlyList<WaveData> WaveDataList => _waveDataList;

    public WaveRepository(ResourceService resourceService)
    {
        _waveDataList = resourceService.LoadAllJson<WaveData>(Folder).OrderBy(data => data.Order).ToList();
    }
}
