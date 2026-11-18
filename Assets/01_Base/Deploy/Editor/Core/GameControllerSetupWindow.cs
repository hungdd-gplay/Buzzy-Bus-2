#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BaseProject.EditorTools
{
    /// <summary>
    /// Tools > Base Project > Setup GameController
    /// Tạo / sắp xếp lại cấu trúc con của GameController theo chuẩn Base Project:
    ///   UserProfile(+Event) / DataContain(+EventData) / SegmentController / SoundManager(Music, Sound, SoundUI)
    ///   / AnalyticController / AdsManager(+AdmobAds) / IAPManager(+IapController) / BoxController
    ///   / FXManager(+FXController, MoneyEffectController) / Features / Loading
    /// Đồng thời: chuyển clip từ MusicManager cũ sang SoundDatabase, gán toàn bộ tham chiếu vào GameController.
    /// </summary>
    public class GameControllerSetupWindow : EditorWindow
    {
        private const string PATH_FOLDER_SETTINGS = ProjectPaths.SETTINGS;
        private const string PATH_SOUND_DATABASE = PATH_FOLDER_SETTINGS + "/SoundDatabase.asset";

        private static readonly string[] _ARRAY_OLD_MUSIC_SCRIPT = { "MusicManager.cs", "MusicManagerGameBase_BirdSort.cs" };
        private static readonly string[] _ARRAY_OLD_MUSIC_TYPE = { "MusicManager", "MusicManagerGameBase_BirdSort" };

        private GameController _gameController;
        private Vector2 _scrollPosition;
        private readonly List<string> _lstLog = new List<string>();

        [MenuItem("Tools/Base Project/Setup GameController", false, 1)]
        public static void Open()
        {
            var _window = GetWindow<GameControllerSetupWindow>("Setup GameController");
            _window.minSize = new Vector2(460, 420);
            _window._FindGameController();
        }

        private void OnFocus()
        {
            _FindGameController();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Mở scene (hoặc prefab) chứa GameController rồi bấm 'Sắp xếp & tạo thiếu'.\n" +
                "Tool sẽ tạo GameObject còn thiếu, đưa provider (AdmobAds, IapController, FXController...) vào đúng nhóm, " +
                "chuyển clip nhạc cũ sang SoundDatabase và gán tham chiếu cho GameController. Có thể Undo (Ctrl+Z).",
                MessageType.Info);

            _gameController = (GameController)EditorGUILayout.ObjectField("GameController", _gameController, typeof(GameController), true);
            if (_gameController == null)
            {
                EditorGUILayout.HelpBox("Không tìm thấy GameController trong scene/prefab đang mở.", MessageType.Warning);
                if (GUILayout.Button("Tìm lại"))
                    _FindGameController();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Trạng thái", EditorStyles.boldLabel);
            _DrawStatus("1. UserProfile", _gameController.useProfile != null && _gameController.useProfile.userEventProfile != null);
            _DrawStatus("2. DataContain", _gameController.dataContain != null && _gameController.dataContain.eventDataContain != null);
            _DrawStatus("3. SegmentController", _gameController.segmentController != null);
            _DrawStatus("4. SoundManager", _gameController.soundManager != null);
            _DrawStatus("5. AnalyticController", _gameController.AnalyticsController != null);
            _DrawStatus("6. AdsManager", _gameController.adsManager != null && _gameController.adsManager.provider != null);
            _DrawStatus("7. IAPManager", _gameController.iapManager != null && _gameController.iapManager.provider != null);
            _DrawStatus("8. BoxController", _gameController.boxController != null);
            _DrawStatus("9. FXManager", _gameController.fxManager != null);
            _DrawStatus("Script Music cũ đã xóa", !_IsHasOldMusicScript());

            EditorGUILayout.Space();
            if (GUILayout.Button("Sắp xếp & tạo thiếu", GUILayout.Height(32)))
                _RunSetup();

            using (new EditorGUI.DisabledScope(!_IsHasOldMusicScript()))
            {
                if (GUILayout.Button("Xóa script Music cũ (sau khi đã Setup + Save scene)"))
                    _DeleteOldMusicScripts();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var _line in _lstLog)
                EditorGUILayout.LabelField(_line, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void _DrawStatus(string label, bool isOk)
        {
            var _color = GUI.color;
            GUI.color = isOk ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.75f, 0.5f);
            EditorGUILayout.LabelField(label, isOk ? "✓ OK" : "✗ Thiếu");
            GUI.color = _color;
        }

        private void _FindGameController()
        {
            if (_gameController != null)
                return;
            var _stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (_stage != null)
                _gameController = _stage.prefabContentsRoot.GetComponentInChildren<GameController>(true);
            if (_gameController == null)
                _gameController = UnityEngine.Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        }

        // ======================================================================
        // SETUP
        // ======================================================================

        private void _RunSetup()
        {
            _lstLog.Clear();
            var _root = _gameController.transform;
            Undo.RegisterFullObjectHierarchyUndo(_root.gameObject, "Setup GameController");

            int _index = 0;

            // 1. UserProfile (+Event)
            var _useProfile = _GetOrCreateGroup<UseProfile>(_root, "UserProfile", _index++);
            var _userEventProfile = _GetOrCreateChild<UserEventProfile>(_useProfile.transform, "Event");
            _useProfile.userEventProfile = _userEventProfile;
            EditorUtility.SetDirty(_useProfile);

            // 2. DataContain (+EventData)
            var _dataContain = _GetOrCreateGroup<DataContain>(_root, "DataContain", _index++);
            _dataContain.eventDataContain = _GetOrCreateChild<EventDataContain>(_dataContain.transform, "EventData");

            // 3. SegmentController
            var _segmentController = _GetOrCreateGroup<SegmentController>(_root, "SegmentController", _index++);

            // 4. SoundManager
            var _soundManager = _SetupSoundManager(_root, _index++, _dataContain);

            // 5. Analytic
            var _analyticsController = _GetOrCreateGroup<AnalyticsController>(_root, "AnalyticController", _index++);

            // 6. Ads
            var _adsManager = _GetOrCreateGroup<AdsManager>(_root, "AdsManager", _index++);
            _adsManager.provider = _MoveProviderInto<AdmobAds>(_root, _adsManager.transform);
            EditorUtility.SetDirty(_adsManager);

            // 7. IAP
            var _iapManager = _GetOrCreateGroup<IAPManager>(_root, "IAPManager", _index++);
            _iapManager.provider = _MoveProviderInto<IapController>(_root, _iapManager.transform);
            if (_iapManager.provider != null && _dataContain.iapDatabase == null)
                _dataContain.iapDatabase = _iapManager.provider.inappDatabase;
            EditorUtility.SetDirty(_iapManager);

            // 8. BoxController
            var _boxController = _GetOrCreateGroup<BoxController>(_root, "BoxController", _index++);

            // 9. FX
            var _fxManager = _GetOrCreateGroup<FXManager>(_root, "FXManager", _index++);
            _fxManager.fxController = _MoveProviderInto<FXController>(_root, _fxManager.transform);
            _fxManager.moneyEffectController = _MoveProviderInto<MoneyEffectController>(_root, _fxManager.transform);
            EditorUtility.SetDirty(_fxManager);

            // Features
            var _features = _GetOrCreateEmpty(_root, "Features", _index++);
            var _packController = _MoveProviderInto<PackController>(_root, _features);
            var _videoPackController = _MoveProviderInto<SaleVideoController>(_root, _features);
            var _challengeController = _MoveProviderInto<ChallengeController>(_root, _features);
            var _eventSaleController = _MoveProviderInto<EventSaleController>(_root, _features);
            var _loginController = _MoveProviderInto<LoginController>(_root, _features);
            var _skillUserController = _MoveProviderInto<SkillUserController>(_root, _features);
            var _notificationController = _MoveProviderInto<Gplay.Notifications.NotificationController>(_root, _features);
            var _eventPackController = _MoveProviderInto<EventPackController>(_root, _features);
            var _poolController = _root.GetComponentInChildren<PoolController>(true);

            // Loading (LoadingController, Canvas...) để cuối
            var _loadingController = _root.GetComponentInChildren<LoadingController>(true);
            if (_loadingController != null && _loadingController.transform != _root)
                _loadingController.transform.SetAsLastSibling();

            _dataContain.soundDatabase = _soundManager.soundDatabase;
            EditorUtility.SetDirty(_dataContain);

            // Gán tham chiếu GameController (SerializedObject để hỗ trợ Undo + prefab override)
            var _serializedGameController = new SerializedObject(_gameController);
            _SetReference(_serializedGameController, "useProfile", _useProfile);
            _SetReference(_serializedGameController, "dataContain", _dataContain);
            _SetReference(_serializedGameController, "segmentController", _segmentController);
            _SetReference(_serializedGameController, "soundManager", _soundManager);
            _SetReference(_serializedGameController, "AnalyticsController", _analyticsController);
            _SetReference(_serializedGameController, "adsManager", _adsManager);
            _SetReference(_serializedGameController, "iapManager", _iapManager);
            _SetReference(_serializedGameController, "boxController", _boxController);
            _SetReference(_serializedGameController, "fxManager", _fxManager);
            _SetReference(_serializedGameController, "packController", _packController);
            _SetReference(_serializedGameController, "videoPackController", _videoPackController);
            _SetReference(_serializedGameController, "challengeController", _challengeController);
            _SetReference(_serializedGameController, "eventSaleController", _eventSaleController);
            _SetReference(_serializedGameController, "loginController", _loginController);
            _SetReference(_serializedGameController, "skillUserController", _skillUserController);
            _SetReference(_serializedGameController, "notificationController", _notificationController);
            _SetReference(_serializedGameController, "eventPackController", _eventPackController);
            _SetReference(_serializedGameController, "poolController", _poolController);
            _serializedGameController.ApplyModifiedProperties();

            _MarkDirty();
            _Log("Hoàn tất. Nhớ Save scene/prefab (Ctrl+S).");
        }

        private SoundManager _SetupSoundManager(Transform root, int siblingIndex, DataContain dataContain)
        {
            var _soundManager = _GetOrCreateGroup<SoundManager>(root, "SoundManager", siblingIndex);

            // Music
            var _musicPlayer = _GetOrCreateChild<MusicPlayer>(_soundManager.transform, "Music");
            var _serializedMusic = new SerializedObject(_musicPlayer);
            var _propertySource = _serializedMusic.FindProperty("audioSourceMusic");
            if (_propertySource != null && _propertySource.objectReferenceValue == null)
            {
                _propertySource.objectReferenceValue = _musicPlayer.GetComponent<AudioSource>();
                _serializedMusic.ApplyModifiedPropertiesWithoutUndo();
            }

            // Sound + SoundUI
            var _soundPlayer = _GetOrCreateChild<SoundPlayer>(_soundManager.transform, "Sound");
            var _soundUIPlayer = _GetOrCreateChild<SoundPlayer>(_soundManager.transform, "SoundUI");
            if (_soundUIPlayer == _soundPlayer)
                _Log("⚠ Sound và SoundUI trùng GameObject, kiểm tra lại.");
            _EnsureAudioSources(_soundPlayer, _soundPlayer.countSourceInit);
            _soundUIPlayer.countSourceInit = Mathf.Min(_soundUIPlayer.countSourceInit, 2);
            _EnsureAudioSources(_soundUIPlayer, _soundUIPlayer.countSourceInit);

            _soundManager.musicPlayer = _musicPlayer;
            _soundManager.soundPlayer = _soundPlayer;
            _soundManager.soundUIPlayer = _soundUIPlayer;

            // Database
            if (_soundManager.soundDatabase == null)
                _soundManager.soundDatabase = dataContain.soundDatabase != null ? dataContain.soundDatabase : _LoadOrCreateSoundDatabase();

            _MigrateOldMusicManager(root, _soundManager.soundDatabase);
            EditorUtility.SetDirty(_soundManager);
            return _soundManager;
        }

        private void _EnsureAudioSources(SoundPlayer soundPlayer, int count)
        {
            int _countCurrent = soundPlayer.GetComponentsInChildren<AudioSource>(true).Length;
            for (int i = _countCurrent; i < count; i++)
            {
                var _goSource = new GameObject("Source_" + i);
                Undo.RegisterCreatedObjectUndo(_goSource, "Create AudioSource");
                _goSource.transform.SetParent(soundPlayer.transform, false);
                var _source = _goSource.AddComponent<AudioSource>();
                _source.playOnAwake = false;
            }
        }

        private SoundDatabase _LoadOrCreateSoundDatabase()
        {
            var _database = ProjectPaths.FindAsset<SoundDatabase>(PATH_SOUND_DATABASE);
            if (_database != null)
                return _database;
            ProjectPaths.EnsureFolder(PATH_FOLDER_SETTINGS);
            _database = CreateInstance<SoundDatabase>();
            AssetDatabase.CreateAsset(_database, PATH_SOUND_DATABASE);
            AssetDatabase.SaveAssets();
            _Log("Tạo SoundDatabase: " + PATH_SOUND_DATABASE);
            return _database;
        }

        /// <summary>Đọc clip từ MusicManager cũ (theo tên field) -> SoundDatabase, rồi gỡ component cũ.</summary>
        private void _MigrateOldMusicManager(Transform root, SoundDatabase database)
        {
            var _mapFieldToSoundId = new Dictionary<string, SoundId>
            {
                { "BGMusicHome", SoundId.MusicHome },
                { "BGMusicLevel", SoundId.MusicGameplay },
                { "winMusic", SoundId.MusicWin },
                { "loseMusic", SoundId.MusicLose },
                { "clickSound", SoundId.UIButtonClick },
            };

            foreach (var _behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (_behaviour == null || Array.IndexOf(_ARRAY_OLD_MUSIC_TYPE, _behaviour.GetType().Name) < 0)
                    continue;

                var _serialized = new SerializedObject(_behaviour);
                foreach (var _pair in _mapFieldToSoundId)
                {
                    var _property = _serialized.FindProperty(_pair.Key);
                    var _clip = _property != null ? _property.objectReferenceValue as AudioClip : null;
                    if (_clip != null && database.GetClip(_pair.Value) == null)
                    {
                        Undo.RecordObject(database, "Migrate sound");
                        database.SetClip(_pair.Value, _clip);
                        _Log($"Chuyển clip {_clip.name} -> {_pair.Value}");
                    }
                }
                EditorUtility.SetDirty(database);

                var _goOld = _behaviour.gameObject;
                Undo.DestroyObjectImmediate(_behaviour);
                if (_goOld != root.gameObject && _IsOnlyAudioObject(_goOld))
                {
                    _Log("Xóa GameObject nhạc cũ: " + _goOld.name);
                    Undo.DestroyObjectImmediate(_goOld);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private bool _IsOnlyAudioObject(GameObject go)
        {
            foreach (var _component in go.GetComponentsInChildren<Component>(true))
            {
                if (_component == null || _component is Transform || _component is AudioSource)
                    continue;
                return false;
            }
            return true;
        }

        // ======================================================================
        // Helper
        // ======================================================================

        /// <summary>Tìm component T trong con của GameController; chưa có thì tạo GameObject mới. Đặt tên + thứ tự.</summary>
        private T _GetOrCreateGroup<T>(Transform root, string nameGroup, int siblingIndex) where T : Component
        {
            var _component = root.GetComponentInChildren<T>(true);
            if (_component == null || _component.transform == root)
            {
                var _go = new GameObject(nameGroup);
                Undo.RegisterCreatedObjectUndo(_go, "Create " + nameGroup);
                _go.transform.SetParent(root, false);
                _component = Undo.AddComponent<T>(_go);
                _Log("Tạo mới: " + nameGroup);
            }
            else if (_component.transform.parent != root)
            {
                Undo.SetTransformParent(_component.transform, root, "Move " + nameGroup);
            }

            if (_component.gameObject.name != nameGroup)
            {
                Undo.RecordObject(_component.gameObject, "Rename");
                _component.gameObject.name = nameGroup;
            }
            _component.transform.SetSiblingIndex(Mathf.Min(siblingIndex, root.childCount - 1));
            return _component;
        }

        private Transform _GetOrCreateEmpty(Transform root, string nameGroup, int siblingIndex)
        {
            var _transform = root.Find(nameGroup);
            if (_transform == null)
            {
                var _go = new GameObject(nameGroup);
                Undo.RegisterCreatedObjectUndo(_go, "Create " + nameGroup);
                _go.transform.SetParent(root, false);
                _transform = _go.transform;
                _Log("Tạo mới: " + nameGroup);
            }
            _transform.SetSiblingIndex(Mathf.Min(siblingIndex, root.childCount - 1));
            return _transform;
        }

        private T _GetOrCreateChild<T>(Transform parent, string nameChild) where T : Component
        {
            var _transformChild = parent.Find(nameChild);
            if (_transformChild == null)
            {
                var _go = new GameObject(nameChild);
                Undo.RegisterCreatedObjectUndo(_go, "Create " + nameChild);
                _go.transform.SetParent(parent, false);
                _transformChild = _go.transform;
                _Log($"Tạo mới: {parent.name}/{nameChild}");
            }
            var _component = _transformChild.GetComponent<T>();
            if (_component == null)
                _component = Undo.AddComponent<T>(_transformChild.gameObject);
            return _component;
        }

        /// <summary>Tìm provider T bất kỳ đâu dưới GameController và đưa vào làm con của parent.</summary>
        private T _MoveProviderInto<T>(Transform root, Transform parent) where T : Component
        {
            var _component = root.GetComponentInChildren<T>(true);
            if (_component == null)
            {
                _Log($"⚠ Không tìm thấy {typeof(T).Name} (bỏ qua).");
                return null;
            }
            var _transform = _component.transform;
            if (_transform == root || _transform == parent)
                return _component;
            if (_transform.parent != parent && !_transform.IsChildOf(parent))
                Undo.SetTransformParent(_transform, parent, "Move " + typeof(T).Name);
            return _component;
        }

        private void _SetReference(SerializedObject serializedObject, string nameProperty, UnityEngine.Object value)
        {
            var _property = serializedObject.FindProperty(nameProperty);
            if (_property == null)
            {
                _Log("⚠ GameController không có field: " + nameProperty);
                return;
            }
            if (value != null)
                _property.objectReferenceValue = value;
        }

        private void _MarkDirty()
        {
            var _stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (_stage != null)
                EditorSceneManager.MarkSceneDirty(_stage.scene);
            else
                EditorSceneManager.MarkSceneDirty(_gameController.gameObject.scene);
        }

        private bool _IsHasOldMusicScript()
        {
            foreach (var _fileName in _ARRAY_OLD_MUSIC_SCRIPT)
            {
                if (!string.IsNullOrEmpty(_FindOldMusicScriptPath(_fileName)))
                    return true;
            }
            return false;
        }

        private void _DeleteOldMusicScripts()
        {
            if (!EditorUtility.DisplayDialog("Xóa script Music cũ",
                    "Xóa MusicManager.cs và MusicManagerGameBase_BirdSort.cs?\n" +
                    "Hãy chắc chắn đã chạy Setup và Save scene. Prefab nào còn gắn script cũ sẽ thành Missing Script " +
                    "(dọn bằng Tools > Base Project > Remove Missing Scripts...).", "Xóa", "Hủy"))
                return;

            foreach (var _fileName in _ARRAY_OLD_MUSIC_SCRIPT)
            {
                string _path = _FindOldMusicScriptPath(_fileName);
                if (!string.IsNullOrEmpty(_path) && AssetDatabase.DeleteAsset(_path))
                    _Log("Đã xóa: " + _path);
            }
            AssetDatabase.Refresh();
        }

        /// <summary>Tìm script Music cũ theo tên file (không phụ thuộc thư mục, vẫn đúng sau khi tái cấu trúc).</summary>
        private static string _FindOldMusicScriptPath(string fileName)
        {
            string _name = Path.GetFileNameWithoutExtension(fileName);
            foreach (var _guid in AssetDatabase.FindAssets(_name + " t:MonoScript"))
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                if (Path.GetFileName(_path) == fileName && !_path.StartsWith(ProjectPaths.TO_DELETE))
                    return _path;
            }
            return null;
        }

        private void _Log(string message)
        {
            _lstLog.Add(message);
            Debug.Log("[SetupGameController] " + message);
            Repaint();
        }
    }
}
#endif
