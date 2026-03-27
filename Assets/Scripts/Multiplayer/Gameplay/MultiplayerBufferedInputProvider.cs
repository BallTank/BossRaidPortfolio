using Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace Core.Multiplayer
{
    public struct MultiplayerLocomotionInput : INetworkSerializable
    {
        public int InputSequence;
        public Vector2 MoveDirection;
        public float LookYaw;
        public float LookPitch;
        public byte Buttons;

        public PlayerInputPacket ToPlayerInputPacket()
        {
            return new PlayerInputPacket
            {
                moveDir = MoveDirection,
                lookYaw = LookYaw,
                lookPitch = LookPitch,
                buttons = Buttons
            };
        }

        public static MultiplayerLocomotionInput FromPlayerInputPacket(in PlayerInputPacket input, int inputSequence)
        {
            return new MultiplayerLocomotionInput
            {
                InputSequence = inputSequence,
                MoveDirection = Vector2.ClampMagnitude(input.moveDir, 1f),
                LookYaw = input.lookYaw,
                LookPitch = Mathf.Clamp(input.lookPitch, -80f, 80f),
                Buttons = input.buttons
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InputSequence);
            serializer.SerializeValue(ref MoveDirection);
            serializer.SerializeValue(ref LookYaw);
            serializer.SerializeValue(ref LookPitch);
            serializer.SerializeValue(ref Buttons);
        }
    }

    [DisallowMultipleComponent]
    public sealed class MultiplayerBufferedInputProvider : MonoBehaviour, IInputProvider
    {
        private PlayerInputPacket _latestInput;
        private int _latestInputSequence;

        public int LatestInputSequence => _latestInputSequence;

        public PlayerInputPacket GetInput()
        {
            return _latestInput;
        }

        public void SetInput(in MultiplayerLocomotionInput input)
        {
            SetInput(input.ToPlayerInputPacket(), input.InputSequence);
        }

        public void SetInput(PlayerInputPacket input, int inputSequence)
        {
            if (inputSequence < _latestInputSequence)
            {
                return;
            }

            _latestInput = input;
            _latestInputSequence = inputSequence;
        }

        public void Clear()
        {
            _latestInput = default;
            _latestInputSequence = 0;
        }
    }
}
