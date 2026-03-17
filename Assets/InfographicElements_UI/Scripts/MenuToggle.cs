using UnityEngine;

public class MenuToggle : MonoBehaviour
{
    [SerializeField] private GameObject _menu; // Menu 오브젝트를 드래그 앤 드롭

    /// <summary>
    /// Menu 오브젝트의 활성/비활성 상태를 토글한다.
    /// </summary>
    public void ToggleMenu()
    {
        _menu.SetActive(!_menu.activeSelf);
    }
}
