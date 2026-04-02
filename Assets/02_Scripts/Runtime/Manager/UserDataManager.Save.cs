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

    private bool _isSaving;
    private bool _pendingSave;

    public static string DirPath
    {
        get
        {
            if (string.IsNullOrEmpty(_dirPathCache))
                _dirPathCache = Application.persistentDataPath;

            return _dirPathCache;
        }
    }

    public static string LocalUserDataPath
    {
        get
        {
            if (string.IsNullOrEmpty(_savePathCache))
                _savePathCache = Path.Combine(DirPath, FileName);

            return _savePathCache;
        }
    }

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Populate,
        Formatting = Formatting.Indented,
        Converters = { new BigIntegerAsStringConverter() }
    };

    public async UniTask LoadLocalDataAsync()
    {
        if (User == null)
        {
            Debug.LogError("UserData is null. Call Init() before LoadLocalDataAsync().");
            return;
        }

        if (!File.Exists(LocalUserDataPath))
        {
            Debug.Log("로컬 저장 파일이 없어 기본 데이터를 사용함");
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(LocalUserDataPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("저장 파일이 비어 있어 기본 데이터를 사용함");
                return;
            }

            UserDataDto dto = JsonConvert.DeserializeObject<UserDataDto>(json, JsonSettings);
            if (dto == null)
            {
                Debug.LogWarning("역직렬화 결과가 null 이어서 기본 데이터를 사용함");
                return;
            }

            User.ApplyDto(dto);
            Debug.Log("로컬 데이터 로드 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"로컬 데이터 로드 실패: {e}");
        }
    }

    public async UniTask SaveLocalDataAsync()
    {
        if (User == null)
        {
            Debug.LogError("UserData is null. Cannot save.");
            return;
        }

        if (_isSaving)
        {
            _pendingSave = true;
            return;
        }

        _isSaving = true;

        try
        {
            Directory.CreateDirectory(DirPath);

            do
            {
                _pendingSave = false;

                UserDataDto dto = User.ToDto();
                string json = JsonConvert.SerializeObject(dto, JsonSettings);
                await File.WriteAllTextAsync(LocalUserDataPath, json);
            }
            while (_pendingSave);

            Debug.Log("데이터 저장 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 저장 실패: {e}");
        }
        finally
        {
            _isSaving = false;
        }
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
        if (reader.TokenType == JsonToken.Null)
        {
            if (objectType == typeof(BigInteger?))
                return null;

            return BigInteger.Zero;
        }

        if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
        {
            var s = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
            return BigInteger.Parse(s, CultureInfo.InvariantCulture);
        }

        if (reader.TokenType == JsonToken.String)
        {
            var s = (string)reader.Value;

            if (string.IsNullOrWhiteSpace(s))
                return BigInteger.Zero;

            if (BigInteger.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var bi))
                return bi;

            throw new JsonSerializationException($"Invalid BigInteger string: '{s}'");
        }

        throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing BigInteger.");
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var bi = (BigInteger)value;
        writer.WriteValue(bi.ToString(CultureInfo.InvariantCulture));
    }
}