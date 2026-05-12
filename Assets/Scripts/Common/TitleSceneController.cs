using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Core.Audio;
using Core.Multiplayer;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Core.GameFlow
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TitleSceneController : MonoBehaviour
    {
        private const string SoundLibraryResourcePath = "SoundLibrary";

        private enum TitlePanelState
        {
            Main,
            MultiplayerMode,
            HostCreate,
            ClientJoin,
            Lobby
        }

        private enum LobbyRole
        {
            None,
            Host,
            Client
        }

        private static readonly Color RootOverlayColor = new Color(0.05f, 0.07f, 0.09f, 0.55f);
        private static readonly Color CardColor = new Color(0.08f, 0.11f, 0.14f, 0.9f);
        private static readonly Color AccentColor = new Color(0.84f, 0.69f, 0.37f, 1f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.2f, 0.24f, 0.96f);
        private static readonly Color InputColor = new Color(0.12f, 0.15f, 0.18f, 0.96f);
        private static readonly Color TextColor = new Color(0.96f, 0.95f, 0.92f, 1f);
        private static readonly Color MutedTextColor = new Color(0.72f, 0.76f, 0.8f, 1f);
        private static readonly Color PopupOverlayColor = new Color(0f, 0f, 0f, 0.72f);

        [Header("Flow")]
        [SerializeField] private GameSceneId _nextSceneId = GameSceneId.GamePlay;

        [Header("Input Guard")]
        [SerializeField, Min(0f)] private float _inputLockDuration = 0.1f;

        [Header("UI Text")]
        [SerializeField] private string _wrongKeyMessage = "Wrong key. Please type again.";

        [Header("UI Prototype")]
        [SerializeField, Min(0f)] private float _hostStartEnableDelay = 2f;
        [SerializeField] private bool _keepRuntimeRootInEditMode = true;

        private float _elapsedTime;
        private float _hostStartTimer;
        private bool _requestedTransition;
        private bool _hostStartCountdownActive;
        private bool _waitingForPressAnyKeyInput;

        private TMP_FontAsset _fontAsset;
        private Canvas _titleCanvas;
        private GameObject _legacyPrompt;

        private GameObject _runtimeRoot;
        private GameObject _mainPanel;
        private GameObject _modePanel;
        private GameObject _hostCreatePanel;
        private GameObject _clientJoinPanel;
        private GameObject _lobbyPanel;
        private GameObject _wrongKeyPopup;

        private TMP_InputField _roomTitleInput;
        private TMP_InputField _joinCodeInput;

        private TMP_Text _roomTitleValueText;
        private TMP_Text _joinCodeValueText;
        private TMP_Text _connectedPlayersValueText;
        private TMP_Text _lobbyStatusText;
        private TMP_Text _lobbyHeaderText;
        private TMP_Text _popupMessageText;

        private Button _soloPlayButton;
        private Button _multiPlayButton;
        private Button _hostModeButton;
        private Button _clientModeButton;
        private Button _createRoomButton;
        private Button _joinRoomButton;
        private Button _startButton;
        private TMP_Text _startButtonLabel;
        private Button _lobbyExitButton;
        private TMP_Text _lobbyExitButtonLabel;

        private Selectable _mainDefaultSelection;
        private Selectable _modeDefaultSelection;
        private Selectable _hostDefaultSelection;
        private Selectable _clientDefaultSelection;
        private Selectable _lobbyDefaultSelection;

        private TitlePanelState _currentPanelState;
        private LobbyRole _currentLobbyRole;
        private string _currentRoomTitle = string.Empty;
        private string _currentJoinCode = string.Empty;
        private int _connectedPlayerCount;

        private void OnEnable()
        {
            if (Application.isPlaying || !_keepRuntimeRootInEditMode)
            {
                return;
            }

            if (EnsureRuntimeUi())
            {
                ShowMainPanel();
                MarkSceneDirtyInEditor();
            }
        }

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneLoader.CancelPendingTransition();
            EnsureRuntimeUi();
            EnsureMultiplayerDriverBound();
            EnterPressAnyKeyGate();
            EnsureLobbyBgmPlayback();
        }

        private void EnsureLobbyBgmPlayback()
        {
            SoundController controller = SoundController.Instance;
            if (controller == null)
            {
                GameObject soundControllerRoot = new GameObject("GlobalSoundController");
                controller = soundControllerRoot.AddComponent<SoundController>();
                DontDestroyOnLoad(soundControllerRoot);
            }

            if (!controller.HasLibrary)
            {
                SoundLibrary library = Resources.Load<SoundLibrary>(SoundLibraryResourcePath);
                if (library != null)
                {
                    controller.SetLibrary(library);
                }
                else
                {
                    Debug.LogWarning($"TitleSceneController: Missing SoundLibrary at Resources/{SoundLibraryResourcePath}.asset");
                }
            }

            controller.Play(SoundId.BgmLobby);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_requestedTransition)
            {
                return;
            }

            if (_waitingForPressAnyKeyInput)
            {
                if (IsKeyboardOrMouseInputDetected())
                {
                    RevealRuntimeUiFromPressAnyKey();
                }

                return;
            }

            _elapsedTime += Time.deltaTime;
            UpdateHostStartCountdown();
        }

        private bool EnsureRuntimeUi()
        {
            if (!ResolveCanvasReferences())
            {
                return false;
            }

            Transform existingRoot = FindPreferredRuntimeRoot(_titleCanvas.transform);
            if (existingRoot != null)
            {
                _runtimeRoot = existingRoot.gameObject;
                ApplyRuntimeRootLayoutContract();
                ResolveRuntimeReferences();

                if (!HasRequiredRuntimeReferences())
                {
                    Debug.LogError("TitleSceneController: Existing TitleRuntimeRoot is missing required children. Delete it and let the controller regenerate the UI.");
                    return false;
                }

                ApplyMainPanelRuntimeLayoutGuard();
                BindUiEvents();
                InitializeDefaultSelections();
                return false;
            }

            BuildRuntimeUi();
            ApplyRuntimeRootLayoutContract();
            ApplyMainPanelRuntimeLayoutGuard();
            BindUiEvents();
            InitializeDefaultSelections();
            return true;
        }

        private bool ResolveCanvasReferences()
        {
            _titleCanvas = FindFirstObjectByType<Canvas>();
            if (_titleCanvas == null)
            {
                Debug.LogError("TitleSceneController: TitleCanvas was not found.");
                return false;
            }

            Transform legacyPromptTransform = FindChildRecursive(_titleCanvas.transform, "Text_PressAnyKey");
            _legacyPrompt = legacyPromptTransform != null ? legacyPromptTransform.gameObject : null;

            TMP_Text fontSource = _legacyPrompt != null
                ? _legacyPrompt.GetComponent<TMP_Text>()
                : _titleCanvas.GetComponentInChildren<TMP_Text>(true);

            _fontAsset = fontSource != null ? fontSource.font : TMP_Settings.defaultFontAsset;
            if (_fontAsset == null)
            {
                Debug.LogError("TitleSceneController: TMP FontAsset was not found.");
                return false;
            }

            if (!Application.isPlaying && _legacyPrompt != null && _legacyPrompt.activeSelf)
            {
                _legacyPrompt.SetActive(false);
                MarkSceneDirtyInEditor();
            }

            return true;
        }

        private static Transform FindPreferredRuntimeRoot(Transform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return null;
            }

            Transform preferredRoot = canvasTransform.Find("TitleRuntimeRoot (1)");
            if (preferredRoot != null)
            {
                return preferredRoot;
            }

            return canvasTransform.Find("TitleRuntimeRoot");
        }

        private void ApplyRuntimeRootLayoutContract()
        {
            if (_runtimeRoot == null)
            {
                return;
            }

            RectTransform runtimeRootRect = _runtimeRoot.GetComponent<RectTransform>();
            if (runtimeRootRect != null)
            {
                if (Application.isPlaying)
                {
                    runtimeRootRect.anchorMin = new Vector2(0.5f, 0.5f);
                    runtimeRootRect.anchorMax = new Vector2(0.5f, 0.5f);
                    runtimeRootRect.anchoredPosition = Vector2.zero;
                    runtimeRootRect.localPosition = Vector3.zero;

                    if (runtimeRootRect.sizeDelta.x <= 0f || runtimeRootRect.sizeDelta.y <= 0f)
                    {
                        runtimeRootRect.sizeDelta = new Vector2(Screen.width, Screen.height);
                    }
                }

                runtimeRootRect.pivot = new Vector2(0.5f, 0.5f);
                runtimeRootRect.localScale = Vector3.one;
                runtimeRootRect.localRotation = Quaternion.identity;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            Animator runtimeRootAnimator = _runtimeRoot.GetComponent<Animator>();
            if (runtimeRootAnimator != null && runtimeRootAnimator.enabled)
            {
                runtimeRootAnimator.enabled = false;
            }
        }

        private void BuildRuntimeUi()
        {
            _runtimeRoot = CreateStretchObject("TitleRuntimeRoot", _titleCanvas.transform, RootOverlayColor);

            TMP_Text titleText = CreateText("TitleLabel", _runtimeRoot.transform, "Boss Raid Portfolio", 48f, FontStyles.Bold, TextAlignmentOptions.Center);
            SetAnchoredRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(760f, 80f));

            TMP_Text subtitleText = CreateText("SubtitleLabel", _runtimeRoot.transform, "Solo keeps the current raid flow. Multi opens Host / Client lobby panels.", 22f, FontStyles.Normal, TextAlignmentOptions.Center);
            subtitleText.color = MutedTextColor;
            subtitleText.enableWordWrapping = true;
            SetAnchoredRect(subtitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(860f, 56f));

            _mainPanel = CreatePanelCard("TitleMainPanel", _runtimeRoot.transform, new Vector2(540f, 360f));
            _modePanel = CreatePanelCard("MultiplayerModePanel", _runtimeRoot.transform, new Vector2(540f, 360f));
            _hostCreatePanel = CreatePanelCard("HostCreatePanel", _runtimeRoot.transform, new Vector2(600f, 420f));
            _clientJoinPanel = CreatePanelCard("ClientJoinPanel", _runtimeRoot.transform, new Vector2(600f, 420f));
            _lobbyPanel = CreatePanelCard("LobbyPanel", _runtimeRoot.transform, new Vector2(660f, 460f));
            _wrongKeyPopup = CreatePopupOverlay("WrongKeyPopup", _runtimeRoot.transform, new Vector2(480f, 220f));

            BuildMainPanel(_mainPanel.transform);
            BuildModePanel(_modePanel.transform);
            BuildHostCreatePanel(_hostCreatePanel.transform);
            BuildClientJoinPanel(_clientJoinPanel.transform);
            BuildLobbyPanel(_lobbyPanel.transform);
            BuildWrongKeyPopup(_wrongKeyPopup.transform);

            MarkSceneDirtyInEditor();
        }

        private void ApplyMainPanelRuntimeLayoutGuard()
        {
            if (!Application.isPlaying || _mainPanel == null)
            {
                return;
            }

            Animator mainPanelAnimator = _mainPanel.GetComponent<Animator>();
            if (mainPanelAnimator != null && mainPanelAnimator.enabled)
            {
                mainPanelAnimator.enabled = false;
            }
        }

        private void BuildMainPanel(Transform parent)
        {
            CreatePanelHeader(parent, "Choose Mode", "Start solo now, or open the multiplayer path.");

            _soloPlayButton = CreateButton(parent, "Solo Play", HandleSoloPlaySelected);
            _multiPlayButton = CreateButton(parent, "Multi Play", HandleMultiPlaySelected);

            _mainDefaultSelection = _soloPlayButton;
        }

        private void BuildModePanel(Transform parent)
        {
            CreatePanelHeader(parent, "Multiplayer", "Pick Host or Client. This step only builds the title UI flow.");

            _hostModeButton = CreateButton(parent, "Host", HandleHostModeSelected);
            _clientModeButton = CreateButton(parent, "Client", HandleClientModeSelected);
            CreateButton(parent, "Back to Title", ShowMainPanel);

            _modeDefaultSelection = _hostModeButton;
        }

        private void BuildHostCreatePanel(Transform parent)
        {
            CreatePanelHeader(parent, "Create Room", "Room title is optional. Empty title uses auto title `join here 0000`.");

            _roomTitleInput = CreateInputField(parent, "RoomTitleInput", "optional room title");
            CreateBodyText(parent, "HostCreateHint", "Create Room moves to the shared lobby panel and shows room title + join code.");

            _createRoomButton = CreateButton(parent, "Create Room", HandleCreateRoomSelected);
            CreateButton(parent, "Back", ShowModePanel);

            _hostDefaultSelection = _roomTitleInput;
        }

        private void BuildClientJoinPanel(Transform parent)
        {
            CreatePanelHeader(parent, "Join Room", "Type a 6-character Relay-style join code. Wrong code shows the popup.");

            _joinCodeInput = CreateInputField(parent, "JoinCodeInput", "relay join code");
            CreateBodyText(parent, "ClientJoinHint", "This UI step validates join-code format only. Relay/Lobby init comes next.");

            _joinRoomButton = CreateButton(parent, "Join", HandleJoinRoomSelected);
            CreateButton(parent, "Back", ShowModePanel);

            _clientDefaultSelection = _joinCodeInput;
        }

        private void BuildLobbyPanel(Transform parent)
        {
            _lobbyHeaderText = CreatePanelHeader(parent, "Lobby", "Host and Client share this waiting panel.");
            _roomTitleValueText = CreateBodyText(parent, "RoomTitleValue", "Room Title: -");
            _joinCodeValueText = CreateBodyText(parent, "JoinCodeValue", "Join Code: -");
            _connectedPlayersValueText = CreateBodyText(parent, "ConnectedPlayersValue", "0/2 connected");
            _connectedPlayersValueText.color = AccentColor;

            _lobbyStatusText = CreateBodyText(parent, "LobbyStatusValue", "Waiting for other player...");
            _lobbyStatusText.color = MutedTextColor;
            _lobbyStatusText.enableWordWrapping = true;

            _startButton = CreateButton(parent, "Start", HandleStartSelected);
            _startButtonLabel = _startButton.GetComponentInChildren<TMP_Text>(true);
            _lobbyExitButton = CreateButton(parent, "Cancel", HandleLobbyExitSelected);
            _lobbyExitButtonLabel = _lobbyExitButton.GetComponentInChildren<TMP_Text>(true);

            _lobbyDefaultSelection = _startButton;
        }

        private void BuildWrongKeyPopup(Transform overlayRoot)
        {
            Transform popupCard = overlayRoot.Find("PopupCard");
            if (popupCard == null)
            {
                return;
            }

            _popupMessageText = CreateText("PopupMessage", popupCard, _wrongKeyMessage, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
            _popupMessageText.enableWordWrapping = true;
            LayoutElement messageLayout = _popupMessageText.gameObject.AddComponent<LayoutElement>();
            messageLayout.preferredHeight = 84f;

            CreateButton(popupCard, "OK", CloseWrongKeyPopup);
        }

        private void ResolveRuntimeReferences()
        {
            if (_runtimeRoot == null)
            {
                return;
            }

            Transform runtimeRootTransform = _runtimeRoot.transform;
            _mainPanel = runtimeRootTransform.Find("TitleMainPanel")?.gameObject;
            _modePanel = runtimeRootTransform.Find("MultiplayerModePanel")?.gameObject;
            _hostCreatePanel = runtimeRootTransform.Find("HostCreatePanel")?.gameObject;
            _clientJoinPanel = runtimeRootTransform.Find("ClientJoinPanel")?.gameObject;
            _lobbyPanel = runtimeRootTransform.Find("LobbyPanel")?.gameObject;
            _wrongKeyPopup = runtimeRootTransform.Find("WrongKeyPopup")?.gameObject;

            _roomTitleInput = FindComponentRecursive<TMP_InputField>(_hostCreatePanel != null ? _hostCreatePanel.transform : null, "RoomTitleInput");
            _joinCodeInput = FindComponentRecursive<TMP_InputField>(_clientJoinPanel != null ? _clientJoinPanel.transform : null, "JoinCodeInput");

            _roomTitleValueText = FindComponentRecursive<TMP_Text>(_lobbyPanel != null ? _lobbyPanel.transform : null, "RoomTitleValue");
            _joinCodeValueText = FindComponentRecursive<TMP_Text>(_lobbyPanel != null ? _lobbyPanel.transform : null, "JoinCodeValue");
            _connectedPlayersValueText = FindComponentRecursive<TMP_Text>(_lobbyPanel != null ? _lobbyPanel.transform : null, "ConnectedPlayersValue");
            _lobbyStatusText = FindComponentRecursive<TMP_Text>(_lobbyPanel != null ? _lobbyPanel.transform : null, "LobbyStatusValue");
            _lobbyHeaderText = FindComponentRecursive<TMP_Text>(_lobbyPanel != null ? _lobbyPanel.transform : null, "Header");
            _popupMessageText = FindComponentRecursive<TMP_Text>(_wrongKeyPopup != null ? _wrongKeyPopup.transform : null, "PopupMessage");

            _soloPlayButton = FindComponentRecursive<Button>(_mainPanel != null ? _mainPanel.transform : null, "Solo PlayButton");
            _multiPlayButton = FindComponentRecursive<Button>(_mainPanel != null ? _mainPanel.transform : null, "Multi PlayButton");
            _hostModeButton = FindComponentRecursive<Button>(_modePanel != null ? _modePanel.transform : null, "HostButton");
            _clientModeButton = FindComponentRecursive<Button>(_modePanel != null ? _modePanel.transform : null, "ClientButton");
            _createRoomButton = FindComponentRecursive<Button>(_hostCreatePanel != null ? _hostCreatePanel.transform : null, "Create RoomButton");
            _joinRoomButton = FindComponentRecursive<Button>(_clientJoinPanel != null ? _clientJoinPanel.transform : null, "JoinButton");
            _startButton = FindComponentRecursive<Button>(_lobbyPanel != null ? _lobbyPanel.transform : null, "StartButton");
            _startButtonLabel = FindComponentRecursive<TMP_Text>(_startButton != null ? _startButton.transform : null, "Label");
            _lobbyExitButton = FindComponentRecursive<Button>(_lobbyPanel != null ? _lobbyPanel.transform : null, "CancelButton");
            _lobbyExitButtonLabel = FindComponentRecursive<TMP_Text>(_lobbyExitButton != null ? _lobbyExitButton.transform : null, "Label");
        }

        private bool HasRequiredRuntimeReferences()
        {
            return _runtimeRoot != null
                   && _mainPanel != null
                   && _modePanel != null
                   && _hostCreatePanel != null
                   && _clientJoinPanel != null
                   && _lobbyPanel != null
                   && _wrongKeyPopup != null
                   && _roomTitleInput != null
                   && _joinCodeInput != null
                   && _roomTitleValueText != null
                   && _joinCodeValueText != null
                   && _connectedPlayersValueText != null
                   && _lobbyStatusText != null
                   && _lobbyHeaderText != null
                   && _popupMessageText != null
                   && _soloPlayButton != null
                   && _multiPlayButton != null
                   && _hostModeButton != null
                   && _clientModeButton != null
                   && _createRoomButton != null
                   && _joinRoomButton != null
                   && _startButton != null
                   && _startButtonLabel != null
                   && _lobbyExitButton != null
                   && _lobbyExitButtonLabel != null
                   && FindComponentRecursive<Button>(_modePanel != null ? _modePanel.transform : null, "Back to TitleButton") != null
                   && FindComponentRecursive<Button>(_hostCreatePanel != null ? _hostCreatePanel.transform : null, "BackButton") != null
                   && FindComponentRecursive<Button>(_clientJoinPanel != null ? _clientJoinPanel.transform : null, "BackButton") != null
                   && FindComponentRecursive<Button>(_wrongKeyPopup != null ? _wrongKeyPopup.transform : null, "OKButton") != null;
        }

        private void BindUiEvents()
        {
            BindButton(_soloPlayButton, HandleSoloPlaySelected);
            BindButton(_multiPlayButton, HandleMultiPlaySelected);
            BindButton(_hostModeButton, HandleHostModeSelected);
            BindButton(_clientModeButton, HandleClientModeSelected);
            BindButton(FindComponentRecursive<Button>(_modePanel != null ? _modePanel.transform : null, "Back to TitleButton"), ShowMainPanel);
            BindButton(_createRoomButton, HandleCreateRoomSelected);
            BindButton(FindComponentRecursive<Button>(_hostCreatePanel != null ? _hostCreatePanel.transform : null, "BackButton"), ShowModePanel);
            BindButton(_joinRoomButton, HandleJoinRoomSelected);
            BindButton(FindComponentRecursive<Button>(_clientJoinPanel != null ? _clientJoinPanel.transform : null, "BackButton"), ShowModePanel);
            BindButton(_startButton, HandleStartSelected);
            BindButton(_lobbyExitButton, HandleLobbyExitSelected);
            BindButton(FindComponentRecursive<Button>(_wrongKeyPopup != null ? _wrongKeyPopup.transform : null, "OKButton"), CloseWrongKeyPopup);
        }

        private void InitializeDefaultSelections()
        {
            _mainDefaultSelection = _soloPlayButton;
            _modeDefaultSelection = _hostModeButton;
            _hostDefaultSelection = _roomTitleInput;
            _clientDefaultSelection = _joinCodeInput;
            _lobbyDefaultSelection = _startButton;
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction onClick)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            EnsurePressSoundComponent(button);
        }

        private static void EnsurePressSoundComponent(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (button.GetComponent<UiPressSoundHook>() == null)
            {
                button.gameObject.AddComponent<UiPressSoundHook>();
            }
        }

        private sealed class UiPressSoundHook : MonoBehaviour, IPointerDownHandler
        {
            public void OnPointerDown(PointerEventData eventData)
            {
                SoundController.Instance?.Play(SoundId.UiButton);
            }
        }

        private static Transform FindChildRecursive(Transform parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == objectName)
                {
                    return children[i];
                }
            }

            return null;
        }

        private static T FindComponentRecursive<T>(Transform parent, string objectName) where T : Component
        {
            Transform child = FindChildRecursive(parent, objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private void MarkSceneDirtyInEditor()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        private TMP_Text CreatePanelHeader(Transform parent, string title, string description)
        {
            TMP_Text header = CreateText("Header", parent, title, 34f, FontStyles.Bold, TextAlignmentOptions.Center);
            LayoutElement headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 58f;

            TMP_Text body = CreateBodyText(parent, "HeaderDescription", description);
            body.alignment = TextAlignmentOptions.Center;
            body.enableWordWrapping = true;
            LayoutElement descriptionLayout = body.gameObject.AddComponent<LayoutElement>();
            descriptionLayout.preferredHeight = 60f;
            return header;
        }

        private TMP_Text CreateBodyText(Transform parent, string objectName, string text)
        {
            TMP_Text body = CreateText(objectName, parent, text, 22f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            body.color = MutedTextColor;
            LayoutElement bodyLayout = body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.preferredHeight = 34f;
            return body;
        }

        private TMP_InputField CreateInputField(Transform parent, string objectName, string placeholderText)
        {
            GameObject rootObject = CreateUiObject(objectName, parent);
            Image background = rootObject.AddComponent<Image>();
            background.color = InputColor;

            Outline outline = rootObject.AddComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(1f, -1f);

            LayoutElement layout = rootObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 68f;

            TMP_InputField inputField = rootObject.AddComponent<TMP_InputField>();
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.characterValidation = TMP_InputField.CharacterValidation.None;
            inputField.caretWidth = 3;
            inputField.selectionColor = new Color(0.84f, 0.69f, 0.37f, 0.3f);

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(0f, 68f);

            GameObject viewportObject = CreateUiObject("Viewport", rootObject.transform);
            RectMask2D mask = viewportObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;

            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(20f, 12f);
            viewportRect.offsetMax = new Vector2(-20f, -12f);

            TMP_Text textComponent = CreateText("Text", viewportObject.transform, string.Empty, 24f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            textComponent.enableWordWrapping = false;
            textComponent.color = TextColor;
            RectTransform textRect = textComponent.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMP_Text placeholder = CreateText("Placeholder", viewportObject.transform, placeholderText, 24f, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            placeholder.enableWordWrapping = false;
            placeholder.color = new Color(0.72f, 0.76f, 0.8f, 0.55f);
            RectTransform placeholderRect = placeholder.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            inputField.textViewport = viewportRect;
            inputField.textComponent = textComponent;
            inputField.placeholder = placeholder;
            inputField.targetGraphic = background;

            return inputField;
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = CreateUiObject(label + "Button", parent);
            Image image = buttonObject.AddComponent<Image>();
            image.color = ButtonColor;

            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = new Color(0.22f, 0.27f, 0.32f, 1f);
            colors.pressedColor = new Color(0.12f, 0.15f, 0.18f, 1f);
            colors.selectedColor = new Color(0.22f, 0.27f, 0.32f, 1f);
            colors.disabledColor = new Color(0.18f, 0.18f, 0.18f, 0.8f);
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(onClick);
            EnsurePressSoundComponent(button);

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 62f;

            TMP_Text labelText = CreateText("Label", buttonObject.transform, label, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        private TMP_Text CreateText(string objectName, Transform parent, string text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            TMP_Text tmpText = textObject.AddComponent<TextMeshProUGUI>();
            tmpText.font = _fontAsset;
            tmpText.text = text;
            tmpText.fontSize = fontSize;
            tmpText.fontStyle = fontStyle;
            tmpText.color = TextColor;
            tmpText.alignment = alignment;
            tmpText.raycastTarget = false;
            return tmpText;
        }

        private GameObject CreatePanelCard(string objectName, Transform parent, Vector2 size)
        {
            GameObject panelObject = CreateUiObject(objectName, parent);
            Image image = panelObject.AddComponent<Image>();
            image.color = CardColor;

            Outline outline = panelObject.AddComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(1f, -1f);

            VerticalLayoutGroup layoutGroup = panelObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.spacing = 16f;
            layoutGroup.padding = new RectOffset(40, 40, 36, 36);

            RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
            SetAnchoredRect(rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), size);

            return panelObject;
        }

        private GameObject CreatePopupOverlay(string objectName, Transform parent, Vector2 popupSize)
        {
            GameObject overlay = CreateStretchObject(objectName, parent, PopupOverlayColor);
            overlay.SetActive(false);

            GameObject popupCard = CreateUiObject("PopupCard", overlay.transform);
            Image image = popupCard.AddComponent<Image>();
            image.color = CardColor;

            Outline outline = popupCard.AddComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(1f, -1f);

            VerticalLayoutGroup layoutGroup = popupCard.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.spacing = 18f;
            layoutGroup.padding = new RectOffset(32, 32, 28, 28);

            RectTransform popupRect = popupCard.GetComponent<RectTransform>();
            SetAnchoredRect(popupRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, popupSize);

            return overlay;
        }

        private GameObject CreateStretchObject(string objectName, Transform parent, Color imageColor)
        {
            GameObject stretchObject = CreateUiObject(objectName, parent);
            Image image = stretchObject.AddComponent<Image>();
            image.color = imageColor;

            RectTransform rectTransform = stretchObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return stretchObject;
        }

        private GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private static void SetAnchoredRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.localScale = Vector3.one;
        }

        private void HandleSoloPlaySelected()
        {
            if (!CanAcceptMenuAction())
            {
                return;
            }

            _requestedTransition = true;
            SceneLoader.Load(_nextSceneId);
        }

        private void HandleMultiPlaySelected()
        {
            if (!CanAcceptMenuAction())
            {
                return;
            }

            ShowModePanel();
        }

        private void HandleHostModeSelected()
        {
            if (!CanAcceptMenuAction())
            {
                return;
            }

            _roomTitleInput.text = string.Empty;
            CloseWrongKeyPopup();
            ShowPanel(TitlePanelState.HostCreate);
        }

        private void HandleClientModeSelected()
        {
            if (!CanAcceptMenuAction())
            {
                return;
            }

            _joinCodeInput.text = string.Empty;
            CloseWrongKeyPopup();
            ShowPanel(TitlePanelState.ClientJoin);
        }

        private void HandleCreateRoomSelected()
        {
            if (!CanAcceptMenuAction())
            {
                return;
            }

            _currentLobbyRole = LobbyRole.Host;
            _currentRoomTitle = ResolveRoomTitle(_roomTitleInput.text);
            _currentJoinCode = GenerateJoinCode(6);
            _connectedPlayerCount = 2;
            _hostStartCountdownActive = true;
            _hostStartTimer = 0f;

            UpdateLobbyVisuals();
            ShowPanel(TitlePanelState.Lobby);
        }

        private void HandleJoinRoomSelected()
        {
            if (!CanAcceptMenuAction())
            {
                return;
            }

            string joinCode = NormalizeJoinCode(_joinCodeInput.text);
            if (!IsValidJoinCode(joinCode))
            {
                ShowWrongKeyPopup();
                return;
            }

            _currentLobbyRole = LobbyRole.Client;
            _currentJoinCode = joinCode;
            _currentRoomTitle = "join here " + joinCode.Substring(joinCode.Length - 4);
            _connectedPlayerCount = 2;
            _hostStartCountdownActive = false;

            UpdateLobbyVisuals();
            ShowPanel(TitlePanelState.Lobby);
        }

        private void HandleStartSelected()
        {
            if (!CanAcceptMenuAction())
            {
                return;
            }

            if (_currentLobbyRole != LobbyRole.Host || _startButton == null || !_startButton.interactable)
            {
                return;
            }

            _requestedTransition = true;
            SceneLoader.Load(_nextSceneId);
        }

        private void HandleLobbyExitSelected()
        {
            if (!CanAcceptMenuAction())
            {
                return;
            }

            ResetLobbyState();
            ShowMainPanel();
        }

        private void UpdateHostStartCountdown()
        {
            if (!_hostStartCountdownActive || _currentLobbyRole != LobbyRole.Host || _currentPanelState != TitlePanelState.Lobby)
            {
                return;
            }

            _hostStartTimer += Time.deltaTime;
            float remainTime = Mathf.Max(0f, _hostStartEnableDelay - _hostStartTimer);

            if (_hostStartTimer < _hostStartEnableDelay)
            {
                if (_lobbyStatusText != null)
                {
                    _lobbyStatusText.text = $"2/2 connected. Start unlocks in {remainTime:0.0}s";
                }

                if (_startButton != null)
                {
                    _startButton.interactable = false;
                }

                return;
            }

            _hostStartCountdownActive = false;

            if (_lobbyStatusText != null)
            {
                _lobbyStatusText.text = "Ready to start";
            }

            if (_startButton != null)
            {
                _startButton.interactable = true;
            }
        }

        private void ShowMainPanel()
        {
            ResetLobbyState();
            ShowPanel(TitlePanelState.Main);
        }

        private void ShowModePanel()
        {
            CloseWrongKeyPopup();
            ShowPanel(TitlePanelState.MultiplayerMode);
        }

        private void ShowPanel(TitlePanelState panelState)
        {
            _currentPanelState = panelState;
            CloseWrongKeyPopup();

            if (_mainPanel != null)
            {
                _mainPanel.SetActive(panelState == TitlePanelState.Main);
            }

            if (_modePanel != null)
            {
                _modePanel.SetActive(panelState == TitlePanelState.MultiplayerMode);
            }

            if (_hostCreatePanel != null)
            {
                _hostCreatePanel.SetActive(panelState == TitlePanelState.HostCreate);
            }

            if (_clientJoinPanel != null)
            {
                _clientJoinPanel.SetActive(panelState == TitlePanelState.ClientJoin);
            }

            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(panelState == TitlePanelState.Lobby);
            }

            SelectDefaultControl(panelState);
        }

        private void SelectDefaultControl(TitlePanelState panelState)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            Selectable selection = panelState switch
            {
                TitlePanelState.Main => _mainDefaultSelection,
                TitlePanelState.MultiplayerMode => _modeDefaultSelection,
                TitlePanelState.HostCreate => _hostDefaultSelection,
                TitlePanelState.ClientJoin => _clientDefaultSelection,
                TitlePanelState.Lobby => _currentLobbyRole == LobbyRole.Host ? _lobbyDefaultSelection : _lobbyExitButton,
                _ => null
            };

            eventSystem.SetSelectedGameObject(selection != null ? selection.gameObject : null);
        }

        private void UpdateLobbyVisuals()
        {
            if (_lobbyHeaderText != null)
            {
                _lobbyHeaderText.text = _currentLobbyRole == LobbyRole.Host ? "Host Lobby" : "Lobby";
            }

            if (_roomTitleValueText != null)
            {
                _roomTitleValueText.text = $"Room Title: {_currentRoomTitle}";
            }

            if (_joinCodeValueText != null)
            {
                _joinCodeValueText.text = $"Join Code: {_currentJoinCode}";
            }

            if (_connectedPlayersValueText != null)
            {
                _connectedPlayersValueText.text = $"{_connectedPlayerCount}/2 connected";
            }

            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(_currentLobbyRole == LobbyRole.Host);
                _startButton.interactable = _currentLobbyRole == LobbyRole.Host && !_hostStartCountdownActive;
            }

            if (_startButtonLabel != null)
            {
                _startButtonLabel.text = "Start";
            }

            if (_lobbyExitButtonLabel != null)
            {
                _lobbyExitButtonLabel.text = _currentLobbyRole == LobbyRole.Host ? "Cancel" : "Back";
            }

            if (_lobbyStatusText == null)
            {
                return;
            }

            if (_currentLobbyRole == LobbyRole.Client)
            {
                _lobbyStatusText.text = "Waiting for host to start...";
                return;
            }

            _lobbyStatusText.text = _hostStartCountdownActive ? "2/2 connected. Checking stable connection..." : "Ready to start";
        }

        private void ShowWrongKeyPopup()
        {
            if (_popupMessageText != null)
            {
                _popupMessageText.text = _wrongKeyMessage;
            }

            if (_wrongKeyPopup != null)
            {
                _wrongKeyPopup.SetActive(true);
            }

            EventSystem.current?.SetSelectedGameObject(_wrongKeyPopup != null ? _wrongKeyPopup.GetComponentInChildren<Button>(true)?.gameObject : null);
        }

        private void EnterPressAnyKeyGate()
        {
            ResetLobbyState();
            CloseWrongKeyPopup();
            _elapsedTime = 0f;
            _waitingForPressAnyKeyInput = _legacyPrompt != null;

            if (_runtimeRoot != null)
            {
                _runtimeRoot.SetActive(!_waitingForPressAnyKeyInput);
            }

            if (_legacyPrompt != null)
            {
                _legacyPrompt.SetActive(_waitingForPressAnyKeyInput);
            }

            if (!_waitingForPressAnyKeyInput)
            {
                ShowMainPanel();
            }
        }

        private void RevealRuntimeUiFromPressAnyKey()
        {
            _waitingForPressAnyKeyInput = false;

            if (_legacyPrompt != null)
            {
                _legacyPrompt.SetActive(false);
            }

            if (_runtimeRoot != null)
            {
                _runtimeRoot.SetActive(true);
            }

            ShowMainPanel();
        }

        private static bool IsKeyboardOrMouseInputDetected()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                return true;
            }

            if (!Input.anyKeyDown)
            {
                return false;
            }

            return !IsAnyJoystickButtonDown();
        }

        private static bool IsAnyJoystickButtonDown()
        {
            for (KeyCode keyCode = KeyCode.JoystickButton0; keyCode <= KeyCode.Joystick8Button19; keyCode++)
            {
                if (Input.GetKeyDown(keyCode))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureMultiplayerDriverBound()
        {
            if (!MultiplayerScenePaths.IsMultiplayerTitleScene(gameObject.scene.path))
            {
                return;
            }

            if (GetComponent<MultiplayerTitleSceneDriver>() != null)
            {
                return;
            }

            gameObject.AddComponent<MultiplayerTitleSceneDriver>();
        }

        public bool CanAcceptMultiplayerMenuAction()
        {
            return CanAcceptMenuAction();
        }

        public string ResolveHostRoomTitleForMultiplayer()
        {
            return ResolveRoomTitle(_roomTitleInput != null ? _roomTitleInput.text : string.Empty);
        }

        public string ResolveClientJoinCodeForMultiplayer()
        {
            return _joinCodeInput != null ? _joinCodeInput.text : string.Empty;
        }

        public void ExecuteJoinRoomSelectedPrototypeForMultiplayer()
        {
            HandleJoinRoomSelected();
        }

        public void ShowMultiplayerLobby(string roomTitle, string joinCode, int connectedPlayerCount, bool isHost, bool canStart, string statusText)
        {
            _currentLobbyRole = isHost ? LobbyRole.Host : LobbyRole.Client;
            _currentRoomTitle = roomTitle ?? string.Empty;
            _currentJoinCode = joinCode ?? string.Empty;
            _connectedPlayerCount = Mathf.Max(0, connectedPlayerCount);
            _hostStartCountdownActive = false;
            _hostStartTimer = 0f;

            ShowPanel(TitlePanelState.Lobby);
            UpdateLobbyVisuals();

            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(isHost);
                _startButton.interactable = isHost && canStart;
            }

            if (_lobbyStatusText != null && !string.IsNullOrEmpty(statusText))
            {
                _lobbyStatusText.text = statusText;
            }
        }

        public void ShowMultiplayerPopup(string message)
        {
            if (_popupMessageText != null)
            {
                _popupMessageText.text = string.IsNullOrEmpty(message) ? _wrongKeyMessage : message;
            }

            if (_wrongKeyPopup != null)
            {
                _wrongKeyPopup.SetActive(true);
            }

            EventSystem.current?.SetSelectedGameObject(_wrongKeyPopup != null ? _wrongKeyPopup.GetComponentInChildren<Button>(true)?.gameObject : null);
        }

        public void ReturnToMainPanelFromMultiplayer()
        {
            ShowMainPanel();
        }

        public void MarkSceneTransitionRequestedForMultiplayer()
        {
            _requestedTransition = true;
        }

        private void CloseWrongKeyPopup()
        {
            if (_wrongKeyPopup != null)
            {
                _wrongKeyPopup.SetActive(false);
            }
        }

        private bool CanAcceptMenuAction()
        {
            return !_requestedTransition && _elapsedTime >= _inputLockDuration;
        }

        private void ResetLobbyState()
        {
            _currentLobbyRole = LobbyRole.None;
            _currentRoomTitle = string.Empty;
            _currentJoinCode = string.Empty;
            _connectedPlayerCount = 0;
            _hostStartCountdownActive = false;
            _hostStartTimer = 0f;
        }

        private string ResolveRoomTitle(string rawRoomTitle)
        {
            if (!string.IsNullOrWhiteSpace(rawRoomTitle))
            {
                return rawRoomTitle.Trim();
            }

            return "join here " + Random.Range(0, 10000).ToString("0000");
        }

        private static string NormalizeJoinCode(string rawJoinCode)
        {
            if (string.IsNullOrWhiteSpace(rawJoinCode))
            {
                return string.Empty;
            }

            return rawJoinCode.Trim().ToUpperInvariant();
        }

        private static bool IsValidJoinCode(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode) || joinCode.Length != 6)
            {
                return false;
            }

            for (int i = 0; i < joinCode.Length; i++)
            {
                char character = joinCode[i];
                if (!char.IsLetterOrDigit(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GenerateJoinCode(int length)
        {
            const string charset = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] buffer = new char[length];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = charset[Random.Range(0, charset.Length)];
            }

            return new string(buffer);
        }
    }
}
