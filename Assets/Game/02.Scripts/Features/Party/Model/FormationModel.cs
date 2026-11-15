using System;
using R3;

// 지금 진형(이번 웨이브)과 대기 진형(다음 웨이브 시작에 적용). 쓰는 쪽은 FormationService뿐이다.
public class FormationModel : IDisposable
{
    private readonly ReactiveProperty<string> _currentId = new(string.Empty);
    private readonly ReactiveProperty<string> _pendingId = new(string.Empty);

    public ReadOnlyReactiveProperty<string> CurrentId => _currentId;
    public ReadOnlyReactiveProperty<string> PendingId => _pendingId;

    public void SetCurrentId(string id) => _currentId.Value = id;

    public void SetPendingId(string id) => _pendingId.Value = id;

    public void Dispose()
    {
        _currentId.Dispose();
        _pendingId.Dispose();
    }
}
