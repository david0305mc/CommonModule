using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
public partial class UserDataManager : Singleton<UserDataManager>
{
    private static string _dirPathCache;
    private static string _savePathCache;
    private const string FileName = "userdata.json";

    public static string DirPath
    {
        get
        {
            if (_dirPathCache == null)
            {
                _dirPathCache = Application.persistentDataPath;
            }
            return _dirPathCache;
        }
    }

    public static string SavePath
    {
        get
        {
            if (_savePathCache == null)
            {
                _savePathCache = Path.Combine(DirPath, FileName);
            }
            return _savePathCache;
        }
    }

    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Populate,
        Formatting = Formatting.Indented,
        Converters = { new BigIntegerAsStringConverter() }
    };

    public async UniTask LoadLocalDataAsync()
    {
        
    }

    public void SaveLocalDataAsync()
    {

    }

}


public sealed class BigIntegerAsStringConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(BigInteger) || objectType == typeof(BigInteger?);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        // null 처리 (nullable 지원)
        if (reader.TokenType == JsonToken.Null)
        {
            if (objectType == typeof(BigInteger?)) return null;
            return BigInteger.Zero; // 필요시 throw로 변경
        }

        // 숫자 토큰(정수)이면 문자열로 변환 후 파싱 (Int64 범위 넘어가도 안전)
        if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
        {
            var s = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
            return BigInteger.Parse(s, CultureInfo.InvariantCulture);
        }

        // 문자열 토큰이면 그대로 파싱
        if (reader.TokenType == JsonToken.String)
        {
            var s = (string)reader.Value;
            if (string.IsNullOrWhiteSpace(s)) return BigInteger.Zero; // 필요시 null 반환
            if (BigInteger.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var bi))
                return bi;

            throw new JsonSerializationException($"Invalid BigInteger string: '{s}'");
        }

        throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing BigInteger.");
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null) { writer.WriteNull(); return; }
        var bi = (BigInteger)value;

        // 문자열로 기록하면 JS 등 타언어에서도 정밀도 손실 없음
        writer.WriteValue(bi.ToString(CultureInfo.InvariantCulture));

        // 만약 숫자 리터럴로 기록하고 싶다면 (정밀도 이슈 주의):
        // writer.WriteRawValue(bi.ToString(CultureInfo.InvariantCulture));
    }
}