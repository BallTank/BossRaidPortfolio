using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Core.Multiplayer
{
    public enum MultiplayerBootstrapState
    {
        Idle,
        InitializingServices,
        SigningIn,
        Ready,
        Failed
    }

    [DisallowMultipleComponent]
    public sealed class MultiplayerServicesBootstrap : MonoBehaviour
    {
        private static MultiplayerServicesBootstrap _instance;

        private Task _initializationTask;
        private MultiplayerBootstrapState _state = MultiplayerBootstrapState.Idle;
        private string _playerId = string.Empty;
        private string _lastErrorMessage = string.Empty;

        public static MultiplayerServicesBootstrap Instance => GetOrCreateInstance();

        public MultiplayerBootstrapState State => _state;
        public bool IsReady => _state == MultiplayerBootstrapState.Ready && !string.IsNullOrEmpty(_playerId);
        public bool IsBusy => _state == MultiplayerBootstrapState.InitializingServices || _state == MultiplayerBootstrapState.SigningIn;
        public string PlayerId => _playerId;
        public string LastErrorMessage => _lastErrorMessage;

        public event Action<MultiplayerBootstrapState> StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        private static MultiplayerServicesBootstrap GetOrCreateInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject host = new GameObject("MultiplayerServicesBootstrap");
            _instance = host.AddComponent<MultiplayerServicesBootstrap>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public Task EnsureInitializedAsync()
        {
            if (IsReady)
            {
                return Task.CompletedTask;
            }

            if (_initializationTask != null && !_initializationTask.IsCompleted)
            {
                return _initializationTask;
            }

            _initializationTask = EnsureInitializedInternalAsync();
            return _initializationTask;
        }

        private async Task EnsureInitializedInternalAsync()
        {
            _lastErrorMessage = string.Empty;

            try
            {
                await EnsureUnityServicesReadyAsync();
                await EnsureAnonymousSignInAsync();

                _playerId = AuthenticationService.Instance.PlayerId ?? string.Empty;
                if (string.IsNullOrEmpty(_playerId))
                {
                    throw new InvalidOperationException("PlayerId is empty after anonymous sign-in.");
                }

                SetState(MultiplayerBootstrapState.Ready);
            }
            catch (Exception ex)
            {
                _playerId = string.Empty;
                _lastErrorMessage = $"UGS bootstrap failed: {ex.Message}";
                SetState(MultiplayerBootstrapState.Failed);
                Debug.LogError(_lastErrorMessage);
                _initializationTask = null;
                throw;
            }
        }

        private static async Task EnsureUnityServicesReadyAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                return;
            }

            MultiplayerServicesBootstrap.Instance.SetState(MultiplayerBootstrapState.InitializingServices);

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
                return;
            }

            DateTime timeoutAt = DateTime.UtcNow.AddSeconds(10d);
            while (UnityServices.State == ServicesInitializationState.Initializing)
            {
                if (DateTime.UtcNow >= timeoutAt)
                {
                    throw new TimeoutException("Unity Services initialization timed out.");
                }

                await Task.Yield();
            }

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                throw new InvalidOperationException($"Unity Services state is {UnityServices.State}.");
            }
        }

        private async Task EnsureAnonymousSignInAsync()
        {
            IAuthenticationService authenticationService = AuthenticationService.Instance;
            if (authenticationService.IsSignedIn && !authenticationService.IsExpired && !string.IsNullOrEmpty(authenticationService.PlayerId))
            {
                return;
            }

            SetState(MultiplayerBootstrapState.SigningIn);
            await authenticationService.SignInAnonymouslyAsync();
        }

        private void SetState(MultiplayerBootstrapState nextState)
        {
            if (_state == nextState)
            {
                return;
            }

            _state = nextState;
            StateChanged?.Invoke(_state);
        }
    }
}
