using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CombatGirlsCharacterPack
{
    public class CameraFollowWalk : CameraWalk
    {
        private Quaternion _initialRotation;
        private Transform _parent;
        private Vector3 initialLocalPosition;

        [Header("ī�޶� ĳ���� ���� ȸ���ϱ�")]
        public bool isRotate = false;
        protected override void Start()
        {
            base.Start();

            _initialRotation = transform.rotation;
            _parent = transform.parent;
            initialLocalPosition = transform.position - _parent.position;
        }
        protected virtual void LateUpdate()
        {
            if (!isRotate)
            {
                transform.rotation = _initialRotation;
                transform.position = _parent.position + initialLocalPosition;
            }


        }
        protected override void myCameraWalk()
        {
            // WASD�� ī�޶� �̵�
            float horizontal = Input.GetAxis("Horizontal") * moveSpeed * Time.deltaTime;
            float vertical = Input.GetAxis("Vertical") * moveSpeed * Time.deltaTime;
            Vector3 prePos = transform.position;
            transform.Translate(horizontal, 0, vertical, Space.Self);
            initialLocalPosition += transform.position - prePos;
            //
            if (Input.GetKey(KeyCode.Q))
            {
                prePos = transform.position;
                transform.Translate(0, -moveSpeed * Time.deltaTime, 0, Space.Self);
                initialLocalPosition += transform.position - prePos;
            }
            if (Input.GetKey(KeyCode.E))
            {
                prePos = transform.position;
                transform.Translate(0, moveSpeed * Time.deltaTime, 0, Space.Self);
                initialLocalPosition += transform.position - prePos;
            }
            // ���콺 �ٷ� Ȯ��/���
            float fov = GetComponent<Camera>().fieldOfView;
            fov -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
            fov = Mathf.Clamp(fov, minFov, maxFov);
            GetComponent<Camera>().fieldOfView = fov;

            // ������ ���콺�� ���콺 Ŀ�� ǥ��/�����
            VisibleMouse();

            // 
            if (!isCursorVisible)
            {
                float mouseX = Input.GetAxis("Mouse X") * rotateSpeed * Time.deltaTime;
                float mouseY = Input.GetAxis("Mouse Y") * rotateSpeed * Time.deltaTime;

                transform.eulerAngles = new Vector3(transform.eulerAngles.x - mouseY, transform.eulerAngles.y + mouseX, 0);
                _initialRotation = transform.rotation;
            }
        }
    }
}