using UnityEngine;

namespace CombatGirlsCharacterPack
{
    public class WeaponMaterialChanger : MonoBehaviour
    {
        // ������ ���׸��� �迭
        public Material[] materials;
        // ������ 3D ������Ʈ (��: ����)
        public GameObject targetObject;

        // ���� ���õ� ���׸��� �ε���
        private int currentMaterialIndex = 0;

        // ��ư Ŭ�� �� ȣ���� �Լ�
        public void ChangeMaterial()
        {
            if (materials.Length == 0 || targetObject == null) return;

            // MeshRenderer ������Ʈ ��������
            MeshRenderer renderer = targetObject.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                // ���� ���׸���� ����
                currentMaterialIndex = (currentMaterialIndex + 1) % materials.Length;
                renderer.material = materials[currentMaterialIndex];
            }
        }
    }
}