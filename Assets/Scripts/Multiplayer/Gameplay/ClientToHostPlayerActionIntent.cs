using Core.Player;
using Unity.Netcode;

namespace Core.Multiplayer
{
    public struct ClientToHostPlayerActionIntent : INetworkSerializable
    {
        private const byte SupportedActionMask = (byte)(InputFlag.Dash | InputFlag.Attack);

        public int ActionSequence;
        public int ClientTick;
        public byte RequestedAction;
        public float FacingYaw;

        public InputFlag RequestedFlag => (InputFlag)RequestedAction;

        public bool IsValid
        {
            get
            {
                if ((RequestedAction & ~SupportedActionMask) != 0)
                {
                    return false;
                }

                return RequestedAction == (byte)InputFlag.Dash
                       || RequestedAction == (byte)InputFlag.Attack;
            }
        }

        public static ClientToHostPlayerActionIntent Create(InputFlag requestedFlag, int actionSequence, int clientTick, float facingYaw)
        {
            return new ClientToHostPlayerActionIntent
            {
                ActionSequence = actionSequence,
                ClientTick = clientTick,
                RequestedAction = (byte)requestedFlag,
                FacingYaw = facingYaw
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ActionSequence);
            serializer.SerializeValue(ref ClientTick);
            serializer.SerializeValue(ref RequestedAction);
            serializer.SerializeValue(ref FacingYaw);
        }
    }
}
