using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class CsvTableCodeGenerator
{
    // ===== 경로 설정 =====
    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    private static readonly string DATATABLE_DEF_PATH =
        Path.Combine(ProjectRoot, "Assets/02_Scripts/Manager/DataManager/DataManager.Data.cs");

    private static readonly string CONFIG_TABLE_DEF_PATH =
        Path.Combine(ProjectRoot, "Assets/02_Scripts/Manager/DataManager/ConfigTable.cs");

    private static readonly string TABLE_ENUM_DEF_PATH =
        Path.Combine(ProjectRoot, "Assets/02_Scripts/Manager/DataManager/EnumTable.cs");

    private static readonly string LOCAL_CSV_PATH =
        Path.Combine(Application.dataPath, "Resources/Data");

    private const string CONFIG_TABLE_NAME = "ConfigTable.csv";
    private const string ENUM_TABLE_NAME = "EnumTable.csv";

    // CSV 구분자(네 코드 기준 '|')
    private const char CSV_SEP = '|';

    public static void GenerateAll(IEnumerable<string> tableNames)
    {
        if (tableNames == null) throw new ArgumentNullException(nameof(tableNames));

        GenerateDatatable(tableNames);
        GenerateConfigTable();
        GenerateTableEnum();

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    // =========================
    // 1) DataManager.Data.cs 생성
    // =========================
    public static void GenerateDatatable(IEnumerable<string> tableNames)
    {
        try
        {
            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine("#pragma warning disable 114");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.Scripting;");
            sb.AppendLine();
            sb.AppendLine("public partial class DataManager");
            sb.AppendLine("{");

            GenerateTableData(sb, tableNames);

            sb.AppendLine("}");

            WriteCode(DATATABLE_DEF_PATH, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 테이블 생성 실패: {e}");
            throw;
        }
    }

    private static void GenerateTableData(StringBuilder sb, IEnumerable<string> tableNames)
    {
        foreach (var rawName in tableNames)
        {
            var tableName = (rawName ?? "").Trim();
            if (string.IsNullOrEmpty(tableName))
                continue;

            var csvPath = Path.Combine(LOCAL_CSV_PATH, $"{tableName}.csv");
            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"CSV 파일이 없음: {csvPath}");

            var data = File.ReadAllText(csvPath);
            var rows = CSVSerializer.ParseCSV(data, CSV_SEP);

            // 최소: 헤더 row(0) + 타입 row(1)
            if (rows.Count < 2)
                throw new InvalidDataException($"CSV 형식이 잘못됨(헤더/타입 행 필요): {csvPath}");

            var header = rows[0];
            var types = rows[1];

            if (header == null || header.Length == 0)
                throw new InvalidDataException($"헤더가 비어있음: {csvPath}");

            if (types == null || types.Length != header.Length)
                throw new InvalidDataException($"타입 행 길이가 헤더와 다름: {csvPath} (header={header?.Length}, type={types?.Length})");

            // 생성되는 클래스/프로퍼티 이름은 C# 식별자로 안전하게
            var tableClassName = ToPascalIdentifier(tableName);
            var arrayName = $"{tableClassName}Array";
            var dicName = $"{tableClassName}Dic";

            // Key: "첫 컬럼" 기반 (id 하드코딩 제거)
            var keyFieldRaw = header[0];
            var keyFieldName = ToCamelIdentifier(keyFieldRaw);
            var keyType = NormalizeCSharpType(types[0]);

            // ===== Row Class 생성 =====
            sb.AppendLine("\t[Preserve]");
            sb.AppendLine($"\tpublic partial class {tableClassName}");
            sb.AppendLine("\t{");

            for (int i = 0; i < header.Length; i++)
            {
                var fieldRaw = header[i];
                var fieldName = ToCamelIdentifier(fieldRaw);
                var fieldType = NormalizeCSharpType(types[i]);

                sb.AppendLine($"\t\tpublic {fieldType} {fieldName};");
            }

            sb.AppendLine("\t}");
            sb.AppendLine();

            // ===== Array / Dic =====
            sb.AppendLine($"\tpublic {tableClassName}[] {arrayName} {{ get; private set; }}");
            sb.AppendLine($"\tpublic Dictionary<{keyType}, {tableClassName}> {dicName} {{ get; private set; }}");
            sb.AppendLine();

            // ===== Bind =====
            sb.AppendLine("\t[Preserve]");
            sb.AppendLine($"\tpublic void Bind{tableClassName}Data(Type type, string text)");
            sb.AppendLine("\t{");
            sb.AppendLine($"\t\tvar arr = ({tableClassName}[])CSVDeserialize(text, type);");
            sb.AppendLine($"\t\t{arrayName} = arr;");
            sb.AppendLine($"\t\t{dicName} = Build{tableClassName}Dictionary(arr);");
            sb.AppendLine("\t}");
            sb.AppendLine();

            // ===== Dictionary builder (중복 키 방어) =====
            sb.AppendLine("\t[Preserve]");
            sb.AppendLine($"\tprivate static Dictionary<{keyType}, {tableClassName}> Build{tableClassName}Dictionary({tableClassName}[] arr)");
            sb.AppendLine("\t{");
            sb.AppendLine($"\t\tvar dic = new Dictionary<{keyType}, {tableClassName}>(arr?.Length ?? 0);");
            sb.AppendLine("\t\tif (arr == null) return dic;");
            sb.AppendLine("\t\tfor (int i = 0; i < arr.Length; i++)");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\tvar row = arr[i];");
            sb.AppendLine("\t\t\tif (row == null) continue;");
            sb.AppendLine($"\t\t\tvar key = row.{keyFieldName};");
            sb.AppendLine("\t\t\tif (dic.ContainsKey(key))");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine($"\t\t\t\tDebug.LogError($\"[{tableClassName}] 중복 키 발견: {{key}} (index={{i}})\");");
            sb.AppendLine("\t\t\t\tcontinue;");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine("\t\t\tdic.Add(key, row);");
            sb.AppendLine("\t\t}");
            sb.AppendLine("\t\treturn dic;");
            sb.AppendLine("\t}");
            sb.AppendLine();

            // ===== Getter =====
            sb.AppendLine("\t[Preserve]");
            sb.AppendLine($"\tpublic {tableClassName} Get{tableClassName}Data({keyType} key)");
            sb.AppendLine("\t{");
            sb.AppendLine($"\t\tif ({dicName} != null && {dicName}.TryGetValue(key, out var value)) return value;");
            sb.AppendLine($"\t\tDebug.LogError($\"[{tableClassName}] 테이블에 키가 없음: {{key}}\");");
            sb.AppendLine("\t\treturn null;");
            sb.AppendLine("\t}");
            sb.AppendLine();
        }
    }

    // =========================
    // 2) ConfigTable.cs 생성
    // =========================
    public static void GenerateConfigTable()
    {
        try
        {
            var csvPath = Path.Combine(LOCAL_CSV_PATH, CONFIG_TABLE_NAME);
            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"ConfigTable CSV 없음: {csvPath}");

            var sb = new StringBuilder(8 * 1024);
            sb.AppendLine("#pragma warning disable 114");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using UnityEngine.Scripting;");
            sb.AppendLine();
            sb.AppendLine("public class ConfigTable : Singleton<ConfigTable>");
            sb.AppendLine("{");

            GenerateConfigTableData(sb, csvPath);

            sb.AppendLine("}");

            WriteCode(CONFIG_TABLE_DEF_PATH, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError($"설정 테이블 생성 실패: {e}");
            throw;
        }
    }

    private static void GenerateConfigTableData(StringBuilder sb, string csvPath)
    {
        var data = File.ReadAllText(csvPath);
        var rows = CSVSerializer.ParseCSV(data, CSV_SEP);

        // 네 코드 기준: 0~1은 헤더/타입 같은 용도로 쓰고 2부터 데이터
        for (int i = 2; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null || row.Length < 2) continue;

            var rawName = row[0];
            var rawType = row[1];

            if (string.IsNullOrWhiteSpace(rawName) || string.IsNullOrWhiteSpace(rawType))
                continue;

            var name = ToCamelIdentifier(rawName);
            var type = NormalizeCSharpType(rawType);

            sb.AppendLine("\t[Preserve]");
            sb.AppendLine($"\tpublic {type} {name};");
        }

        sb.AppendLine();
        sb.AppendLine("\t[Preserve]");
        sb.AppendLine("\tpublic void LoadConfig(Dictionary<string, Dictionary<string, object>> rowList)");
        sb.AppendLine("\t{");
        sb.AppendLine("\t\tif (rowList == null) return;");
        sb.AppendLine("\t\tforeach (var rowItem in rowList)");
        sb.AppendLine("\t\t{");
        sb.AppendLine("\t\t\tvar field = typeof(ConfigTable).GetField(rowItem.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);");
        sb.AppendLine("\t\t\tif (field == null) continue;");
        sb.AppendLine("\t\t\tif (rowItem.Value == null) continue;");
        sb.AppendLine("\t\t\tif (!rowItem.Value.TryGetValue(\"value\", out var v)) continue;");
        sb.AppendLine("\t\t\tfield.SetValue(this, v);");
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t}");
    }

    // =========================
    // 3) EnumTable.cs 생성
    // =========================
    public static void GenerateTableEnum()
    {
        try
        {
            var csvPath = Path.Combine(LOCAL_CSV_PATH, ENUM_TABLE_NAME);
            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"EnumTable CSV 없음: {csvPath}");

            var sb = new StringBuilder(8 * 1024);
            GenerateTableEnum(sb, csvPath);
            WriteCode(TABLE_ENUM_DEF_PATH, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError($"열거형 테이블 생성 실패: {e}");
            throw;
        }
    }

    private static void GenerateTableEnum(StringBuilder sb, string csvPath)
    {
        var data = File.ReadAllText(csvPath);
        var rows = CSVSerializer.ParseCSV(data, CSV_SEP);

        // 예상 컬럼: [0]=enumType, [1]=name, [2]=value, [3]=desc(optional)
        string currentEnum = null;
        var usedEnumNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedMemberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 2; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null || row.Length < 3) continue;

            var enumTypeRaw = row[0];
            var nameRaw = row[1];
            var valueRaw = row[2];
            var descRaw = (row.Length >= 4) ? row[3] : null;

            if (string.IsNullOrWhiteSpace(enumTypeRaw) || string.IsNullOrWhiteSpace(nameRaw) || string.IsNullOrWhiteSpace(valueRaw))
                continue;

            var enumType = ToPascalIdentifier(enumTypeRaw);

            if (!string.Equals(currentEnum, enumType, StringComparison.Ordinal))
            {
                // enum 닫기
                if (currentEnum != null) sb.AppendLine("}");
                sb.AppendLine($"public enum {enumType}");
                sb.AppendLine("{");

                currentEnum = enumType;
                usedEnumNames.Add(enumType);
                usedMemberNames.Clear();
            }

            var member = ToPascalIdentifier(nameRaw);
            if (usedMemberNames.Contains(member))
            {
                // 중복은 suffix로 회피(원하면 여기서 throw로 바꿔도 됨)
                int suffix = 2;
                var baseName = member;
                while (usedMemberNames.Contains(member))
                {
                    member = $"{baseName}_{suffix++}";
                }
            }
            usedMemberNames.Add(member);

            if (!string.IsNullOrEmpty(descRaw))
                sb.AppendLine($"\t{member,-28} = {valueRaw,-10}, // {descRaw}");
            else
                sb.AppendLine($"\t{member,-28} = {valueRaw,-10},");
        }

        if (currentEnum != null) sb.AppendLine("}");
    }

    // =========================
    // 유틸
    // =========================
    private static void WriteCode(string filePath, string content)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // UTF8 BOM 없이(일반적으로 깃 diff 깔끔)
        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Debug.Log($"파일 생성 완료: {filePath}");
    }

    private static string NormalizeCSharpType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "string";
        var t = raw.Trim();

        // 흔한 타입들 정규화
        switch (t.ToLowerInvariant())
        {
            case "int": return "int";
            case "int32": return "int";
            case "long": return "long";
            case "int64": return "long";
            case "short": return "short";
            case "int16": return "short";
            case "float": return "float";
            case "single": return "float";
            case "double": return "double";
            case "bool":
            case "boolean": return "bool";
            case "string": return "string";
        }

        // 배열 타입 같은 경우("int[]", "float[]")는 그대로 두되 공백 제거
        t = t.Replace(" ", "");

        // 커스텀 타입/enum은 "그대로" 두는 게 안전함 (기존처럼 무조건 대문자 변환은 위험)
        // 다만 CSV에 "MyEnum"인데 실수로 "myenum"일 수 있어 PascalCase로 보정하고 싶으면 아래를 사용
        // return ToPascalIdentifier(t);

        return t;
    }

    private static string ToPascalIdentifier(string raw)
    {
        var s = SanitizeIdentifier(raw);
        if (string.IsNullOrEmpty(s)) return "_";
        return char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s.Substring(1) : "");
    }

    private static string ToCamelIdentifier(string raw)
    {
        var s = SanitizeIdentifier(raw);
        if (string.IsNullOrEmpty(s)) return "_";
        return char.ToLowerInvariant(s[0]) + (s.Length > 1 ? s.Substring(1) : "");
    }

    private static string SanitizeIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "_";

        // BOM 제거 + 트림
        var s = raw.Trim().TrimStart('\uFEFF');

        // 구분자/공백/특수문자 제거하면서 단어 경계는 대문자로
        var sb = new StringBuilder(s.Length);
        bool upperNext = false;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if (char.IsLetterOrDigit(c) || c == '_')
            {
                if (sb.Length == 0)
                {
                    // 첫 글자가 숫자면 앞에 '_' 붙임
                    if (char.IsDigit(c))
                        sb.Append('_');
                    sb.Append(c);
                }
                else
                {
                    sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                }
                upperNext = false;
            }
            else
            {
                // 구분 문자는 다음 유효 문자를 대문자로
                upperNext = true;
            }
        }

        var result = sb.ToString();
        if (string.IsNullOrEmpty(result)) return "_";

        // C# 예약어 최소 방어(필요하면 더 추가)
        if (result == "class" || result == "namespace" || result == "public" || result == "private")
            result = "_" + result;

        return result;
    }
}