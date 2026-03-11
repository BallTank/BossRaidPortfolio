using UnityEngine;
using UnityEngine.UI;

namespace CombatGirlsCharacterPack
{
    [System.Serializable]
    public class ObjectGroup
    {
        public GameObject[] objectsToToggle;
        public Button[] buttons;
    }

    public class ObjectController : MonoBehaviour
    {
        public ObjectGroup[] objectGroups;

        private void Start()
        {
            // �� �׷쿡 ���� Ŭ�� �̺�Ʈ �ڵ鷯�� �����մϴ�.
            for (int groupIndex = 0; groupIndex < objectGroups.Length; groupIndex++)
            {
                ObjectGroup group = objectGroups[groupIndex];

                for (int buttonIndex = 0; buttonIndex < group.buttons.Length; buttonIndex++)
                {
                    int buttonIdx = buttonIndex; // Ŭ�������� �ùٸ� ��ư �ε����� ����ϱ� ���� ������ ����ϴ�.
                    group.buttons[buttonIdx].onClick.AddListener(() => ToggleObject(group, buttonIdx));
                }
            }
        }

        private void ToggleObject(ObjectGroup group, int buttonIndex)
        {
            // Ŭ���� ��ư�� �ش��ϴ� ������Ʈ�� �Ѱų� ���ϴ�.
            if (buttonIndex >= 0 && buttonIndex < group.objectsToToggle.Length)
            {
                GameObject obj = group.objectsToToggle[buttonIndex];
                obj.SetActive(!obj.activeSelf); // ���� ���¸� �ݴ�� �����մϴ�.
            }
        }
    }
}