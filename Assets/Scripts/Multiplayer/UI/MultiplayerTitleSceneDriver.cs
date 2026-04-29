using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Core.GameFlow;

namespace Core.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerTitleSceneDriver : MonoBehaviour
    {
        private const string SoloPlayButtonName = "Solo PlayButton";
        private const string CreateRoomButtonName = "Create RoomButton";
        private const string JoinRoomButtonName = "JoinButton";
        private const string StartButtonName = "StartButton";
        private const string CancelButtonName = "CancelButton";

        private readonly Dictionary<Button, bool> _buttonInteractableCache = new Dictionary<Button, bool>(16);

        private TitleSceneController _titleSceneController;
        private Button[] _sceneButtons;
        private Button _soloPlayButton;
        private Button _createRoomButton;
        private Button _joinRoomButton;
        private Button _startButton;
        private Button _lobbyExitButton;
        private TMP_Text _soloPlayButtonLabel;
        private TMP_Text _createRoomButtonLabel;
        private TMP_Text _joinRoomButtonLabel;
        private TMP_Text _startButtonLabel;
        private TMP_Text _lobbyExitButtonLabel;

        private bool _isBusy;

        private void Start()
        {
            if (!Application.isPlaying || !MultiplayerScenePaths.IsMultiplayerTitleScene(gameObject.scene.path))
            {
                enabled = false;
                return;
            }

            if (!TryBindTitleSceneController())
            {
                enabled = false;
                return;
            }

            if (!TryBindSceneButtons())
            {
                enabled = false;
                return;
            }

            MultiplayerSessionService.Instance.StateChanged += HandleSessionStateChanged;
            MultiplayerSessionService.Instance.SnapshotChanged += HandleSessionSnapshotChanged;
            MultiplayerSessionService.Instance.FatalErrorOccurred += HandleFatalError;

            RebindButton(_soloPlayButton, HandleSoloPlaySelected);
            RebindButton(_createRoomButton, HandleCreateRoomSelectedAsync);
            RebindButton(_joinRoomButton, HandleJoinRoomSelectedAsync);
            RebindButton(_startButton, HandleStartSelected);
            RebindButton(_lobbyExitButton, HandleLobbyExitSelectedAsync);

            ApplySessionSnapshot(MultiplayerSessionService.Instance.CurrentSnapshot);
            HandleSessionStateChanged(MultiplayerSessionService.Instance.State);
        }

        private void OnDestroy()
        {
            if (!MultiplayerSessionService.HasInstance)
            {
                return;
            }

            MultiplayerSessionService.Instance.StateChanged -= HandleSessionStateChanged;
            MultiplayerSessionService.Instance.SnapshotChanged -= HandleSessionSnapshotChanged;
            MultiplayerSessionService.Instance.FatalErrorOccurred -= HandleFatalError;
        }

        private bool TryBindTitleSceneController()
        {
            _titleSceneController = GetComponent<TitleSceneController>();
            if (_titleSceneController == null)
            {
                Debug.LogError("MultiplayerTitleSceneDriver: TitleSceneController component is missing.");
                return false;
            }

            return true;
        }

        private bool TryBindSceneButtons()
        {
            _sceneButtons = ResolveRuntimeButtons();
            if (_sceneButtons == null || _sceneButtons.Length == 0)
            {
                Debug.LogError("MultiplayerTitleSceneDriver: Could not resolve runtime root buttons.");
                return false;
            }

            _soloPlayButton = FindSceneButton(SoloPlayButtonName);
            _createRoomButton = FindSceneButton(CreateRoomButtonName);
            _joinRoomButton = FindSceneButton(JoinRoomButtonName);
            _startButton = FindSceneButton(StartButtonName);
            _lobbyExitButton = FindSceneButton(CancelButtonName);

            _soloPlayButtonLabel = ResolveButtonLabel(_soloPlayButton);
            _createRoomButtonLabel = ResolveButtonLabel(_createRoomButton);
            _joinRoomButtonLabel = ResolveButtonLabel(_joinRoomButton);
            _startButtonLabel = ResolveButtonLabel(_startButton);
            _lobbyExitButtonLabel = ResolveButtonLabel(_lobbyExitButton);

            bool hasAllButtons = _soloPlayButton != null
                                 && _createRoomButton != null
                                 && _joinRoomButton != null
                                 && _startButton != null
                                 && _lobbyExitButton != null
                                 && _soloPlayButtonLabel != null
                                 && _createRoomButtonLabel != null
                                 && _joinRoomButtonLabel != null
                                 && _startButtonLabel != null
                                 && _lobbyExitButtonLabel != null;

            if (!hasAllButtons)
            {
                Debug.LogError("MultiplayerTitleSceneDriver: Failed to bind one or more title scene buttons.");
            }

            return hasAllButtons;
        }

        private Button[] ResolveRuntimeButtons()
        {
            Transform runtimeRoot = ResolvePreferredRuntimeRoot();
            if (runtimeRoot != null)
            {
                Button[] runtimeButtons = runtimeRoot.GetComponentsInChildren<Button>(true);
                if (runtimeButtons != null && runtimeButtons.Length > 0)
                {
                    return runtimeButtons;
                }
            }

            Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<Button> sceneButtonList = new List<Button>(allButtons.Length);

            for (int i = 0; i < allButtons.Length; i++)
            {
                Button button = allButtons[i];
                if (button != null && button.gameObject.scene == gameObject.scene)
                {
                    sceneButtonList.Add(button);
                }
            }

            return sceneButtonList.ToArray();
        }

        private Transform ResolvePreferredRuntimeRoot()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                Transform preferredRoot = canvas.transform.Find("TitleRuntimeRoot (1)");
                if (preferredRoot != null)
                {
                    return preferredRoot;
                }

                Transform fallbackRoot = canvas.transform.Find("TitleRuntimeRoot");
                if (fallbackRoot != null)
                {
                    return fallbackRoot;
                }
            }

            return null;
        }

        private Button FindSceneButton(string buttonName)
        {
            for (int i = 0; i < _sceneButtons.Length; i++)
            {
                Button button = _sceneButtons[i];
                if (button != null && string.Equals(button.name, buttonName, StringComparison.Ordinal))
                {
                    return button;
                }
            }

            return null;
        }

        private static TMP_Text ResolveButtonLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        }

        private static void RebindButton(Button button, UnityEngine.Events.UnityAction onClick)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
        }

        private async void HandleCreateRoomSelectedAsync()
        {
            if (_isBusy || !_titleSceneController.CanAcceptMultiplayerMenuAction())
            {
                return;
            }

            _isBusy = true;
            CacheAndLockSceneButtons();
            SetLabel(_createRoomButtonLabel, "Connecting...");

            try
            {
                MultiplayerSessionService sessionService = MultiplayerSessionService.Instance;
                await sessionService.CreateHostSessionAsync(_titleSceneController.ResolveHostRoomTitleForMultiplayer());

                if (this == null || !_titleSceneController)
                {
                    return;
                }

                RestoreSceneButtons();
                ApplySessionSnapshot(sessionService.CurrentSnapshot);
            }
            catch (Exception)
            {
                RestoreSceneButtons();
                _titleSceneController.ReturnToMainPanelFromMultiplayer();
                ShowSessionErrorPopup("Host create failed. Please try again.");
            }
            finally
            {
                SetLabel(_createRoomButtonLabel, "Create Room");
                _isBusy = false;
            }
        }

        private async void HandleJoinRoomSelectedAsync()
        {
            if (_isBusy || !_titleSceneController.CanAcceptMultiplayerMenuAction())
            {
                return;
            }

            _isBusy = true;
            CacheAndLockSceneButtons();
            SetLabel(_joinRoomButtonLabel, "Connecting...");

            MultiplayerSessionService sessionService = MultiplayerSessionService.Instance;

            try
            {
                await sessionService.JoinClientSessionAsync(_titleSceneController.ResolveClientJoinCodeForMultiplayer());

                if (this == null || !_titleSceneController)
                {
                    return;
                }

                RestoreSceneButtons();
                ApplySessionSnapshot(sessionService.CurrentSnapshot);
            }
            catch (Exception)
            {
                RestoreSceneButtons();

                if (sessionService.LastFailureKind == MultiplayerSessionFailureKind.WrongJoinCode)
                {
                    ShowSessionErrorPopup("Wrong key. Please type again.");
                }
                else
                {
                    _titleSceneController.ReturnToMainPanelFromMultiplayer();
                    ShowSessionErrorPopup("Client join failed. Please try again.");
                }
            }
            finally
            {
                SetLabel(_joinRoomButtonLabel, "Join");
                _isBusy = false;
            }
        }

        private async void HandleLobbyExitSelectedAsync()
        {
            if (_isBusy || !_titleSceneController.CanAcceptMultiplayerMenuAction())
            {
                return;
            }

            _isBusy = true;
            CacheAndLockSceneButtons();
            SetLabel(_lobbyExitButtonLabel, "Closing...");

            try
            {
                if (MultiplayerSessionService.Instance.HasActiveSession)
                {
                    await MultiplayerSessionService.Instance.ShutdownSessionAsync();
                }

                if (this == null || !_titleSceneController)
                {
                    return;
                }

                RestoreSceneButtons();
                _titleSceneController.ReturnToMainPanelFromMultiplayer();
            }
            catch (Exception)
            {
                RestoreSceneButtons();
                _titleSceneController.ReturnToMainPanelFromMultiplayer();
                ShowSessionErrorPopup("Session close failed. Please try again.");
            }
            finally
            {
                SetLabel(_lobbyExitButtonLabel, "Cancel");
                _isBusy = false;
            }
        }

        private void HandleSoloPlaySelected()
        {
            if (_isBusy || !_titleSceneController.CanAcceptMultiplayerMenuAction())
            {
                return;
            }

            _titleSceneController.MarkSceneTransitionRequestedForMultiplayer();
            SceneManager.LoadScene(MultiplayerScenePaths.GamePlayScenePath);
        }

        private async void HandleStartSelected()
        {
            if (_isBusy || !_titleSceneController.CanAcceptMultiplayerMenuAction())
            {
                return;
            }

            if (_startButton == null || !_startButton.gameObject.activeInHierarchy || !_startButton.interactable)
            {
                return;
            }

            _isBusy = true;
            CacheAndLockSceneButtons();
            SetLabel(_startButtonLabel, "Starting...");

            MultiplayerSessionService sessionService = MultiplayerSessionService.Instance;

            try
            {
                await sessionService.StartGameplayAsync();
            }
            catch (Exception)
            {
                if (this == null || !_titleSceneController)
                {
                    return;
                }

                RestoreSceneButtons();

                if (!sessionService.HasActiveSession)
                {
                    _titleSceneController.ReturnToMainPanelFromMultiplayer();
                }
                else
                {
                    ApplySessionSnapshot(sessionService.CurrentSnapshot);
                }

                ShowSessionErrorPopup("Gameplay start failed. Please try again.");
            }
            finally
            {
                if (this != null)
                {
                    SetLabel(_startButtonLabel, "Start");
                    _isBusy = false;
                }
            }
        }

        private void HandleSessionSnapshotChanged(MultiplayerSessionSnapshot snapshot)
        {
            ApplySessionSnapshot(snapshot);
        }

        private void HandleSessionStateChanged(MultiplayerSessionState state)
        {
            if (state != MultiplayerSessionState.Closing)
            {
                return;
            }

            _isBusy = true;
            SetLabel(_lobbyExitButtonLabel, "Closing...");
        }

        private void HandleFatalError(string message)
        {
            RestoreSceneButtons();
            ResetActionLabels();
            _isBusy = false;

            if (_titleSceneController == null)
            {
                return;
            }

            _titleSceneController.ReturnToMainPanelFromMultiplayer();
            _titleSceneController.ShowMultiplayerPopup(message);
        }

        private void ApplySessionSnapshot(MultiplayerSessionSnapshot snapshot)
        {
            if (_titleSceneController == null || !snapshot.HasActiveSession)
            {
                return;
            }

            _titleSceneController.ShowMultiplayerLobby(
                snapshot.RoomTitle,
                snapshot.JoinCode,
                snapshot.ConnectedPlayerCount,
                snapshot.IsHost,
                snapshot.CanStart,
                snapshot.LobbyStatusText);

            if (_lobbyExitButtonLabel != null)
            {
                _lobbyExitButtonLabel.text = snapshot.IsHost ? "Cancel" : "Back";
            }
        }

        private void CacheAndLockSceneButtons()
        {
            _buttonInteractableCache.Clear();

            for (int i = 0; i < _sceneButtons.Length; i++)
            {
                Button button = _sceneButtons[i];
                if (button == null)
                {
                    continue;
                }

                _buttonInteractableCache[button] = button.interactable;
                button.interactable = false;
            }
        }

        private void RestoreSceneButtons()
        {
            foreach (KeyValuePair<Button, bool> entry in _buttonInteractableCache)
            {
                if (entry.Key != null)
                {
                    entry.Key.interactable = entry.Value;
                }
            }

            _buttonInteractableCache.Clear();
        }

        private void ShowSessionErrorPopup(string fallbackMessage)
        {
            string errorMessage = MultiplayerSessionService.HasInstance
                ? MultiplayerSessionService.Instance.LastErrorMessage
                : string.Empty;

            if (string.IsNullOrEmpty(errorMessage))
            {
                errorMessage = MultiplayerServicesBootstrap.Instance.LastErrorMessage;
            }

            if (string.IsNullOrEmpty(errorMessage))
            {
                errorMessage = fallbackMessage;
            }

            _titleSceneController.ShowMultiplayerPopup(errorMessage);
        }

        private void ResetActionLabels()
        {
            SetLabel(_createRoomButtonLabel, "Create Room");
            SetLabel(_joinRoomButtonLabel, "Join");
            SetLabel(_startButtonLabel, "Start");
            SetLabel(_lobbyExitButtonLabel, "Cancel");
        }

        private static void SetLabel(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
