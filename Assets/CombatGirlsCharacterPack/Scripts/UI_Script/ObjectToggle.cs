using System.Collections.Generic;
using UnityEngine;

namespace CombatGirlsCharacterPack
{
    public class ObjectToggle : MonoBehaviour
    {
        [SerializeField] private List<GameObject> objects; // Ȱ��ȭ/��Ȱ��ȭ�� ���� ������Ʈ ����Ʈ
        private int currentIndex = 0; // ���� Ȱ��ȭ�� ������Ʈ�� �ε���

        public void ToggleObjects()
        {
            if (objects.Count == 0)
                return; // ����Ʈ�� ��� �ִ� ���, �ƹ� �۾��� ���� ����

            // ���� Ȱ��ȭ�� ������Ʈ�� ��Ȱ��ȭ
            objects[currentIndex].SetActive(false);

            // ���� ������Ʈ�� �ε����� �̵�, ����Ʈ ���� �����ϸ� ó������ ���ư�
            currentIndex = (currentIndex + 1) % objects.Count;

            // ���� ������Ʈ�� Ȱ��ȭ
            objects[currentIndex].SetActive(true);
        }
    }
}