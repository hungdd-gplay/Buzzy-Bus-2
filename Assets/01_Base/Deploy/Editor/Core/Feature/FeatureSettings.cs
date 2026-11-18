using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BaseProject.Core.Feature.EditorTools
{
    [Serializable]
    public class FeatureToggle
    {
        public FeatureId id;
        public bool isEnabled = true;
    }

    /// <summary>
    /// File setting bật/tắt tính năng (chỉ dùng trong Editor). Tick rồi bấm "Áp dụng" để ghi Scripting Define Symbols
    /// cho Android, iOS, Standalone. Đường dẫn mặc định: Assets/01_Base/Res/Data/Settings/FeatureSettings.asset (tìm theo kiểu nếu đã bị di chuyển)
    /// </summary>
    public class FeatureSettings : ScriptableObject
    {
        public const string PATH_ASSET = ProjectPaths.SETTINGS + "/FeatureSettings.asset";

        private static readonly NamedBuildTarget[] _TARGETS_APPLY =
        {
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
            NamedBuildTarget.Standalone,
        };

        public List<FeatureToggle> toggles = new List<FeatureToggle>();

        #region Load / tạo

        public static FeatureSettings LoadOrCreate(out bool isCreated)
        {
            isCreated = false;
            var _settings = ProjectPaths.FindAsset<FeatureSettings>(PATH_ASSET);
            if (_settings == null)
            {
                _settings = CreateInstance<FeatureSettings>();
                ProjectPaths.EnsureFolder(System.IO.Path.GetDirectoryName(PATH_ASSET).Replace('\\', '/'));
                AssetDatabase.CreateAsset(_settings, PATH_ASSET);
                isCreated = true;
            }
            if (_settings.SyncWithCatalog())
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }
            return _settings;
        }

        /// <summary>Thêm tính năng mới (mặc định bật), xoá tính năng không còn trong catalog. Trả về true nếu có thay đổi.</summary>
        public bool SyncWithCatalog()
        {
            bool _isChanged = false;
            foreach (var _info in FeatureCatalog.ALL_FEATURES)
            {
                if (toggles.All(t => t.id != _info.id))
                {
                    toggles.Add(new FeatureToggle { id = _info.id, isEnabled = true });
                    _isChanged = true;
                }
            }
            int _removed = toggles.RemoveAll(t => FeatureCatalog.Get(t.id) == null);
            if (_removed > 0)
                _isChanged = true;
            var _sorted = toggles.OrderBy(t => (int)t.id).ToList();
            if (!_sorted.SequenceEqual(toggles))
            {
                toggles = _sorted;
                _isChanged = true;
            }
            return _isChanged;
        }

        #endregion

        public bool IsEnabled(FeatureId id)
        {
            var _toggle = toggles.FirstOrDefault(t => t.id == id);
            return _toggle != null && _toggle.isEnabled;
        }

        public void SetEnabled(FeatureId id, bool isEnabled)
        {
            var _toggle = toggles.FirstOrDefault(t => t.id == id);
            if (_toggle != null)
                _toggle.isEnabled = isEnabled;
        }

        /// <summary>Danh sách cảnh báo phụ thuộc: tính năng đang bật nhưng tính năng nó cần đang tắt.</summary>
        public List<string> GetDependencyWarnings()
        {
            var _warnings = new List<string>();
            foreach (var _toggle in toggles.Where(t => t.isEnabled))
            {
                var _info = FeatureCatalog.Get(_toggle.id);
                foreach (var _dependency in _info.dependencies)
                {
                    if (!IsEnabled(_dependency))
                        _warnings.Add($"{_info.displayName} cần {FeatureCatalog.Get(_dependency).displayName}");
                }
            }
            return _warnings;
        }

        #region Define symbols

        /// <summary>Define tính năng đang có trên nền tảng đang chọn.</summary>
        public static HashSet<string> GetCurrentFeatureDefines()
        {
            var _target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            return new HashSet<string>(PlayerSettings.GetScriptingDefineSymbols(_target)
                .Split(';')
                .Select(s => s.Trim())
                .Where(s => s.StartsWith(FeatureCatalog.PREFIX_DEFINE)));
        }

        /// <summary>Setting có khớp với define hiện tại không (nếu không -> cần bấm Áp dụng).</summary>
        public bool IsSyncedWithDefines()
        {
            var _current = GetCurrentFeatureDefines();
            foreach (var _toggle in toggles)
            {
                string _define = FeatureCatalog.Get(_toggle.id).defineSymbol;
                if (_toggle.isEnabled != _current.Contains(_define))
                    return false;
            }
            return true;
        }

        /// <summary>Ghi define cho các nền tảng (giữ nguyên define khác không thuộc FEATURE_). Unity sẽ compile lại.</summary>
        public void ApplyDefines()
        {
            var _setEnabled = new HashSet<string>(toggles.Where(t => t.isEnabled).Select(t => FeatureCatalog.Get(t.id).defineSymbol));
            var _targets = new List<NamedBuildTarget>(_TARGETS_APPLY);
            var _targetCurrent = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (!_targets.Contains(_targetCurrent))
                _targets.Add(_targetCurrent);

            foreach (var _target in _targets)
            {
                var _defines = PlayerSettings.GetScriptingDefineSymbols(_target)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !s.StartsWith(FeatureCatalog.PREFIX_DEFINE))
                    .ToList();
                _defines.AddRange(_setEnabled.OrderBy(s => s));
                PlayerSettings.SetScriptingDefineSymbols(_target, _defines.ToArray());
            }
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log("[FeatureSettings] Đã áp dụng define: " + (_setEnabled.Count > 0 ? string.Join(", ", _setEnabled.OrderBy(s => s)) : "(không có)"));
        }

        #endregion
    }

    /// <summary>Lần đầu mở project: tạo FeatureSettings (bật tất cả) và ghi define, tránh game bị tắt hết tính năng.</summary>
    [InitializeOnLoad]
    public static class FeatureSettingsBootstrap
    {
        static FeatureSettingsBootstrap()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                var _settings = FeatureSettings.LoadOrCreate(out bool _isCreated);
                bool _isHasLegacyDefine = FeatureSettings.GetCurrentFeatureDefines().Any(d => FeatureCatalog.LEGACY_DEFINES.ContainsKey(d));
                if (_isHasLegacyDefine)
                {
                    Debug.Log("[FeatureSettings] Phát hiện define cũ (vd FEATURE_MUSIC) -> tự chuyển sang define mới.");
                    _settings.ApplyDefines();
                }
                else if (_isCreated)
                {
                    Debug.Log("[FeatureSettings] Tạo mới FeatureSettings (bật tất cả tính năng) và ghi define.");
                    _settings.ApplyDefines();
                }
                else if (!_settings.IsSyncedWithDefines())
                {
                    Debug.LogWarning("[FeatureSettings] Define hiện tại KHÁC FeatureSettings. Mở Tools > Base Project > Feature Settings và bấm Áp dụng.", _settings);
                }
            };
        }
    }
}
