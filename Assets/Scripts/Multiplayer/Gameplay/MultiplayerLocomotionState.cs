using Unity.Netcode;
using UnityEngine;

namespace Core.Multiplayer
{
    public struct MultiplayerLocomotionState : INetworkSerializable
    {
        private const byte GroundedFlag = 1 << 0;
        private const byte AllowPredictionFlag = 1 << 1;

        public int InputSequence;
        public int ServerTick;
        public Vector3 Position;
        public float Yaw;
        public Vector3 PlanarVelocity;
        public float VerticalVelocity;
        public float JumpTimer;
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

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InputSequence);
            serializer.SerializeValue(ref ServerTick);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref PlanarVelocity);
            serializer.SerializeValue(ref VerticalVelocity);
            serializer.SerializeValue(ref JumpTimer);
            serializer.SerializeValue(ref Flags);
        }
    }
}
