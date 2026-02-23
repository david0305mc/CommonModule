// TableCodeGenerator.cs
// ✅ 하드코딩 경로 제거: ScriptableObject(TableCodeGenConfig)로 경로/파일명 관리
// ✅ UPM 패키지 배포용: Editor 폴더(또는 Editor asmdef) 아래에 두는 걸 권장
//
// 전제: CSVSerializer.ParseCSV(string, char) / DataManager.CSVDeserialize(...) 등 기존 구현이 프로젝트에 존재
//      (필요하면 이쪽도 패키지로 같이 빼는 구조로 정리 가능)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class TableCodeGenerator
{
    private const char CSV_SEP = '|';

#if UNITY_EDITOR
    // ===== 프로젝트 루트 (Assets 상위) =====
    private static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName;

    // ===== Config 로드 =====
    private static TableCodeGenConfig _config;

    private static TableCodeGenConfig Config
    {
        get
        {
            if (_config != null) return _config;

            // 1) 프로젝트 내 TableCodeGenConfig 검색
            var guids = AssetDatabase.FindAssets("t:TableCodeGenConfig");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _config = AssetDatabase.LoadAssetAtPath<TableCodeGenConfig>(path);
                if (_config != null) return _config;
            }

            // 2) 없으면 기본 asset 자동 생성(선택)
            _config = CreateDefaultConfigAsset();
            return _config;
        }
    }

    private static TableCodeGenConfig CreateDefaultConfigAsset()
    {
        const string defaultDir = "Assets/Settings";
        const string defaultAssetPath = "Assets/Settings/TableCodeGenConfig.asset";

        if (!AssetDatabase.IsValidFolder(defaultDir))
        {
            Directory.CreateDirectory(Path.Combine(ProjectRoot, defaultDir));
            AssetDatabase.Refresh();
        }

        var existing = AssetDatabase.LoadAssetAtPath<TableCodeGenConfig>(defaultAssetPath);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<TableCodeGenConfig>();
        AssetDatabase.CreateAsset(asset, defaultAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TableCodeGenerator] 기본 TableCodeGenConfig 생성: {defaultAssetPath}");
        return asset;
    }

    // ===== 경로 유틸 =====
    private static string ToFullPathFromAssetRelative(string assetRelativePath)
    {
        // 예: "Assets/xx/yy.cs" -> "C:\Project\Assets\xx\yy.cs"
        if (string.IsNullOrWhiteSpace(assetRelativePath))
            throw new ArgumentException("경로가 비어있습니다.");

        assetRelativePath = assetRelativePath.Replace("\\", "/");

        if (!assetRelativePath.StartsWith("Assets/") && assetRelativePath != "Assets")
            throw new ArgumentException($"asset-relative 경로는 'Assets/...' 형식이어야 합니다: {assetRelativePath}");

        return Path.Combine(ProjectRoot, assetRelativePath);
    }

    private static string CsvFolderFullPath
        => ToFullPathFromAssetRelative(Config.csvFolderPath);

    // ===== 외부 호출 API =====
    public static void GenerateAll(IEnumerable<string> tableNames)
    {
        if (Config == null)
        {
            Debug.LogError("[TableCodeGenerator] TableCodeGenConfig를 찾거나 생성하지 못했습니다.");
            return;
        }

        var list = tableNames?.ToList() ?? new List<string>();
        if (list.Count == 0)
        {
            Debug.LogWarning("[TableCodeGenerator] tableNames가 비어있습니다. 생성할 테이블이 없습니다.");
            return;
        }

        GenDatatable(list);
        GenConfigTable();
        GenTableEnum();

        AssetDatabase.Refresh();
        Debug.Log("[TableCodeGenerator] GenerateAll 완료");
    }

    public static void GenDatatable(IEnumerable<string> tableNames)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("#pragma warning disable 114");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.Scripting;");
            sb.AppendLine();
            sb.AppendLine("public partial class DataManager {");

            GenTableData(sb, tableNames);

            sb.AppendLine("}");

            var outPath = ToFullPathFromAssetRelative(Config.dataTableDefPath);
            WriteCode(outPath, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError($"[TableCodeGenerator] 데이터 테이블 생성 실패: {e}");
            throw;
        }
    }

    private static void GenTableData(StringBuilder sb, IEnumerable<string> tableNames)
    {
        foreach (var tableName in tableNames)
        {
            try
            {
                var csvPath = Path.Combine(CsvFolderFullPath, $"{tableName}.csv");
                if (!File.Exists(csvPath))
                {
                    Debug.LogError($"[TableCodeGenerator] CSV 파일이 없습니다: {csvPath}");
                    continue;
                }

                var data = File.ReadAllText(csvPath);
                var rows = CSVSerializer.ParseCSV(data, CSV_SEP);

                if (rows == null || rows.Count < 2)
                {
                    Debug.LogError($"[TableCodeGenerator] CSV 형식이 올바르지 않습니다(헤더/타입 라인 필요): {tableName}");
                    continue;
                }

                string tableNameUpper = $"{char.ToUpper(tableName[0])}{tableName.Substring(1)}";
                string arrayName = $"{tableNameUpper}Array";
                string dicName = $"{tableNameUpper}Dic";

                // ✅ 테이블 클래스 Preserve
                sb.AppendLine("\t[Preserve]");
                sb.AppendLine($"\tpublic partial class {tableNameUpper} {{");

                // rows[0] = field names, rows[1] = field types
                for (int i = 0; i < rows[0].Length; i++)
                {
                    var rawType = rows[1][i];
                    var csType = NormalizeType(rawType);

                    // 필드명은 기존 코드처럼 lower로 맞추되, 필요하면 PascalCase로 바꿔도 됨
                    var fieldName = rows[0][i].ToLower();
                    sb.AppendLine($"\t\tpublic {csType} {fieldName};");
                }

                // key type (첫 컬럼)
                var keyType = NormalizeType(rows[1][0]);

                sb.AppendLine("\t}");
                sb.AppendLine($"\tpublic {tableNameUpper}[] {arrayName} {{ get; private set; }}");
                sb.AppendLine($"\tpublic Dictionary<{keyType}, {tableNameUpper}> {dicName} {{ get; private set; }}");

                // ✅ Bind 메서드 Preserve
                sb.AppendLine("\t[Preserve]");
                sb.AppendLine($"\tpublic void Bind{tableNameUpper}Data(Type type, string text) {{");
                sb.AppendLine("\t\tvar deserializedData = CSVSerializer.Deserialize(text, type);");
                sb.AppendLine($"\t\tGetType().GetProperty(nameof({arrayName}))?.SetValue(this, deserializedData, null);");
                sb.AppendLine($"\t\t{dicName} = {arrayName}?.ToDictionary(i => i.id) ?? new Dictionary<{keyType}, {tableNameUpper}>();");
                sb.AppendLine("\t}");

                // ✅ Get 메서드 Preserve
                sb.AppendLine("\t[Preserve]");
                sb.AppendLine($"\tpublic {tableNameUpper} Get{tableNameUpper}Data({keyType} _id) {{");
                sb.AppendLine($"\t\tif ({dicName} != null && {dicName}.TryGetValue(_id, out {tableNameUpper} value)) {{");
                sb.AppendLine("\t\t\treturn value;");
                sb.AppendLine("\t\t}");
                sb.AppendLine($"\t\tDebug.LogError($\"테이블에 ID가 없습니다: {{_id}}\");");
                sb.AppendLine("\t\treturn null;");
                sb.AppendLine("\t}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TableCodeGenerator] 테이블 생성 실패 {tableName}: {e}");
                throw;
            }
        }
    }

    public static void GenConfigTable()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("#pragma warning disable 114");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using UnityEngine.Scripting;");
            sb.AppendLine();
            sb.AppendLine("public class ConfigTable : Singleton<ConfigTable> {");

            GenConfigTableData(sb);

            sb.AppendLine("}");

            var outPath = ToFullPathFromAssetRelative(Config.configTableDefPath);
            WriteCode(outPath, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError($"[TableCodeGenerator] 설정 테이블 생성 실패: {e}");
            throw;
        }
    }

    private static void GenConfigTableData(StringBuilder sb)
    {
        var csvPath = Path.Combine(CsvFolderFullPath, Config.configTableName);
        if (!File.Exists(csvPath))
            throw new FileNotFoundException($"ConfigTable CSV 파일이 없습니다: {csvPath}");

        var data = File.ReadAllText(csvPath);
        var rows = CSVSerializer.ParseCSV(data, CSV_SEP);

        if (rows == null || rows.Count < 3)
            throw new Exception("ConfigTable CSV 형식이 올바르지 않습니다(최소 3라인 필요).");

        // 기존 코드 유지: i=2부터 실제 데이터(필드 선언)
        for (int i = 2; i < rows.Count; i++)
        {
            var name = rows[i][0];
            var type = rows[i][1];

            sb.AppendLine("\t[Preserve]");
            sb.AppendLine($"\tpublic {type} {name};");
        }

        sb.AppendLine();
        sb.AppendLine("\t[Preserve]");
        sb.AppendLine("\tpublic void LoadConfig(Dictionary<string, Dictionary<string, object>> rowList)");
        sb.AppendLine("\t{");
        sb.AppendLine("\t\tforeach (var rowItem in rowList)");
        sb.AppendLine("\t\t{");
        sb.AppendLine("\t\t\tvar field = typeof(ConfigTable).GetField(rowItem.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);");
        sb.AppendLine("\t\t\tif (field == null) continue;");
        sb.AppendLine("\t\t\tfield.SetValue(this, rowItem.Value[\"value\"]);");
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t}");
    }

    public static void GenTableEnum()
    {
        try
        {
            var sb = new StringBuilder();
            GenTableEnumImpl(sb);

            var outPath = ToFullPathFromAssetRelative(Config.tableEnumDefPath);
            WriteCode(outPath, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError($"[TableCodeGenerator] 열거형 테이블 생성 실패: {e}");
            throw;
        }
    }

    private static void GenTableEnumImpl(StringBuilder sb)
    {
        var csvPath = Path.Combine(CsvFolderFullPath, Config.enumTableName);
        if (!File.Exists(csvPath))
            throw new FileNotFoundException($"EnumTable CSV 파일이 없습니다: {csvPath}");

        var data = File.ReadAllText(csvPath);
        var rows = CSVSerializer.ParseCSV(data, CSV_SEP);

        if (rows == null || rows.Count < 3)
            throw new Exception("EnumTable CSV 형식이 올바르지 않습니다(최소 3라인 필요).");

        var keySet = new HashSet<string>();

        for (int i = 2; i < rows.Count; i++)
        {
            string enumType = rows[i][0].ToUpper();

            if (!keySet.Contains(enumType))
            {
                if (keySet.Count > 0)
                    sb.AppendLine("}");
                sb.AppendLine($"public enum {enumType}");
                sb.AppendLine("{");
                keySet.Add(enumType);
            }

            if (rows[i].Length > 3)
                sb.AppendLine($"\t{rows[i][1].ToUpper(),-28} = {rows[i][2],-10}, // {rows[i][3]}");
            else
                sb.AppendLine($"\t{rows[i][1].ToUpper(),-28} = {rows[i][2],-10},");
        }

        sb.AppendLine("}");
    }

    // ===== 타입 정규화 =====
    private static string NormalizeType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "string";

        raw = raw.Trim();

        // 기본 primitive
        if (raw == "int" || raw == "long" || raw == "float" || raw == "double" || raw == "string" || raw == "bool")
            return raw;

        // enum 등 사용자 타입은 관례적으로 PascalCase / UPPER 유지 등 팀 룰에 따라 조정 가능
        // 기존 코드에선 type.ToUpper()를 썼으니 그 동작을 최대한 유지
        return raw.ToUpper();
    }

    // ===== 파일 쓰기 =====
    private static void WriteCode(string filePath, string content)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Debug.Log($"[TableCodeGenerator] 파일 생성 완료: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TableCodeGenerator] 파일 생성 실패 {filePath}: {e}");
            throw;
        }
    }
#else
    // 런타임 빌드에서 에디터 제너레이터 호출 방지
    public static void GenerateAll(IEnumerable<string> tableNames) =>
        Debug.LogError("TableCodeGenerator는 Editor 전용입니다.");

    public static void GenDatatable(IEnumerable<string> tableNames) =>
        Debug.LogError("TableCodeGenerator는 Editor 전용입니다.");

    public static void GenConfigTable() =>
        Debug.LogError("TableCodeGenerator는 Editor 전용입니다.");

    public static void GenTableEnum() =>
        Debug.LogError("TableCodeGenerator는 Editor 전용입니다.");
#endif
}