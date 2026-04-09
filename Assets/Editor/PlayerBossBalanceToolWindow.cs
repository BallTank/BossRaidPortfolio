using System;
using System.IO;
using System.Reflection;
using System.Text;
using Core.Boss;
using Core.Combat;
using Core.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Editor
{
    internal sealed class PlayerBossBalanceToolWindow : EditorWindow
    {
        private enum PlayerExportSource
        {
            PrefabAsset,
            Scene
        }

        private enum BossExportSource
        {
            Scene,
            PrefabAsset
        }

        private const int CurrentSchemaVersion = 2;
        private const string MenuItemPath = "Tools/Balance/Open Player Boss Balance Tool";
        private const string DefaultPlayerPrefabPath = "Assets/Resources/Multiplayer/MultiplayerPlayerAvatar.prefab";
        private const string DefaultScenePath = "Assets/Scenes/mutiplayer/GamePlayScene_Verify.unity";
        private const string DefaultJsonRelativePath = "Assets/Balance/player_boss_balance.json";

        [SerializeField] private GameObject _playerPrefabAsset;
        [SerializeField] private GameObject _bossPrefabAsset;
        [SerializeField] private SceneAsset _balanceSceneAsset;
        [SerializeField] private string _jsonFilePath = DefaultJsonRelativePath;
        [SerializeField] private PlayerExportSource _playerExportSource = PlayerExportSource.PrefabAsset;
        [SerializeField] private BossExportSource _bossExportSource = BossExportSource.Scene;

        [MenuItem(MenuItemPath)]
        private static void OpenWindow()
        {
            PlayerBossBalanceToolWindow window = GetWindow<PlayerBossBalanceToolWindow>("Balance Tool");
            window.minSize = new Vector2(520f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadDefaultsIfNeeded();
        }

        private void OnGUI()
        {
            LoadDefaultsIfNeeded();

            EditorGUILayout.LabelField("Player & Boss Balance JSON Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool exports or imports one combined JSON file for Player and Boss balance data. " +
                "Scene flow uses the assigned verify scene, and prefab flow uses the assigned prefab assets.",
                MessageType.Info);

            DrawJsonPathSection();
            DrawTargetSection();
            DrawSourceSection();
            DrawActionSection();
        }

        private void DrawJsonPathSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("JSON File", EditorStyles.boldLabel);
            _jsonFilePath = EditorGUILayout.TextField("Path", _jsonFilePath);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Browse Save Path"))
                {
                    BrowseSavePath();
                }

                if (GUILayout.Button("Browse Existing File"))
                {
                    BrowseOpenPath();
                }

                if (GUILayout.Button("Use Default"))
                {
                    _jsonFilePath = DefaultJsonRelativePath;
                }
            }
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
            _playerPrefabAsset = (GameObject)EditorGUILayout.ObjectField("Player Prefab", _playerPrefabAsset, typeof(GameObject), false);
            _bossPrefabAsset = (GameObject)EditorGUILayout.ObjectField("Boss Prefab", _bossPrefabAsset, typeof(GameObject), false);
            _balanceSceneAsset = (SceneAsset)EditorGUILayout.ObjectField("Balance Scene", _balanceSceneAsset, typeof(SceneAsset), false);
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Export Source", EditorStyles.boldLabel);
            _playerExportSource = (PlayerExportSource)EditorGUILayout.EnumPopup("Player", _playerExportSource);
            _bossExportSource = (BossExportSource)EditorGUILayout.EnumPopup("Boss", _bossExportSource);
        }

        private void DrawActionSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Export Combined JSON", GUILayout.Height(32f)))
            {
                ExportCombinedJson();
            }

            if (GUILayout.Button("Import JSON To Prefab Targets", GUILayout.Height(28f)))
            {
                ImportJsonToPrefabTargets();
            }

            if (GUILayout.Button("Import JSON To Scene Targets", GUILayout.Height(28f)))
            {
                ImportJsonToSceneTargets();
            }

            if (GUILayout.Button("Import JSON To All Assigned Targets", GUILayout.Height(28f)))
            {
                ImportJsonToAllTargets();
            }
        }

        private void ExportCombinedJson()
        {
            try
            {
                if (!TryBuildBalanceFile(out CombinedBalanceFile balanceFile, out string error))
                {
                    EditorUtility.DisplayDialog("Balance Export Failed", error, "OK");
                    return;
                }

                string absolutePath = EnsureAbsoluteJsonPath();
                if (string.IsNullOrEmpty(absolutePath))
                {
                    return;
                }

                WriteUtf8Text(absolutePath, JsonUtility.ToJson(balanceFile, true));
                RefreshAssetDatabaseIfNeeded(absolutePath);

                Debug.Log($"[PlayerBossBalanceTool] Exported combined balance JSON: {absolutePath}");
                EditorUtility.DisplayDialog("Balance Export", $"Export completed.\n{absolutePath}", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Balance Export Failed", ex.Message, "OK");
            }
        }

        private void ImportJsonToPrefabTargets()
        {
            try
            {
                if (!TryLoadBalanceFile(out CombinedBalanceFile balanceFile, out string error))
                {
                    EditorUtility.DisplayDialog("Balance Import Failed", error, "OK");
                    return;
                }

                if (!TryImportToPrefabTargets(balanceFile, out error))
                {
                    EditorUtility.DisplayDialog("Balance Import Failed", error, "OK");
                    return;
                }

                Debug.Log("[PlayerBossBalanceTool] Imported balance JSON to prefab targets.");
                EditorUtility.DisplayDialog("Balance Import", "Prefab import completed.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Balance Import Failed", ex.Message, "OK");
            }
        }

        private void ImportJsonToSceneTargets()
        {
            try
            {
                if (!TryLoadBalanceFile(out CombinedBalanceFile balanceFile, out string error))
                {
                    EditorUtility.DisplayDialog("Balance Import Failed", error, "OK");
                    return;
                }

                if (!TryImportToSceneTargets(balanceFile, out error))
                {
                    EditorUtility.DisplayDialog("Balance Import Failed", error, "OK");
                    return;
                }

                Debug.Log("[PlayerBossBalanceTool] Imported balance JSON to scene targets.");
                EditorUtility.DisplayDialog("Balance Import", "Scene import completed.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Balance Import Failed", ex.Message, "OK");
            }
        }

        private void ImportJsonToAllTargets()
        {
            try
            {
                if (!TryLoadBalanceFile(out CombinedBalanceFile balanceFile, out string error))
                {
                    EditorUtility.DisplayDialog("Balance Import Failed", error, "OK");
                    return;
                }

                bool importedAnything = false;

                if (_playerPrefabAsset != null || _bossPrefabAsset != null)
                {
                    if (!TryImportToPrefabTargets(balanceFile, out error))
                    {
                        EditorUtility.DisplayDialog("Balance Import Failed", error, "OK");
                        return;
                    }

                    importedAnything = true;
                }

                if (_balanceSceneAsset != null)
                {
                    if (!TryImportToSceneTargets(balanceFile, out error))
                    {
                        EditorUtility.DisplayDialog("Balance Import Failed", error, "OK");
                        return;
                    }

                    importedAnything = true;
                }

                if (!importedAnything)
                {
                    EditorUtility.DisplayDialog("Balance Import Failed", "Assign at least one prefab or scene target first.", "OK");
                    return;
                }

                Debug.Log("[PlayerBossBalanceTool] Imported balance JSON to all assigned targets.");
                EditorUtility.DisplayDialog("Balance Import", "Import completed for all assigned targets.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Balance Import Failed", ex.Message, "OK");
            }
        }

        private bool TryBuildBalanceFile(out CombinedBalanceFile balanceFile, out string error)
        {
            balanceFile = new CombinedBalanceFile
            {
                schemaVersion = CurrentSchemaVersion,
                exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                player = new PlayerBalanceData(),
                boss = new BossBalanceData()
            };

            error = null;
            BalanceSceneScope sceneScope = null;
            BalanceSceneTargets sceneTargets = default;

            try
            {
                bool needsSceneTargets = _playerExportSource == PlayerExportSource.Scene || _bossExportSource == BossExportSource.Scene;
                if (needsSceneTargets)
                {
                    if (!TryOpenBalanceScene(out sceneScope, out sceneTargets, out error))
                    {
                        return false;
                    }
                }

                if (_playerExportSource == PlayerExportSource.PrefabAsset)
                {
                    if (!TryReadPlayerFromPrefab(out PlayerBalanceData playerData, out error))
                    {
                        return false;
                    }

                    balanceFile.player = playerData;
                }
                else
                {
                    balanceFile.player = BalanceSerializedMapper.CapturePlayer(sceneTargets.Player.Controller, sceneTargets.Player.Health);
                }

                if (_bossExportSource == BossExportSource.PrefabAsset)
                {
                    if (!TryReadBossFromPrefab(out BossBalanceData bossData, out error))
                    {
                        return false;
                    }

                    balanceFile.boss = bossData;
                }
                else
                {
                    balanceFile.boss = BalanceSerializedMapper.CaptureBoss(sceneTargets.Boss.Controller, sceneTargets.Boss.Health);
                }

                return true;
            }
            finally
            {
                sceneScope?.Dispose();
            }
        }

        private bool TryLoadBalanceFile(out CombinedBalanceFile balanceFile, out string error)
        {
            balanceFile = null;
            error = null;

            if (string.IsNullOrWhiteSpace(_jsonFilePath))
            {
                error = "Set a JSON file path first.";
                return false;
            }

            string absolutePath = ResolveAbsolutePath(_jsonFilePath);
            if (!File.Exists(absolutePath))
            {
                error = $"JSON file not found.\n{absolutePath}";
                return false;
            }

            string json = File.ReadAllText(absolutePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = $"JSON file is empty.\n{absolutePath}";
                return false;
            }

            balanceFile = JsonUtility.FromJson<CombinedBalanceFile>(json);
            if (balanceFile == null)
            {
                error = $"Failed to parse JSON.\n{absolutePath}";
                return false;
            }

            if (balanceFile.player == null)
            {
                balanceFile.player = new PlayerBalanceData();
            }

            if (balanceFile.boss == null)
            {
                balanceFile.boss = new BossBalanceData();
            }

            return true;
        }

        private bool TryImportToPrefabTargets(CombinedBalanceFile balanceFile, out string error)
        {
            error = null;
            bool appliedAny = false;

            if (_playerPrefabAsset != null)
            {
                if (!TryApplyPlayerToPrefab(balanceFile.player, out error))
                {
                    return false;
                }

                appliedAny = true;
            }

            if (_bossPrefabAsset != null)
            {
                if (!TryApplyBossToPrefab(balanceFile.boss, out error))
                {
                    return false;
                }

                appliedAny = true;
            }

            if (!appliedAny)
            {
                error = "No prefab targets are assigned.";
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        private bool TryImportToSceneTargets(CombinedBalanceFile balanceFile, out string error)
        {
            error = null;

            if (_balanceSceneAsset == null)
            {
                error = "Assign a balance scene first.";
                return false;
            }

            BalanceSceneScope sceneScope = null;

            try
            {
                if (!TryOpenBalanceScene(out sceneScope, out BalanceSceneTargets sceneTargets, out error))
                {
                    return false;
                }

                BalanceSerializedMapper.ApplyPlayer(sceneTargets.Player.Controller, sceneTargets.Player.Health, balanceFile.player);
                BalanceSerializedMapper.ApplyBoss(sceneTargets.Boss.Controller, sceneTargets.Boss.Health, balanceFile.boss);

                EditorSceneManager.MarkSceneDirty(sceneScope.Scene);
                if (!EditorSceneManager.SaveScene(sceneScope.Scene))
                {
                    error = $"Failed to save scene.\n{sceneScope.Scene.path}";
                    return false;
                }

                return true;
            }
            finally
            {
                sceneScope?.Dispose();
            }
        }

        private bool TryReadPlayerFromPrefab(out PlayerBalanceData balanceData, out string error)
        {
            balanceData = new PlayerBalanceData();
            error = null;

            if (!TryOpenPrefabContents(_playerPrefabAsset, "player prefab", out PrefabContentsScope prefabScope, out error))
            {
                return false;
            }

            try
            {
                if (!TryResolvePlayerTarget(prefabScope.Root, "player prefab", out PlayerBalanceTarget target, out error))
                {
                    return false;
                }

                balanceData = BalanceSerializedMapper.CapturePlayer(target.Controller, target.Health);
                return true;
            }
            finally
            {
                prefabScope.Dispose();
            }
        }

        private bool TryReadBossFromPrefab(out BossBalanceData balanceData, out string error)
        {
            balanceData = new BossBalanceData();
            error = null;

            if (!TryOpenPrefabContents(_bossPrefabAsset, "boss prefab", out PrefabContentsScope prefabScope, out error))
            {
                return false;
            }

            try
            {
                if (!TryResolveBossTarget(prefabScope.Root, "boss prefab", out BossBalanceTarget target, out error))
                {
                    return false;
                }

                balanceData = BalanceSerializedMapper.CaptureBoss(target.Controller, target.Health);
                return true;
            }
            finally
            {
                prefabScope.Dispose();
            }
        }

        private bool TryApplyPlayerToPrefab(PlayerBalanceData balanceData, out string error)
        {
            error = null;

            if (!TryOpenPrefabContents(_playerPrefabAsset, "player prefab", out PrefabContentsScope prefabScope, out error))
            {
                return false;
            }

            try
            {
                if (!TryResolvePlayerTarget(prefabScope.Root, "player prefab", out PlayerBalanceTarget target, out error))
                {
                    return false;
                }

                BalanceSerializedMapper.ApplyPlayer(target.Controller, target.Health, balanceData);
                prefabScope.Save();
                return true;
            }
            finally
            {
                prefabScope.Dispose();
            }
        }

        private bool TryApplyBossToPrefab(BossBalanceData balanceData, out string error)
        {
            error = null;

            if (!TryOpenPrefabContents(_bossPrefabAsset, "boss prefab", out PrefabContentsScope prefabScope, out error))
            {
                return false;
            }

            try
            {
                if (!TryResolveBossTarget(prefabScope.Root, "boss prefab", out BossBalanceTarget target, out error))
                {
                    return false;
                }

                BalanceSerializedMapper.ApplyBoss(target.Controller, target.Health, balanceData);
                prefabScope.Save();
                return true;
            }
            finally
            {
                prefabScope.Dispose();
            }
        }

        private bool TryOpenBalanceScene(out BalanceSceneScope sceneScope, out BalanceSceneTargets sceneTargets, out string error)
        {
            sceneScope = null;
            sceneTargets = default;
            error = null;

            if (_balanceSceneAsset == null)
            {
                error = "Assign a balance scene first.";
                return false;
            }

            if (!BalanceSceneScope.TryOpen(_balanceSceneAsset, out sceneScope, out error))
            {
                return false;
            }

            if (!TryResolveSceneTargets(sceneScope.Scene, out sceneTargets, out error))
            {
                sceneScope.Dispose();
                sceneScope = null;
                return false;
            }

            return true;
        }

        private static bool TryOpenPrefabContents(GameObject prefabAsset, string label, out PrefabContentsScope prefabScope, out string error)
        {
            prefabScope = null;
            error = null;

            if (prefabAsset == null)
            {
                error = $"Assign a {label} first.";
                return false;
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(prefabPath))
            {
                error = $"Failed to resolve path for {label}.";
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                error = $"Failed to open {label}.\n{prefabPath}";
                return false;
            }

            prefabScope = new PrefabContentsScope(prefabPath, root);
            return true;
        }

        private static bool TryResolveSceneTargets(Scene scene, out BalanceSceneTargets sceneTargets, out string error)
        {
            sceneTargets = default;
            error = null;

            GameObject[] rootObjects = scene.GetRootGameObjects();
            if (rootObjects == null || rootObjects.Length == 0)
            {
                error = $"Scene has no root objects.\n{scene.path}";
                return false;
            }

            if (!TryResolveUniqueComponent(rootObjects, "scene player", out PlayerController playerController, out error))
            {
                return false;
            }

            if (!TryResolveUniqueComponent(rootObjects, "scene boss", out BossController bossController, out error))
            {
                return false;
            }

            Health playerHealth = playerController.GetComponent<Health>();
            if (playerHealth == null)
            {
                error = $"Health component is missing on scene player.\n{playerController.name}";
                return false;
            }

            Health bossHealth = bossController.GetComponent<Health>();
            if (bossHealth == null)
            {
                error = $"Health component is missing on scene boss.\n{bossController.name}";
                return false;
            }

            sceneTargets = new BalanceSceneTargets(
                new PlayerBalanceTarget(playerController, playerHealth),
                new BossBalanceTarget(bossController, bossHealth));
            return true;
        }

        private static bool TryResolvePlayerTarget(GameObject root, string label, out PlayerBalanceTarget target, out string error)
        {
            target = default;
            error = null;

            PlayerController[] controllers = root.GetComponentsInChildren<PlayerController>(true);
            if (controllers == null || controllers.Length == 0)
            {
                error = $"No PlayerController found in {label}.";
                return false;
            }

            if (controllers.Length > 1)
            {
                error = $"More than one PlayerController found in {label}.";
                return false;
            }

            Health health = controllers[0].GetComponent<Health>();
            if (health == null)
            {
                error = $"Health component is missing on {label}.\n{controllers[0].name}";
                return false;
            }

            target = new PlayerBalanceTarget(controllers[0], health);
            return true;
        }

        private static bool TryResolveBossTarget(GameObject root, string label, out BossBalanceTarget target, out string error)
        {
            target = default;
            error = null;

            BossController[] controllers = root.GetComponentsInChildren<BossController>(true);
            if (controllers == null || controllers.Length == 0)
            {
                error = $"No BossController found in {label}.";
                return false;
            }

            if (controllers.Length > 1)
            {
                error = $"More than one BossController found in {label}.";
                return false;
            }

            Health health = controllers[0].GetComponent<Health>();
            if (health == null)
            {
                error = $"Health component is missing on {label}.\n{controllers[0].name}";
                return false;
            }

            target = new BossBalanceTarget(controllers[0], health);
            return true;
        }

        private static bool TryResolveUniqueComponent<T>(GameObject[] rootObjects, string label, out T component, out string error)
            where T : Component
        {
            component = null;
            error = null;

            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                T[] matches = rootObjects[rootIndex].GetComponentsInChildren<T>(true);
                for (int matchIndex = 0; matchIndex < matches.Length; matchIndex++)
                {
                    if (component != null)
                    {
                        error = $"More than one {typeof(T).Name} found while resolving {label}.";
                        component = null;
                        return false;
                    }

                    component = matches[matchIndex];
                }
            }

            if (component == null)
            {
                error = $"No {typeof(T).Name} found while resolving {label}.";
                return false;
            }

            return true;
        }

        private void LoadDefaultsIfNeeded()
        {
            if (_playerPrefabAsset == null)
            {
                _playerPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
            }

            if (_balanceSceneAsset == null)
            {
                _balanceSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultScenePath);
            }

            if (string.IsNullOrWhiteSpace(_jsonFilePath))
            {
                _jsonFilePath = DefaultJsonRelativePath;
            }
        }

        private void BrowseSavePath()
        {
            string defaultAbsolutePath = ResolveAbsolutePath(string.IsNullOrWhiteSpace(_jsonFilePath) ? DefaultJsonRelativePath : _jsonFilePath);
            string directory = Path.GetDirectoryName(defaultAbsolutePath);
            string fileName = Path.GetFileName(defaultAbsolutePath);
            string selectedPath = EditorUtility.SaveFilePanel("Save Balance JSON", directory, fileName, "json");
            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            _jsonFilePath = NormalizePathForEditor(selectedPath);
        }

        private void BrowseOpenPath()
        {
            string defaultAbsolutePath = ResolveAbsolutePath(string.IsNullOrWhiteSpace(_jsonFilePath) ? DefaultJsonRelativePath : _jsonFilePath);
            string directory = Path.GetDirectoryName(defaultAbsolutePath);
            string selectedPath = EditorUtility.OpenFilePanel("Open Balance JSON", directory, "json");
            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            _jsonFilePath = NormalizePathForEditor(selectedPath);
        }

        private string EnsureAbsoluteJsonPath()
        {
            if (string.IsNullOrWhiteSpace(_jsonFilePath))
            {
                _jsonFilePath = DefaultJsonRelativePath;
            }

            string absolutePath = ResolveAbsolutePath(_jsonFilePath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _jsonFilePath = NormalizePathForEditor(absolutePath);
            return absolutePath;
        }

        private static void WriteUtf8Text(string absolutePath, string text)
        {
            File.WriteAllText(absolutePath, text, new UTF8Encoding(false));
        }

        private static string ResolveAbsolutePath(string path)
        {
            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(GetProjectRootPath(), path));
        }

        private static string NormalizePathForEditor(string path)
        {
            string absolutePath = Path.GetFullPath(path);
            string projectRootPath = GetProjectRootPath();
            string normalizedProjectRoot = projectRootPath.Replace('\\', '/').TrimEnd('/');
            string normalizedAbsolutePath = absolutePath.Replace('\\', '/');

            if (normalizedAbsolutePath.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedAbsolutePath.Substring(normalizedProjectRoot.Length + 1);
            }

            return absolutePath;
        }

        private static string GetProjectRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static void RefreshAssetDatabaseIfNeeded(string absolutePath)
        {
            string normalizedProjectRoot = GetProjectRootPath().Replace('\\', '/').TrimEnd('/') + "/";
            string normalizedAbsolutePath = absolutePath.Replace('\\', '/');
            if (normalizedAbsolutePath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.Refresh();
            }
        }
    }

    [Serializable]
    internal sealed class CombinedBalanceFile
    {
        public int schemaVersion;
        public string exportedAt;
        public PlayerBalanceData player;
        public BossBalanceData boss;
    }

    [Serializable]
    internal sealed class HealthBalanceData
    {
        public int maxHealth;
    }

    [Serializable]
    internal sealed class AttackComboBalanceData
    {
        public float damage;
        public float duration;
        public float comboInputWindow;
        public float cancelStartTime;
    }

    [Serializable]
    internal sealed class PlayerBalanceData
    {
        public HealthBalanceData health = new HealthBalanceData();
        public float moveSpeed;
        public float rotationSpeed;
        public float dashDuration;
        public float dashSpeedMultiplier;
        public float dashCooldown;
        public float jumpForce;
        public float airControl;
        public float stunDuration;
        public float postStunInvulDuration;
        public float pushbackDuration;
        public float projectileCountTimer;
        public AttackComboBalanceData[] attackCombos = Array.Empty<AttackComboBalanceData>();
    }

    [Serializable]
    internal sealed class BossBasicAttackBalanceData
    {
        public float readyDuration;
        public Vector2 readyNormalizedWindow;
    }

    [Serializable]
    internal sealed class BossLungeAttackBalanceData
    {
        public float damageMultiplier;
        public Vector2 damageCastNormalizedWindow;
    }

    [Serializable]
    internal sealed class BossProjectileAttackBalanceData
    {
        public float warningDuration;
        public int damage;
        public float speed;
        public float lifetime;
        public int volleyCount;
        public float volleyInterval;
        public float homingStrength;
        public float homingDuration;
        public float verticalFollowSpeed;
        public float postFireRecoveryDuration;
        public float exitNormalizedTime;
    }

    [Serializable]
    internal sealed class BossAoEAttackBalanceData
    {
        public float takeOffDuration;
        public float flyForwardDuration;
        public float flyForwardSpeed;
        public float castRange;
        public float landDuration;
        public float spawnInterval;
        public int damage;
        public float warningDuration;
        public float activeDuration;
        public float tickInterval;
        public int circleCount;
        public int maxCircleInstances;
        public float radius;
        public float spawnSpreadRadius;
        public float headingLeadTime;
        public float maxHeadingLeadDistance;
        public float forwardSpreadRadius;
        public float sideSpreadRadius;
        public float headingBias;
        public float headingMinSpeed;
        public float groundRayHeight;
        public float groundRayDistance;
        public float groundOffset;
        public float fallbackProjectileHeight;
    }

    [Serializable]
    internal sealed class BossBalanceData
    {
        public HealthBalanceData health = new HealthBalanceData();
        public float moveSpeed;
        public float searchingMoveSpeed;
        public float rotationSpeed;
        public float phaseTwoHealthThreshold;
        public float aggroPriorityRange;
        public float detectionRange;
        public bool hasBasicAttackRange;
        public float basicAttackRange;
        public float lungeAttackRange;
        public float sharedRangedAttackRange;
        public float chaseReengageBuffer;
        public float searchDuration;
        public float aggroTime;
        public int attackDamage;
        public float attackDuration;
        public float attackCooldown;
        public BossBasicAttackBalanceData basicAttackSettings = new BossBasicAttackBalanceData();
        public BossLungeAttackBalanceData lungeAttackSettings = new BossLungeAttackBalanceData();
        public BossProjectileAttackBalanceData projectileAttackSettings = new BossProjectileAttackBalanceData();
        public BossAoEAttackBalanceData aoeAttackSettings = new BossAoEAttackBalanceData();
    }

    internal readonly struct PlayerBalanceTarget
    {
        public PlayerBalanceTarget(PlayerController controller, Health health)
        {
            Controller = controller;
            Health = health;
        }

        public PlayerController Controller { get; }
        public Health Health { get; }
    }

    internal readonly struct BossBalanceTarget
    {
        public BossBalanceTarget(BossController controller, Health health)
        {
            Controller = controller;
            Health = health;
        }

        public BossController Controller { get; }
        public Health Health { get; }
    }

    internal readonly struct BalanceSceneTargets
    {
        public BalanceSceneTargets(PlayerBalanceTarget player, BossBalanceTarget boss)
        {
            Player = player;
            Boss = boss;
        }

        public PlayerBalanceTarget Player { get; }
        public BossBalanceTarget Boss { get; }
    }

    internal sealed class PrefabContentsScope : IDisposable
    {
        private readonly string _prefabPath;

        public PrefabContentsScope(string prefabPath, GameObject root)
        {
            _prefabPath = prefabPath;
            Root = root;
        }

        public GameObject Root { get; }

        public void Save()
        {
            PrefabUtility.SaveAsPrefabAsset(Root, _prefabPath);
        }

        public void Dispose()
        {
            if (Root != null)
            {
                PrefabUtility.UnloadPrefabContents(Root);
            }
        }
    }

    internal sealed class BalanceSceneScope : IDisposable
    {
        private readonly bool _shouldCloseScene;

        private BalanceSceneScope(Scene scene, bool shouldCloseScene)
        {
            Scene = scene;
            _shouldCloseScene = shouldCloseScene;
        }

        public Scene Scene { get; }

        public static bool TryOpen(SceneAsset sceneAsset, out BalanceSceneScope sceneScope, out string error)
        {
            sceneScope = null;
            error = null;

            if (sceneAsset == null)
            {
                error = "Balance scene is not assigned.";
                return false;
            }

            string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(scenePath))
            {
                error = "Failed to resolve the balance scene path.";
                return false;
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = $"Failed to open scene.\n{scenePath}";
                return false;
            }

            sceneScope = new BalanceSceneScope(scene, !alreadyLoaded);
            return true;
        }

        public void Dispose()
        {
            if (_shouldCloseScene && Scene.IsValid() && Scene.isLoaded)
            {
                EditorSceneManager.CloseScene(Scene, true);
            }
        }
    }

    internal static class BalanceSerializedMapper
    {
        public static PlayerBalanceData CapturePlayer(PlayerController controller, Health health)
        {
            EnsureComponentReady(controller);

            SerializedObject playerObject = new SerializedObject(controller);
            SerializedObject healthObject = new SerializedObject(health);
            SerializedProperty attackCombosProperty = FindRequiredProperty(playerObject, "attackCombos");

            return new PlayerBalanceData
            {
                health = CaptureHealth(healthObject),
                moveSpeed = FindRequiredProperty(playerObject, "moveSpeed").floatValue,
                rotationSpeed = FindRequiredProperty(playerObject, "rotationSpeed").floatValue,
                dashDuration = FindRequiredProperty(playerObject, "dashDuration").floatValue,
                dashSpeedMultiplier = FindRequiredProperty(playerObject, "dashSpeedMultiplier").floatValue,
                dashCooldown = FindRequiredProperty(playerObject, "dashCooldown").floatValue,
                jumpForce = FindRequiredProperty(playerObject, "jumpForce").floatValue,
                airControl = FindRequiredProperty(playerObject, "airControl").floatValue,
                stunDuration = FindRequiredProperty(playerObject, "stunDuration").floatValue,
                postStunInvulDuration = FindRequiredProperty(playerObject, "postStunInvulDuration").floatValue,
                pushbackDuration = FindRequiredProperty(playerObject, "pushbackDuration").floatValue,
                projectileCountTimer = FindRequiredProperty(playerObject, "projectileCountTimer").floatValue,
                attackCombos = CaptureAttackCombos(attackCombosProperty)
            };
        }

        public static BossBalanceData CaptureBoss(BossController controller, Health health)
        {
            EnsureComponentReady(controller);

            SerializedObject bossObject = new SerializedObject(controller);
            SerializedObject healthObject = new SerializedObject(health);
            SerializedProperty basicAttackProperty = FindRequiredProperty(bossObject, "basicAttackSettings");
            SerializedProperty lungeAttackProperty = FindRequiredProperty(bossObject, "lungeAttackSettings");
            SerializedProperty projectileAttackProperty = FindRequiredProperty(bossObject, "projectileAttackSettings");
            SerializedProperty aoeAttackProperty = FindRequiredProperty(bossObject, "aoeAttackSettings");

            return new BossBalanceData
            {
                health = CaptureHealth(healthObject),
                moveSpeed = FindRequiredProperty(bossObject, "moveSpeed").floatValue,
                searchingMoveSpeed = FindRequiredProperty(bossObject, "searchingMoveSpeed").floatValue,
                rotationSpeed = FindRequiredProperty(bossObject, "rotationSpeed").floatValue,
                phaseTwoHealthThreshold = FindRequiredProperty(bossObject, "phaseTwoHealthThreshold").floatValue,
                aggroPriorityRange = FindRequiredProperty(bossObject, "aggroPriorityRange").floatValue,
                detectionRange = FindRequiredProperty(bossObject, "detectionRange").floatValue,
                hasBasicAttackRange = true,
                basicAttackRange = FindRequiredProperty(bossObject, "basicAttackRange").floatValue,
                lungeAttackRange = FindRequiredProperty(bossObject, "lungeAttackRange").floatValue,
                sharedRangedAttackRange = FindRequiredProperty(bossObject, "sharedRangedAttackRange").floatValue,
                chaseReengageBuffer = FindRequiredProperty(bossObject, "chaseReengageBuffer").floatValue,
                searchDuration = FindRequiredProperty(bossObject, "searchDuration").floatValue,
                aggroTime = FindRequiredProperty(bossObject, "aggroTime").floatValue,
                attackDamage = FindRequiredProperty(bossObject, "attackDamage").intValue,
                attackDuration = FindRequiredProperty(bossObject, "attackDuration").floatValue,
                attackCooldown = FindRequiredProperty(bossObject, "attackCooldown").floatValue,
                basicAttackSettings = new BossBasicAttackBalanceData
                {
                    readyDuration = FindRequiredRelativeProperty(basicAttackProperty, "readyDuration").floatValue,
                    readyNormalizedWindow = FindRequiredRelativeProperty(basicAttackProperty, "readyNormalizedWindow").vector2Value
                },
                lungeAttackSettings = new BossLungeAttackBalanceData
                {
                    damageMultiplier = FindRequiredRelativeProperty(lungeAttackProperty, "damageMultiplier").floatValue,
                    damageCastNormalizedWindow = FindRequiredRelativeProperty(lungeAttackProperty, "damageCastNormalizedWindow").vector2Value
                },
                projectileAttackSettings = new BossProjectileAttackBalanceData
                {
                    warningDuration = FindRequiredRelativeProperty(projectileAttackProperty, "warningDuration").floatValue,
                    damage = FindRequiredRelativeProperty(projectileAttackProperty, "damage").intValue,
                    speed = FindRequiredRelativeProperty(projectileAttackProperty, "speed").floatValue,
                    lifetime = FindRequiredRelativeProperty(projectileAttackProperty, "lifetime").floatValue,
                    volleyCount = FindRequiredRelativeProperty(projectileAttackProperty, "volleyCount").intValue,
                    volleyInterval = FindRequiredRelativeProperty(projectileAttackProperty, "volleyInterval").floatValue,
                    homingStrength = FindRequiredRelativeProperty(projectileAttackProperty, "homingStrength").floatValue,
                    homingDuration = FindRequiredRelativeProperty(projectileAttackProperty, "homingDuration").floatValue,
                    verticalFollowSpeed = FindRequiredRelativeProperty(projectileAttackProperty, "verticalFollowSpeed").floatValue,
                    postFireRecoveryDuration = FindRequiredRelativeProperty(projectileAttackProperty, "postFireRecoveryDuration").floatValue,
                    exitNormalizedTime = FindRequiredRelativeProperty(projectileAttackProperty, "exitNormalizedTime").floatValue
                },
                aoeAttackSettings = new BossAoEAttackBalanceData
                {
                    takeOffDuration = FindRequiredRelativeProperty(aoeAttackProperty, "takeOffDuration").floatValue,
                    flyForwardDuration = FindRequiredRelativeProperty(aoeAttackProperty, "flyForwardDuration").floatValue,
                    flyForwardSpeed = FindRequiredRelativeProperty(aoeAttackProperty, "flyForwardSpeed").floatValue,
                    castRange = FindRequiredRelativeProperty(aoeAttackProperty, "castRange").floatValue,
                    landDuration = FindRequiredRelativeProperty(aoeAttackProperty, "landDuration").floatValue,
                    spawnInterval = FindRequiredRelativeProperty(aoeAttackProperty, "spawnInterval").floatValue,
                    damage = FindRequiredRelativeProperty(aoeAttackProperty, "damage").intValue,
                    warningDuration = FindRequiredRelativeProperty(aoeAttackProperty, "warningDuration").floatValue,
                    activeDuration = FindRequiredRelativeProperty(aoeAttackProperty, "activeDuration").floatValue,
                    tickInterval = FindRequiredRelativeProperty(aoeAttackProperty, "tickInterval").floatValue,
                    circleCount = FindRequiredRelativeProperty(aoeAttackProperty, "circleCount").intValue,
                    maxCircleInstances = FindRequiredRelativeProperty(aoeAttackProperty, "maxCircleInstances").intValue,
                    radius = FindRequiredRelativeProperty(aoeAttackProperty, "radius").floatValue,
                    spawnSpreadRadius = FindRequiredRelativeProperty(aoeAttackProperty, "spawnSpreadRadius").floatValue,
                    headingLeadTime = FindRequiredRelativeProperty(aoeAttackProperty, "headingLeadTime").floatValue,
                    maxHeadingLeadDistance = FindRequiredRelativeProperty(aoeAttackProperty, "maxHeadingLeadDistance").floatValue,
                    forwardSpreadRadius = FindRequiredRelativeProperty(aoeAttackProperty, "forwardSpreadRadius").floatValue,
                    sideSpreadRadius = FindRequiredRelativeProperty(aoeAttackProperty, "sideSpreadRadius").floatValue,
                    headingBias = FindRequiredRelativeProperty(aoeAttackProperty, "headingBias").floatValue,
                    headingMinSpeed = FindRequiredRelativeProperty(aoeAttackProperty, "headingMinSpeed").floatValue,
                    groundRayHeight = FindRequiredRelativeProperty(aoeAttackProperty, "groundRayHeight").floatValue,
                    groundRayDistance = FindRequiredRelativeProperty(aoeAttackProperty, "groundRayDistance").floatValue,
                    groundOffset = FindRequiredRelativeProperty(aoeAttackProperty, "groundOffset").floatValue,
                    fallbackProjectileHeight = FindRequiredRelativeProperty(aoeAttackProperty, "fallbackProjectileHeight").floatValue
                }
            };
        }

        public static void ApplyPlayer(PlayerController controller, Health health, PlayerBalanceData balanceData)
        {
            if (balanceData == null)
            {
                return;
            }

            EnsureComponentReady(controller);

            SerializedObject playerObject = new SerializedObject(controller);
            SerializedObject healthObject = new SerializedObject(health);

            ApplyHealth(healthObject, balanceData.health);
            SetFloat(playerObject, "moveSpeed", SanitizeNonNegative(balanceData.moveSpeed));
            SetFloat(playerObject, "rotationSpeed", SanitizeNonNegative(balanceData.rotationSpeed));
            SetFloat(playerObject, "dashDuration", SanitizeNonNegative(balanceData.dashDuration));
            SetFloat(playerObject, "dashSpeedMultiplier", SanitizeNonNegative(balanceData.dashSpeedMultiplier));
            SetFloat(playerObject, "dashCooldown", SanitizeNonNegative(balanceData.dashCooldown));
            SetFloat(playerObject, "jumpForce", SanitizeNonNegative(balanceData.jumpForce));
            SetFloat(playerObject, "airControl", SanitizeNonNegative(balanceData.airControl));
            SetFloat(playerObject, "stunDuration", SanitizeNonNegative(balanceData.stunDuration));
            SetFloat(playerObject, "postStunInvulDuration", SanitizeNonNegative(balanceData.postStunInvulDuration));
            SetFloat(playerObject, "pushbackDuration", SanitizeNonNegative(balanceData.pushbackDuration));
            SetFloat(playerObject, "projectileCountTimer", SanitizeNonNegative(balanceData.projectileCountTimer));
            ApplyAttackCombos(FindRequiredProperty(playerObject, "attackCombos"), balanceData.attackCombos);

            playerObject.ApplyModifiedPropertiesWithoutUndo();
            healthObject.ApplyModifiedPropertiesWithoutUndo();
            EnsureComponentReady(controller);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(health);
        }

        public static void ApplyBoss(BossController controller, Health health, BossBalanceData balanceData)
        {
            if (balanceData == null)
            {
                return;
            }

            EnsureComponentReady(controller);

            SerializedObject bossObject = new SerializedObject(controller);
            SerializedObject healthObject = new SerializedObject(health);
            BossBasicAttackBalanceData basicAttackData = balanceData.basicAttackSettings ?? new BossBasicAttackBalanceData();
            BossLungeAttackBalanceData lungeAttackData = balanceData.lungeAttackSettings ?? new BossLungeAttackBalanceData();
            BossProjectileAttackBalanceData projectileAttackData = balanceData.projectileAttackSettings ?? new BossProjectileAttackBalanceData();
            BossAoEAttackBalanceData aoeAttackData = balanceData.aoeAttackSettings ?? new BossAoEAttackBalanceData();

            ApplyHealth(healthObject, balanceData.health);
            SetFloat(bossObject, "moveSpeed", SanitizeNonNegative(balanceData.moveSpeed));
            SetFloat(bossObject, "searchingMoveSpeed", SanitizeNonNegative(balanceData.searchingMoveSpeed));
            SetFloat(bossObject, "rotationSpeed", SanitizeNonNegative(balanceData.rotationSpeed));
            SetFloat(bossObject, "phaseTwoHealthThreshold", Mathf.Clamp(balanceData.phaseTwoHealthThreshold, 0.05f, 1f));
            SetFloat(bossObject, "aggroPriorityRange", SanitizeNonNegative(balanceData.aggroPriorityRange));
            SetFloat(bossObject, "detectionRange", SanitizeNonNegative(balanceData.detectionRange));
            if (balanceData.hasBasicAttackRange)
            {
                SetFloat(bossObject, "basicAttackRange", SanitizeNonNegative(balanceData.basicAttackRange));
            }
            SetFloat(bossObject, "lungeAttackRange", SanitizeNonNegative(balanceData.lungeAttackRange));
            SetFloat(bossObject, "sharedRangedAttackRange", SanitizeNonNegative(balanceData.sharedRangedAttackRange));
            SetFloat(bossObject, "chaseReengageBuffer", SanitizeNonNegative(balanceData.chaseReengageBuffer));
            SetFloat(bossObject, "searchDuration", SanitizeNonNegative(balanceData.searchDuration));
            SetFloat(bossObject, "aggroTime", SanitizeNonNegative(balanceData.aggroTime));
            SetInt(bossObject, "attackDamage", Mathf.Max(0, balanceData.attackDamage));
            SetFloat(bossObject, "attackDuration", SanitizeNonNegative(balanceData.attackDuration));
            SetFloat(bossObject, "attackCooldown", SanitizeNonNegative(balanceData.attackCooldown));

            SerializedProperty basicAttackProperty = FindRequiredProperty(bossObject, "basicAttackSettings");
            SetFloat(basicAttackProperty, "readyDuration", SanitizeNonNegative(basicAttackData.readyDuration));
            SetVector2(basicAttackProperty, "readyNormalizedWindow", ClampMinMaxWindow(basicAttackData.readyNormalizedWindow));

            SerializedProperty lungeAttackProperty = FindRequiredProperty(bossObject, "lungeAttackSettings");
            SetFloat(lungeAttackProperty, "damageMultiplier", SanitizeNonNegative(lungeAttackData.damageMultiplier));
            SetVector2(lungeAttackProperty, "damageCastNormalizedWindow", ClampMinMaxWindow(lungeAttackData.damageCastNormalizedWindow));

            SerializedProperty projectileAttackProperty = FindRequiredProperty(bossObject, "projectileAttackSettings");
            SetFloat(projectileAttackProperty, "warningDuration", SanitizeNonNegative(projectileAttackData.warningDuration));
            SetInt(projectileAttackProperty, "damage", Mathf.Max(0, projectileAttackData.damage));
            SetFloat(projectileAttackProperty, "speed", SanitizeNonNegative(projectileAttackData.speed));
            SetFloat(projectileAttackProperty, "lifetime", SanitizeNonNegative(projectileAttackData.lifetime));
            SetInt(projectileAttackProperty, "volleyCount", Mathf.Max(1, projectileAttackData.volleyCount));
            SetFloat(projectileAttackProperty, "volleyInterval", SanitizeNonNegative(projectileAttackData.volleyInterval));
            SetFloat(projectileAttackProperty, "homingStrength", Mathf.Clamp01(projectileAttackData.homingStrength));
            SetFloat(projectileAttackProperty, "homingDuration", SanitizeNonNegative(projectileAttackData.homingDuration));
            SetFloat(projectileAttackProperty, "verticalFollowSpeed", SanitizeNonNegative(projectileAttackData.verticalFollowSpeed));
            SetFloat(projectileAttackProperty, "postFireRecoveryDuration", SanitizeNonNegative(projectileAttackData.postFireRecoveryDuration));
            SetFloat(projectileAttackProperty, "exitNormalizedTime", Mathf.Clamp(projectileAttackData.exitNormalizedTime, 0.5f, 1.2f));

            SerializedProperty aoeAttackProperty = FindRequiredProperty(bossObject, "aoeAttackSettings");
            SetFloat(aoeAttackProperty, "takeOffDuration", SanitizeNonNegative(aoeAttackData.takeOffDuration));
            SetFloat(aoeAttackProperty, "flyForwardDuration", SanitizeNonNegative(aoeAttackData.flyForwardDuration));
            SetFloat(aoeAttackProperty, "flyForwardSpeed", SanitizeNonNegative(aoeAttackData.flyForwardSpeed));
            SetFloat(aoeAttackProperty, "castRange", SanitizeNonNegative(aoeAttackData.castRange));
            SetFloat(aoeAttackProperty, "landDuration", SanitizeNonNegative(aoeAttackData.landDuration));
            SetFloat(aoeAttackProperty, "spawnInterval", SanitizeNonNegative(aoeAttackData.spawnInterval));
            SetInt(aoeAttackProperty, "damage", Mathf.Max(0, aoeAttackData.damage));
            SetFloat(aoeAttackProperty, "warningDuration", SanitizeNonNegative(aoeAttackData.warningDuration));
            SetFloat(aoeAttackProperty, "activeDuration", SanitizeNonNegative(aoeAttackData.activeDuration));
            SetFloat(aoeAttackProperty, "tickInterval", SanitizeNonNegative(aoeAttackData.tickInterval));
            SetInt(aoeAttackProperty, "circleCount", Mathf.Max(0, aoeAttackData.circleCount));
            SetInt(aoeAttackProperty, "maxCircleInstances", Mathf.Max(0, aoeAttackData.maxCircleInstances));
            SetFloat(aoeAttackProperty, "radius", SanitizeNonNegative(aoeAttackData.radius));
            SetFloat(aoeAttackProperty, "spawnSpreadRadius", SanitizeNonNegative(aoeAttackData.spawnSpreadRadius));
            SetFloat(aoeAttackProperty, "headingLeadTime", SanitizeNonNegative(aoeAttackData.headingLeadTime));
            SetFloat(aoeAttackProperty, "maxHeadingLeadDistance", SanitizeNonNegative(aoeAttackData.maxHeadingLeadDistance));
            SetFloat(aoeAttackProperty, "forwardSpreadRadius", SanitizeNonNegative(aoeAttackData.forwardSpreadRadius));
            SetFloat(aoeAttackProperty, "sideSpreadRadius", SanitizeNonNegative(aoeAttackData.sideSpreadRadius));
            SetFloat(aoeAttackProperty, "headingBias", Mathf.Clamp01(aoeAttackData.headingBias));
            SetFloat(aoeAttackProperty, "headingMinSpeed", SanitizeNonNegative(aoeAttackData.headingMinSpeed));
            SetFloat(aoeAttackProperty, "groundRayHeight", SanitizeNonNegative(aoeAttackData.groundRayHeight));
            SetFloat(aoeAttackProperty, "groundRayDistance", SanitizeNonNegative(aoeAttackData.groundRayDistance));
            SetFloat(aoeAttackProperty, "groundOffset", SanitizeNonNegative(aoeAttackData.groundOffset));
            SetFloat(aoeAttackProperty, "fallbackProjectileHeight", SanitizeNonNegative(aoeAttackData.fallbackProjectileHeight));

            bossObject.ApplyModifiedPropertiesWithoutUndo();
            healthObject.ApplyModifiedPropertiesWithoutUndo();
            EnsureComponentReady(controller);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(health);
        }

        private static HealthBalanceData CaptureHealth(SerializedObject healthObject)
        {
            return new HealthBalanceData
            {
                maxHealth = FindRequiredProperty(healthObject, "_maxHealth").intValue
            };
        }

        private static void ApplyHealth(SerializedObject healthObject, HealthBalanceData healthBalanceData)
        {
            if (healthBalanceData == null)
            {
                return;
            }

            SerializedProperty maxHealthProperty = FindRequiredProperty(healthObject, "_maxHealth");
            SerializedProperty currentHealthProperty = FindRequiredProperty(healthObject, "_currentHealth");

            int currentMaxHealth = Mathf.Max(0, maxHealthProperty.intValue);
            int currentHealth = currentHealthProperty.intValue;
            bool startsFull = currentMaxHealth > 0 && currentHealth >= currentMaxHealth;

            int nextMaxHealth = Mathf.Max(0, healthBalanceData.maxHealth);
            int nextCurrentHealth = startsFull
                ? nextMaxHealth
                : Mathf.Clamp(currentHealth, 0, nextMaxHealth);

            maxHealthProperty.intValue = nextMaxHealth;
            currentHealthProperty.intValue = nextCurrentHealth;
        }

        private static AttackComboBalanceData[] CaptureAttackCombos(SerializedProperty attackCombosProperty)
        {
            AttackComboBalanceData[] combos = new AttackComboBalanceData[attackCombosProperty.arraySize];
            for (int i = 0; i < attackCombosProperty.arraySize; i++)
            {
                SerializedProperty comboProperty = attackCombosProperty.GetArrayElementAtIndex(i);
                combos[i] = new AttackComboBalanceData
                {
                    damage = FindRequiredRelativeProperty(comboProperty, "damage").floatValue,
                    duration = FindRequiredRelativeProperty(comboProperty, "duration").floatValue,
                    comboInputWindow = FindRequiredRelativeProperty(comboProperty, "comboInputWindow").floatValue,
                    cancelStartTime = FindRequiredRelativeProperty(comboProperty, "cancelStartTime").floatValue
                };
            }

            return combos;
        }

        private static void ApplyAttackCombos(SerializedProperty attackCombosProperty, AttackComboBalanceData[] attackCombos)
        {
            AttackComboBalanceData[] safeCombos = attackCombos ?? Array.Empty<AttackComboBalanceData>();
            attackCombosProperty.arraySize = safeCombos.Length;

            for (int i = 0; i < safeCombos.Length; i++)
            {
                AttackComboBalanceData comboData = safeCombos[i] ?? new AttackComboBalanceData();
                SerializedProperty comboProperty = attackCombosProperty.GetArrayElementAtIndex(i);
                SetFloat(comboProperty, "damage", SanitizeNonNegative(comboData.damage));
                SetFloat(comboProperty, "duration", SanitizeNonNegative(comboData.duration));
                SetFloat(comboProperty, "comboInputWindow", SanitizeNonNegative(comboData.comboInputWindow));
                SetFloat(comboProperty, "cancelStartTime", SanitizeNonNegative(comboData.cancelStartTime));
            }
        }

        private static Vector2 ClampMinMaxWindow(Vector2 value)
        {
            float min = Mathf.Clamp01(value.x);
            float max = Mathf.Clamp(value.y, min, 1f);
            return new Vector2(min, max);
        }

        private static float SanitizeNonNegative(float value)
        {
            return Mathf.Max(0f, value);
        }

        private static SerializedProperty FindRequiredProperty(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Missing serialized property '{propertyName}' on {serializedObject.targetObject.GetType().Name}.");
            }

            return property;
        }

        private static SerializedProperty FindRequiredRelativeProperty(SerializedProperty parentProperty, string propertyName)
        {
            SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Missing serialized property '{propertyName}' under {parentProperty.propertyPath}.");
            }

            return property;
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            FindRequiredProperty(serializedObject, propertyName).floatValue = value;
        }

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            FindRequiredProperty(serializedObject, propertyName).intValue = value;
        }

        private static void SetFloat(SerializedProperty parentProperty, string propertyName, float value)
        {
            FindRequiredRelativeProperty(parentProperty, propertyName).floatValue = value;
        }

        private static void SetInt(SerializedProperty parentProperty, string propertyName, int value)
        {
            FindRequiredRelativeProperty(parentProperty, propertyName).intValue = value;
        }

        private static void SetVector2(SerializedProperty parentProperty, string propertyName, Vector2 value)
        {
            FindRequiredRelativeProperty(parentProperty, propertyName).vector2Value = value;
        }

        private static void EnsureComponentReady(MonoBehaviour component)
        {
            MethodInfo onValidateMethod = component.GetType().GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            onValidateMethod?.Invoke(component, null);
        }
    }
}
