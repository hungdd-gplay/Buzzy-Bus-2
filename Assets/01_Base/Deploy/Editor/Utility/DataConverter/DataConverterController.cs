#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BaseProject.EditorTools.DataConverter
{
    /// <summary>
    /// Logic chuyển bảng dữ liệu -> ScriptableObject:
    ///  - Tìm các field dạng List&lt;T&gt; / T[] trên asset đích (T là class/struct [Serializable]).
    ///  - Khớp cột theo tên field (không phân biệt hoa thường, bỏ _ - khoảng trắng).
    ///  - Hỗ trợ: string, số, bool, enum (tên hoặc số), Vector2/3, Color (#hex), List/mảng giá trị đơn (ngăn cách | hoặc ;),
    ///    asset Unity (Sprite, AudioClip, GameObject, ScriptableObject...) tìm theo tên.
    ///  - Sinh code class Data + Database từ header.
    /// </summary>
    public static class DataConverterController
    {
        private static readonly char[] _ARRAY_SEPARATOR_LIST = { '|', ';' };

        // ================== Tìm field đích ==================

        public static List<FieldInfo> GetListFields(ScriptableObject target)
        {
            var _lst = new List<FieldInfo>();
            if (target == null)
                return _lst;
            foreach (var _field in _GetSerializableFields(target.GetType()))
            {
                var _elementType = GetElementType(_field.FieldType);
                if (_elementType != null && _IsRowType(_elementType))
                    _lst.Add(_field);
            }
            return _lst;
        }

        public static Type GetElementType(Type typeCollection)
        {
            if (typeCollection.IsArray)
                return typeCollection.GetElementType();
            if (typeCollection.IsGenericType && typeCollection.GetGenericTypeDefinition() == typeof(List<>))
                return typeCollection.GetGenericArguments()[0];
            return null;
        }

        /// <summary>Danh sách field của kiểu phần tử + cột tương ứng (-1 nếu không có).</summary>
        public static List<KeyValuePair<FieldInfo, int>> BuildMapping(Type typeElement, DataTableModel table)
        {
            var _lst = new List<KeyValuePair<FieldInfo, int>>();
            foreach (var _field in _GetSerializableFields(typeElement))
                _lst.Add(new KeyValuePair<FieldInfo, int>(_field, table.FindColumn(_field.Name)));
            return _lst;
        }

        // ================== Ghi dữ liệu ==================

        public static string Apply(ScriptableObject target, FieldInfo fieldList, DataTableModel table, ImportMode mode, string nameFieldKey)
        {
            var _typeElement = GetElementType(fieldList.FieldType);
            var _mapping = BuildMapping(_typeElement, table);
            var _warnings = new StringBuilder();

            Undo.RecordObject(target, "Import data");
            var _lstCurrent = new List<object>();
            if (mode != ImportMode.Replace && fieldList.GetValue(target) is IEnumerable _enumerable)
                foreach (var _item in _enumerable) _lstCurrent.Add(_item);

            FieldInfo _fieldKey = null;
            if (mode == ImportMode.UpdateByKey)
            {
                _fieldKey = _mapping.FirstOrDefault(m => m.Key.Name == nameFieldKey).Key;
                if (_fieldKey == null)
                    return "Chưa chọn cột khóa hợp lệ.";
            }

            int _countAdd = 0, _countUpdate = 0;
            for (int r = 0; r < table.CountRow; r++)
            {
                object _element = null;
                bool _isUpdate = false;
                if (_fieldKey != null)
                {
                    int _columnKey = table.FindColumn(_fieldKey.Name);
                    object _valueKey = ConvertValue(table.GetCell(r, _columnKey), _fieldKey.FieldType, _warnings, r);
                    _element = _lstCurrent.FirstOrDefault(e => e != null && Equals(_fieldKey.GetValue(e), _valueKey));
                    _isUpdate = _element != null;
                }
                if (_element == null)
                    _element = _CreateInstance(_typeElement);

                foreach (var _pair in _mapping)
                {
                    if (_pair.Value < 0)
                        continue;
                    var _value = ConvertValue(table.GetCell(r, _pair.Value), _pair.Key.FieldType, _warnings, r);
                    _pair.Key.SetValue(_element, _value);
                }

                if (_isUpdate)
                {
                    // struct: phải gán lại vào list
                    int _index = _lstCurrent.FindIndex(e => e != null && Equals(_fieldKey.GetValue(e), _fieldKey.GetValue(_element)));
                    if (_index >= 0) _lstCurrent[_index] = _element;
                    _countUpdate++;
                }
                else
                {
                    _lstCurrent.Add(_element);
                    _countAdd++;
                }
            }

            fieldList.SetValue(target, _CreateCollection(fieldList.FieldType, _typeElement, _lstCurrent));
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            string _result = $"Xong: thêm {_countAdd}, cập nhật {_countUpdate}, tổng {_lstCurrent.Count} phần tử -> {target.name}.{fieldList.Name}";
            if (_warnings.Length > 0)
                _result += "\nCảnh báo:\n" + _warnings;
            return _result;
        }

        public static object ConvertValue(string raw, Type type, StringBuilder warnings, int indexRow)
        {
            raw = raw ?? string.Empty;
            string _text = raw.Trim();
            try
            {
                if (type == typeof(string)) return raw;
                if (type == typeof(int)) return string.IsNullOrEmpty(_text) ? 0 : (int)Math.Round(double.Parse(_text, CultureInfo.InvariantCulture));
                if (type == typeof(long)) return string.IsNullOrEmpty(_text) ? 0L : (long)Math.Round(double.Parse(_text, CultureInfo.InvariantCulture));
                if (type == typeof(float)) return string.IsNullOrEmpty(_text) ? 0f : float.Parse(_text, CultureInfo.InvariantCulture);
                if (type == typeof(double)) return string.IsNullOrEmpty(_text) ? 0d : double.Parse(_text, CultureInfo.InvariantCulture);
                if (type == typeof(bool)) return _ParseBool(_text);
                if (type.IsEnum)
                {
                    if (string.IsNullOrEmpty(_text)) return Activator.CreateInstance(type);
                    if (int.TryParse(_text, out int _enumNumber)) return Enum.ToObject(type, _enumNumber);
                    return Enum.Parse(type, _text.Replace(" ", ""), true);
                }
                if (type == typeof(Vector2)) { var _v = _ParseFloats(_text, 2); return new Vector2(_v[0], _v[1]); }
                if (type == typeof(Vector3)) { var _v = _ParseFloats(_text, 3); return new Vector3(_v[0], _v[1], _v[2]); }
                if (type == typeof(Color))
                {
                    if (ColorUtility.TryParseHtmlString(_text.StartsWith("#") ? _text : "#" + _text, out var _color)) return _color;
                    return Color.white;
                }
                if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return _FindAsset(_text, type, warnings, indexRow);

                var _elementType = GetElementType(type);
                if (_elementType != null)
                {
                    var _lstValue = new List<object>();
                    if (!string.IsNullOrEmpty(_text))
                        foreach (var _part in _text.Split(_ARRAY_SEPARATOR_LIST))
                            _lstValue.Add(ConvertValue(_part, _elementType, warnings, indexRow));
                    return _CreateCollection(type, _elementType, _lstValue);
                }
            }
            catch (Exception _exception)
            {
                warnings.AppendLine($"  Dòng {indexRow + 2}: '{raw}' -> {type.Name} lỗi ({_exception.Message})");
            }
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        // ================== Sinh code ==================

        /// <summary>Sinh 2 file: {name}Data.cs (1 dòng) và {name}Database.cs (ScriptableObject chứa list).</summary>
        public static string GenerateClassFiles(DataTableModel table, string nameClass, string folderAsset)
        {
            nameClass = _ToIdentifier(nameClass);
            if (string.IsNullOrEmpty(nameClass))
                return "Tên class không hợp lệ.";

            var _builderData = new StringBuilder();
            _builderData.AppendLine("using System;");
            _builderData.AppendLine("using UnityEngine;");
            _builderData.AppendLine();
            _builderData.AppendLine($"/// <summary>1 dòng dữ liệu (sinh từ {table.nameSource} bằng Data Converter).</summary>");
            _builderData.AppendLine("[Serializable]");
            _builderData.AppendLine($"public class {nameClass}Data");
            _builderData.AppendLine("{");
            for (int c = 0; c < table.CountColumn; c++)
            {
                string _nameField = _ToFieldName(table.lstHeader[c]);
                if (string.IsNullOrEmpty(_nameField))
                    continue;
                _builderData.AppendLine($"    public {_InferType(table, c)} {_nameField};");
            }
            _builderData.AppendLine("}");

            var _builderDatabase = new StringBuilder();
            _builderDatabase.AppendLine("using System.Collections.Generic;");
            _builderDatabase.AppendLine("using UnityEngine;");
            _builderDatabase.AppendLine();
            _builderDatabase.AppendLine($"[CreateAssetMenu(fileName = \"{nameClass}Database\", menuName = \"Base Project/Data/{nameClass} Database\")]");
            _builderDatabase.AppendLine($"public class {nameClass}Database : ScriptableObject");
            _builderDatabase.AppendLine("{");
            _builderDatabase.AppendLine($"    public List<{nameClass}Data> lstData = new List<{nameClass}Data>();");
            _builderDatabase.AppendLine("}");

            Directory.CreateDirectory(folderAsset);
            string _pathData = Path.Combine(folderAsset, nameClass + "Data.cs");
            string _pathDatabase = Path.Combine(folderAsset, nameClass + "Database.cs");
            File.WriteAllText(_pathData, _builderData.ToString(), new UTF8Encoding(true));
            File.WriteAllText(_pathDatabase, _builderDatabase.ToString(), new UTF8Encoding(true));
            AssetDatabase.Refresh();
            return $"Đã sinh {_pathData} và {_pathDatabase}. Chờ Unity compile, rồi bấm 'Tạo asset {nameClass}Database'.";
        }

        public static Type FindType(string nameType)
        {
            foreach (var _assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var _type = _assembly.GetType(nameType);
                if (_type != null)
                    return _type;
            }
            return null;
        }

        // ================== Private ==================

        private static IEnumerable<FieldInfo> _GetSerializableFields(Type type)
        {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var _lstType = new List<Type>();
            for (var _type = type; _type != null && _type != typeof(object) && _type != typeof(ScriptableObject); _type = _type.BaseType)
                _lstType.Insert(0, _type);
            foreach (var _type in _lstType)
            {
                foreach (var _field in _type.GetFields(FLAGS | BindingFlags.DeclaredOnly))
                {
                    if (_field.IsStatic || _field.IsInitOnly || _field.IsDefined(typeof(NonSerializedAttribute), false))
                        continue;
                    if (_field.IsPublic || _field.IsDefined(typeof(SerializeField), false))
                        yield return _field;
                }
            }
        }

        private static bool _IsRowType(Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum || typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;
            return type.IsDefined(typeof(SerializableAttribute), false);
        }

        private static object _CreateInstance(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : Activator.CreateInstance(type, true);
        }

        private static object _CreateCollection(Type typeCollection, Type typeElement, List<object> lstValue)
        {
            if (typeCollection.IsArray)
            {
                var _array = Array.CreateInstance(typeElement, lstValue.Count);
                for (int i = 0; i < lstValue.Count; i++) _array.SetValue(lstValue[i], i);
                return _array;
            }
            var _list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(typeElement));
            foreach (var _value in lstValue) _list.Add(_value);
            return _list;
        }

        private static bool _ParseBool(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            text = text.ToLowerInvariant();
            return text == "1" || text == "true" || text == "yes" || text == "x" || text == "y" || text == "có";
        }

        private static float[] _ParseFloats(string text, int count)
        {
            var _result = new float[count];
            var _parts = text.Trim('(', ')').Split(new[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < count && i < _parts.Length; i++)
                _result[i] = float.Parse(_parts[i], CultureInfo.InvariantCulture);
            return _result;
        }

        private static UnityEngine.Object _FindAsset(string nameAsset, Type type, StringBuilder warnings, int indexRow)
        {
            if (string.IsNullOrEmpty(nameAsset))
                return null;
            if (nameAsset.StartsWith("Assets/"))
                return AssetDatabase.LoadAssetAtPath(nameAsset, type);

            string _filter = type == typeof(Sprite) ? $"{nameAsset} t:Sprite" : $"{nameAsset} t:{type.Name}";
            foreach (var _guid in AssetDatabase.FindAssets(_filter))
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                foreach (var _asset in AssetDatabase.LoadAllAssetsAtPath(_path))
                {
                    if (_asset != null && type.IsInstanceOfType(_asset) && _asset.name == nameAsset)
                        return _asset;
                }
            }
            warnings.AppendLine($"  Dòng {indexRow + 2}: không tìm thấy {type.Name} '{nameAsset}'");
            return null;
        }

        private static string _InferType(DataTableModel table, int column)
        {
            bool _isAllInt = true, _isAllNumber = true, _isAllBool = true, _isHasValue = false;
            for (int r = 0; r < table.CountRow; r++)
            {
                string _text = table.GetCell(r, column).Trim();
                if (string.IsNullOrEmpty(_text))
                    continue;
                _isHasValue = true;
                string _lower = _text.ToLowerInvariant();
                if (_lower != "true" && _lower != "false") _isAllBool = false;
                if (!long.TryParse(_text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) _isAllInt = false;
                if (!double.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) _isAllNumber = false;
            }
            if (!_isHasValue) return "string";
            if (_isAllBool) return "bool";
            if (_isAllInt) return "int";
            if (_isAllNumber) return "float";
            return "string";
        }

        private static string _ToFieldName(string header)
        {
            string _identifier = _ToIdentifier(header);
            if (string.IsNullOrEmpty(_identifier))
                return _identifier;
            return char.ToLowerInvariant(_identifier[0]) + _identifier.Substring(1);
        }

        /// <summary>"item id" / "item_id" -> "ItemId"; bỏ dấu tiếng Việt và ký tự lạ.</summary>
        private static string _ToIdentifier(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            string _normalized = text.Normalize(NormalizationForm.FormD);
            var _builder = new StringBuilder();
            bool _isUpperNext = true;
            foreach (char c in _normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;
                char _char = c == 'đ' ? 'd' : c == 'Đ' ? 'D' : c;
                if (char.IsLetterOrDigit(_char) && _char < 128)
                {
                    _builder.Append(_isUpperNext ? char.ToUpperInvariant(_char) : _char);
                    _isUpperNext = false;
                }
                else
                {
                    _isUpperNext = true;
                }
            }
            if (_builder.Length > 0 && char.IsDigit(_builder[0]))
                _builder.Insert(0, '_');
            return _builder.ToString();
        }
    }
}
#endif
