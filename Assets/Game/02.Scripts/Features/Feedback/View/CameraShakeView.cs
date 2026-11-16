using UnityEngine;

// 카메라 오브젝트에 붙어 흔들림 Presenter가 정한 위치를 쓴다(F6 기획서 D절). 기준·오프셋 계산은 Presenter와 ShakeMath가 한다.
public class CameraShakeView : MonoBehaviour
{
    public Vector3 Position => transform.position;

    public void SetPosition(Vector3 position) => transform.position = position;
}
