using UnityEngine;
using UnityEngine.EventSystems;

namespace CombatGirlsCharacterPack
{
    public class CameraController_AnimationReset : MonoBehaviour
    {
        public Transform target; // �߽����� �� ��ü
        public float distance = 10f; // ī�޶�� ��ü ������ �Ÿ�
        public float heightOffset = 2f; // ī�޶�� ��ü ������ ����
        public float sensitivity = 5f; // ���콺 ����
        public float rotationSpeedMultiplier = 0.2f; // ȸ�� �ӵ� ������ ���� ����
        public float zoomSpeed = 5f; // �� �ӵ�
        public float yAdjustmentSpeed = 0.2f; // y �� ���� �ӵ�

        private float initialDistance;
        private float initialHeightOffset;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialTargetPosition;
        private Animator targetAnimator; // Ÿ���� Animator ������Ʈ

        private float currentX = 0f;
        private float currentY = 0f;
        private Vector3 dragOrigin;
        private bool isDragging = false;
        private bool isMiddleClickDragging = false;
        private float middleClickDragOriginY;

        private void Start()
        {
            // �ʱ� ī�޶� ���� �� ��ġ ����
            initialDistance = distance;
            initialHeightOffset = heightOffset;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialTargetPosition = target.position;

            // �ʱ� ī�޶� ���� ����
            Vector3 angles = transform.eulerAngles;
            currentX = angles.y;
            currentY = angles.x;

            // Ÿ���� Animator ������Ʈ ��������
            if (target != null)
            {
                targetAnimator = target.GetComponent<Animator>();
            }
        }

        private void Update()
        {
            // UI ��ҿ��� �浹�� Ȯ���ϰ� �����ϱ�
            if (EventSystem.current.IsPointerOverGameObject())
            {
                // ���콺�� UI ��� ���� ������ ī�޶� ��Ʈ�� ������ ����
                //isDragging = false;
                //isMiddleClickDragging = false;
                //return;
            }

            // ���콺 �Է� �ޱ�
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                dragOrigin = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            if (Input.GetMouseButtonDown(2))
            {
                isMiddleClickDragging = true;
                middleClickDragOriginY = Input.mousePosition.y;
            }
            else if (Input.GetMouseButtonUp(2))
            {
                isMiddleClickDragging = false;
            }

            // ���콺 �巡�׷� ī�޶� ȸ���ϱ�
            if (isDragging)
            {
                Vector3 difference = Input.mousePosition - dragOrigin;
                currentX += difference.x * sensitivity * rotationSpeedMultiplier * Time.deltaTime;
                currentY -= difference.y * sensitivity * rotationSpeedMultiplier * Time.deltaTime;

                currentY = Mathf.Clamp(currentY, -90f, 90f);
            }

            // ���콺 �߰� ��ư �巡�׷� y �� �����ϱ�
            if (isMiddleClickDragging)
            {
                float yDifference = (Input.mousePosition.y - middleClickDragOriginY) * yAdjustmentSpeed * Time.deltaTime;
                heightOffset -= yDifference; // heightOffset�� �����Ͽ� ī�޶��� ���̸� ����
                middleClickDragOriginY = Input.mousePosition.y; // ������ ������Ʈ
            }

            // �� ��/�ƿ� ó��
            float zoomInput = Input.GetAxis("Mouse ScrollWheel");
            if (zoomInput != 0f)
            {
                distance -= zoomInput * zoomSpeed;
                distance = Mathf.Clamp(distance, 1f, 100f);
            }

            // ��Ŭ������ �����ϱ�
            if (Input.GetMouseButtonDown(1))
            {
                ResetCamera();
            }

            // ī�޶� ��ġ�� ȸ�� ������Ʈ
            Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
            Vector3 offset = new Vector3(0f, heightOffset, 0f);
            transform.position = target.position + offset - rotation * Vector3.forward * distance;
            transform.LookAt(target.position + offset);
        }

        private void ResetCamera()
        {
            distance = initialDistance;
            heightOffset = initialHeightOffset;
            transform.position = initialPosition;
            transform.rotation = initialRotation;
            currentX = initialRotation.eulerAngles.y;
            currentY = initialRotation.eulerAngles.x;
            target.position = initialTargetPosition;

            // Ÿ���� �ִϸ��̼� ����
            if (targetAnimator != null)
            {
                targetAnimator.Play(targetAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash, -1, 0f);
            }
        }
    }
}