using Unity.Netcode;
using UnityEngine;

namespace Core.Multiplayer
{
    public struct MultiplayerLocomotionState : INetworkSerializable
    {
        private const byte GroundedFlag = 1 << 0;
        private const byte AllowPredictionFlag = 1 << 1;
        private const byte DashActiveFlag = 1 << 2;

        public int InputSequence;
        public int ServerTick;
        public Vector3 Position;
        public float Yaw;
        public Vector3 PlanarVelocity;
        public float VerticalVelocity;
        public float JumpTimer;
        public float DashTimer;
        public float DashCooldownTimer;
        public byte LastButtons;
        public byte Flags;

        public bool IsGrounded
        {
            get => (Flags & GroundedFlag) != 0;
            set => Flags = value ? (byte)(Flags | GroundedFlag) : (byte)(Flags & ~GroundedFlag);
        }

        public bool AllowsPrediction
        {
            get => (Flags & AllowPredictionFlag) != 0;
            set => Flags = value ? (byte)(Flags | AllowPredictionFlag) : (byte)(Flags & ~AllowPredictionFlag);
        }

        public bool IsDashActive
        {
            get => (Flags & DashActiveFlag) != 0;
            set => Flags = value ? (byte)(Flags | DashActiveFlag) : (byte)(Flags & ~DashActiveFlag);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InputSequence);
            serializer.SerializeValue(ref ServerTick);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref PlanarVelocity);
            serializer.SerializeValue(ref VerticalVelocity);
            serializer.SerializeValue(ref JumpTimer);
            serializer.SerializeValue(ref DashTimer);
            serializer.SerializeValue(ref DashCooldownTimer);
            serializer.SerializeValue(ref LastButtons);
            serializer.SerializeValue(ref Flags);
        }
    }
}
