using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class CSVSerializer
{
    // ===== 옵션 =====
    public sealed class Options
    {
        public char Separator = '|';
        public char ArraySeparator = ',';          // 배열 내부 구분자
        public bool TrimHeader = true;
        public bool TrimValue = true;
        public bool IgnoreUnknownColumns = true;   // CSV에만 있는 컬럼 무시
        public bool IgnoreMissingFields = true;    // 타입에만 있는 필드/프로퍼티 누락 허용
        public bool LogMissingFields = false;      // 누락 필드 로그 (디폴트 꺼짐)
        public bool EnumIgnoreCase = true;
        public CultureInfo Culture = CultureInfo.InvariantCulture;
        // ✅ 추가: 헤더(0번째 줄) 다음부터 스킵할 데이터 줄 수
        // 예) 1이면 "2번째 라인(rows[1])"을 건너뛰고 rows[2]부터 읽음
        public int SkipDataRows = 0;
    }

    private static readonly Options DefaultOptions = new Options();

    // ===== 타입 캐시 =====
    private sealed class MemberSetter
    {
        public string NameLower;
        public Type ValueType;
        public Action<object, object> Set;
    }

    private sealed class TypeCache
    {
        public readonly Dictionary<string, MemberSetter> Map; // lowerName -> setter
        public TypeCache(Dictionary<string, MemberSetter> map) => Map = map;
    }

    private static readonly Dictionary<Type, TypeCache> _typeCache = new Dictionary<Type, TypeCache>(256);

    private static TypeCache GetOrCreateTypeCache(Type type)
    {
        if (_typeCache.TryGetValue(type, out var cache))
            return cache;

        var map = new Dictionary<string, MemberSetter>(StringComparer.OrdinalIgnoreCase);

        // public field
        foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var nameLower = f.Name.ToLowerInvariant();
            map[nameLower] = new MemberSetter
            {
                NameLower = nameLower,
                ValueType = f.FieldType,
                Set = (obj, val) => f.SetValue(obj, val)
            };
        }

        // public property (setter가 있는 것만)
        foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!p.CanWrite) continue;
            var setMethod = p.GetSetMethod(nonPublic: false);
            if (setMethod == null) continue;

            var nameLower = p.Name.ToLowerInvariant();
            map[nameLower] = new MemberSetter
            {
                NameLower = nameLower,
                ValueType = p.PropertyType,
                Set = (obj, val) => p.SetValue(obj, val, null)
            };
        }

        cache = new TypeCache(map);
        _typeCache[type] = cache;
        return cache;
    }

    // ===== Public API =====
    public static T[] Deserialize<T>(string csvText, Options opt = null)
    {
        opt ??= DefaultOptions;
        var rows = ParseCSV(csvText, opt.Separator);
        return (T[])CreateArray(typeof(T), rows, opt);
    }

    public static T[] Deserialize<T>(List<string[]> rows, Options opt = null)
    {
        opt ??= DefaultOptions;
        return (T[])CreateArray(typeof(T), rows, opt);
    }

    public static object Deserialize(string csvText, Type elementType, Options opt = null)
    {
        opt ??= DefaultOptions;
        var rows = ParseCSV(csvText, opt.Separator);
        return CreateArray(elementType, rows, opt);
    }

    public static object Deserialize(List<string[]> rows, Type elementType, Options opt = null)
    {
        opt ??= DefaultOptions;
        return CreateArray(elementType, rows, opt);
    }

    // id-value 형태(세로 테이블) -> 객체 1개
    public static T DeserializeIdValue<T>(string csvText, int idCol = 0, int valueCol = 1, Options opt = null)
    {
        opt ??= DefaultOptions;
        var rows = ParseCSV(csvText, opt.Separator);
        return (T)CreateIdValue(typeof(T), rows, idCol, valueCol, opt);
    }

    public static T DeserializeIdValue<T>(List<string[]> rows, int idCol = 0, int valueCol = 1, Options opt = null)
    {
        opt ??= DefaultOptions;
        return (T)CreateIdValue(typeof(T), rows, idCol, valueCol, opt);
    }

    // ===== Core =====
    private static object CreateArray(Type elementType, List<string[]> rows, Options opt)
    {
        if (rows == null || rows.Count == 0)
            return Array.CreateInstance(elementType, 0);

        // 헤더 파싱
        var header = rows[0];
        var colToMember = BuildColumnMapping(elementType, header, opt);

        int startRow = 1 + Math.Max(0, opt.SkipDataRows);   // ✅ 변경
        if (startRow > rows.Count) startRow = rows.Count;

        int dataCount = Math.Max(0, rows.Count - startRow); // ✅ 변경
        var array = Array.CreateInstance(elementType, dataCount);

        int outIndex = 0;
        for (int r = startRow; r < rows.Count; r++)         // ✅ 변경
        {
            var obj = Activator.CreateInstance(elementType);
            var cols = rows[r];

            foreach (var kv in colToMember)
            {
                int colIndex = kv.Key;
                if (colIndex >= cols.Length) continue;

                var raw = cols[colIndex];
                if (opt.TrimValue && raw != null) raw = raw.Trim();
                if (string.IsNullOrEmpty(raw)) continue;

                try
                {
                    object parsed = ParseValue(raw, kv.Value.ValueType, opt);
                    if (parsed != null)
                        kv.Value.Set(obj, parsed);
                }
                catch (Exception e)
                {
                    Debug.LogError($"CSV Deserialize 실패: type={elementType.Name}, row={r}, col={colIndex}, member={kv.Value.NameLower}, value='{raw}'\n{e}");
                }
            }

            array.SetValue(obj, outIndex); // ✅ r-1 대신 outIndex 사용
            outIndex++;
        }

        return array;
    }

    private static Dictionary<int, MemberSetter> BuildColumnMapping(Type elementType, string[] header, Options opt)
    {
        var cache = GetOrCreateTypeCache(elementType);
        var map = new Dictionary<int, MemberSetter>(header.Length);

        for (int i = 0; i < header.Length; i++)
        {
            var h = header[i] ?? "";
            if (opt.TrimHeader) h = h.Trim();

            // BOM 제거 (특히 "﻿id" 같은 케이스)
            h = RemoveBom(h);

            var key = h.ToLowerInvariant();
            if (cache.Map.TryGetValue(key, out var setter))
            {
                map[i] = setter;
            }
            else
            {
                if (!opt.IgnoreUnknownColumns)
                    Debug.LogWarning($"CSV 컬럼이 대상 타입에 없음: type={elementType.Name}, column='{h}' (index={i})");
            }
        }

        if (!opt.IgnoreMissingFields && opt.LogMissingFields)
        {
            // 타입 멤버가 CSV에 빠진 것을 강하게 보고 싶을 때만 사용
            foreach (var member in cache.Map.Keys)
            {
                bool found = false;
                for (int i = 0; i < header.Length; i++)
                {
                    var h = RemoveBom(opt.TrimHeader ? (header[i] ?? "").Trim() : (header[i] ?? ""));
                    if (string.Equals(h, member, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    Debug.LogWarning($"CSV에 컬럼 누락: type={elementType.Name}, member='{member}'");
            }
        }

        return map;
    }

    private static object CreateIdValue(Type type, List<string[]> rows, int idCol, int valCol, Options opt)
    {
        var obj = Activator.CreateInstance(type);
        if (rows == null || rows.Count == 0)
            return obj;

        // key -> rowIndex
        var rowTable = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i].Length <= idCol) continue;
            var key = rows[i][idCol];
            if (opt.TrimValue && key != null) key = key.Trim();
            key = RemoveBom(key);

            if (!string.IsNullOrEmpty(key) && !rowTable.ContainsKey(key))
                rowTable.Add(key, i);
        }

        var cache = GetOrCreateTypeCache(type);
        foreach (var kv in cache.Map)
        {
            var memberKey = kv.Key; // lower
            if (!rowTable.TryGetValue(memberKey, out var rowIndex))
            {
                if (opt.LogMissingFields && !opt.IgnoreMissingFields)
                    Debug.LogWarning($"IdValue CSV에서 키 누락: type={type.Name}, member='{memberKey}'");
                continue;
            }

            var row = rows[rowIndex];
            if (row.Length <= valCol) continue;

            var raw = row[valCol];
            if (opt.TrimValue && raw != null) raw = raw.Trim();
            if (string.IsNullOrEmpty(raw)) continue;

            try
            {
                var parsed = ParseValue(raw, kv.Value.ValueType, opt);
                if (parsed != null)
                    kv.Value.Set(obj, parsed);
            }
            catch (Exception e)
            {
                Debug.LogError($"CSV IdValue Deserialize 실패: type={type.Name}, key='{memberKey}', value='{raw}'\n{e}");
            }
        }

        return obj;
    }

    // ===== Value Parsing =====
    private static object ParseValue(string raw, Type targetType, Options opt)
    {
        if (raw == null) return null;

        // Nullable<T>
        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying != null)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            targetType = underlying;
        }

        // string
        if (targetType == typeof(string))
            return raw;

        // enum
        if (targetType.IsEnum)
            return Enum.Parse(targetType, raw, opt.EnumIgnoreCase);

        // bool (0/1, true/false)
        if (targetType == typeof(bool))
        {
            if (raw == "0") return false;
            if (raw == "1") return true;
            return bool.Parse(raw);
        }

        // Guid
        if (targetType == typeof(Guid))
            return Guid.Parse(raw);

        // DateTime
        if (targetType == typeof(DateTime))
            return DateTime.Parse(raw, opt.Culture, DateTimeStyles.RoundtripKind);

        // Vector2/Vector3/Color 같은 Unity 타입도 흔히 씀
        if (targetType == typeof(Vector2))
        {
            var parts = SplitParts(raw, opt.ArraySeparator);
            return new Vector2(
                float.Parse(parts[0], opt.Culture),
                float.Parse(parts[1], opt.Culture)
            );
        }
        if (targetType == typeof(Vector3))
        {
            var parts = SplitParts(raw, opt.ArraySeparator);
            return new Vector3(
                float.Parse(parts[0], opt.Culture),
                float.Parse(parts[1], opt.Culture),
                float.Parse(parts[2], opt.Culture)
            );
        }
        if (targetType == typeof(Color))
        {
            // "r,g,b" or "r,g,b,a" (0~1)
            var parts = SplitParts(raw, opt.ArraySeparator);
            float r = float.Parse(parts[0], opt.Culture);
            float g = float.Parse(parts[1], opt.Culture);
            float b = float.Parse(parts[2], opt.Culture);
            float a = (parts.Length >= 4) ? float.Parse(parts[3], opt.Culture) : 1f;
            return new Color(r, g, b, a);
        }

        // array
        if (targetType.IsArray)
        {
            var elemType = targetType.GetElementType();
            var parts = SplitParts(raw, opt.ArraySeparator);

            var arr = Array.CreateInstance(elemType, parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (opt.TrimValue && p != null) p = p.Trim();

                object elemVal = ParseValue(p, elemType, opt);
                arr.SetValue(elemVal, i);
            }
            return arr;
        }

#if UNITY_EDITOR
        if (targetType == typeof(Sprite))
        {
            // 빈 값/잘못된 경로 방어
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(raw);
            if (sprite == null)
                Debug.LogWarning($"Sprite 로드 실패: path='{raw}'");
            return sprite;
        }
#endif

        // 숫자/기타 (Culture 고정)
        // (기존 코드의 "정수 필드에 1.0 같은 값" 처리 포함)
        if (targetType == typeof(short) || targetType == typeof(int) || targetType == typeof(long))
        {
            if (raw.IndexOf('.') >= 0)
            {
                var f = float.Parse(raw, opt.Culture);
                return Convert.ChangeType(f, targetType, opt.Culture);
            }
        }

        return Convert.ChangeType(raw, targetType, opt.Culture);
    }

    private static string[] SplitParts(string raw, char sep)
    {
        // 배열/벡터 파싱은 CSV 필드 내부의 string이라 따옴표는 이미 제거된 상태라는 가정
        // 그래도 공백 제거는 ParseValue에서 처리
        return raw.Split(sep);
    }

    private static string RemoveBom(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // UTF-8 BOM(0xFEFF) 제거
        if (s.Length > 0 && s[0] == '\uFEFF')
            return s.Substring(1);
        return s;
    }

    // ===== CSV Parser =====
    public static List<string[]> ParseCSV(string text, char separator = ',')
    {
        var lines = new List<string[]>();
        if (string.IsNullOrEmpty(text))
            return lines;

        var row = new List<string>(32);
        var token = new StringBuilder(64);

        bool inQuotes = false;

        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // "" => "
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        token.Append('"');
                        i += 2;
                        continue;
                    }

                    // end quotes
                    inQuotes = false;
                    i++;
                    continue;
                }

                // \n escape 지원(기존 유지)
                if (c == '\\' && i + 1 < text.Length && text[i + 1] == 'n')
                {
                    token.Append('\n');
                    i += 2;
                    continue;
                }

                // \" escape 지원(기존 유지)
                if (c == '\\' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    token.Append('"');
                    i += 2;
                    continue;
                }

                token.Append(c);
                i++;
                continue;
            }

            // not in quotes
            if (c == '"')
            {
                inQuotes = true;
                i++;
                continue;
            }

            if (c == separator)
            {
                row.Add(token.ToString());
                token.Length = 0;
                i++;
                continue;
            }

            if (c == '\r' || c == '\n')
            {
                // flush token
                row.Add(token.ToString());
                token.Length = 0;

                // add row (빈 줄 제외하고 싶으면 여기 조건 추가)
                // 현재는 "완전 빈 줄"은 제외
                bool allEmpty = true;
                for (int k = 0; k < row.Count; k++)
                {
                    if (!string.IsNullOrEmpty(row[k]))
                    {
                        allEmpty = false;
                        break;
                    }
                }
                if (!allEmpty)
                    lines.Add(row.ToArray());

                row.Clear();

                // \r\n 처리
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i += 2;
                else
                    i++;

                continue;
            }

            token.Append(c);
            i++;
        }

        // 마지막 토큰/row flush
        row.Add(token.ToString());
        bool allEmptyLast = true;
        for (int k = 0; k < row.Count; k++)
        {
            if (!string.IsNullOrEmpty(row[k]))
            {
                allEmptyLast = false;
                break;
            }
        }
        if (!allEmptyLast)
            lines.Add(row.ToArray());

        return lines;
    }
}