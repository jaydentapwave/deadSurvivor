using UnityEngine;
using UnityEngine.UI;

// 정비 화면 대원 자리의 "쓰러짐" 표시와 부활 버튼(기획서 F절 V3). 쓰러진 대원 자리에만 보인다
public class UI_ReviveView : MonoBehaviour
{
    [SerializeField] private Button reviveButton;

    public Button ReviveButton => reviveButton;

    public void UpdateUI(bool isDown) => gameObject.SetActive(isDown);
}
