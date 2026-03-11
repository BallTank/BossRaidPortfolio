using System.Collections.Generic;
using UnityEngine;

namespace CombatGirlsCharacterPack
{
    public class MaterialChanger : MonoBehaviour
    {
        [SerializeField] private List<SkinnedMeshRenderer> characterMeshRenderers; // ���� SkinnedMeshRenderer���� ����� �� �ִ� ����Ʈ
        [SerializeField] private List<Material> materials; // ������ ���� ��Ƽ������ ����� �� �ִ� ����Ʈ

        private int currentMaterialIndex = 0; // ���� ���õ� ��Ƽ������ �ε���

        public void ChangeMaterial()
        {
            if (materials.Count == 0 || characterMeshRenderers.Count == 0)
                return; // ����Ʈ�� ��� �ִ� ���, �ƹ� �۾��� ���� ����

            // ���� �ε����� �ش��ϴ� ��Ƽ������ ��� SkinnedMeshRenderer�� ����
            foreach (SkinnedMeshRenderer renderer in characterMeshRenderers)
            {
                renderer.material = materials[currentMaterialIndex];
            }

            // ���� ��Ƽ����� �ε����� �̵�, ����Ʈ ���� �����ϸ� ó������ ���ư�
            currentMaterialIndex = (currentMaterialIndex + 1) % materials.Count;
        }
    }
}