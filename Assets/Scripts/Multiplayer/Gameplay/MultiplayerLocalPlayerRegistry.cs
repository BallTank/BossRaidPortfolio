namespace Core.Multiplayer
{
    public static class MultiplayerLocalPlayerRegistry
    {
        public static PlayerController LocalPlayer { get; private set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticState()
        {
            LocalPlayer = null;
        }

        public static void SetLocalPlayer(PlayerController playerController)
        {
            LocalPlayer = playerController;
        }

        public static void Clear()
        {
            LocalPlayer = null;
        }
    }
}
