using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;
using System.Numerics;
using System.Globalization;
using UnityEditor;
using System.Threading;
using System.Text;
using AppsInToss;

public partial class UserDataManager : Singleton<UserDataManager>, IDisposable
{
    // 키는 한 곳에서만 관리
    private static string SaveKey => GameDefine.TossLocalFileName;
    private static bool IsCompressed = true;
    private const string PrefixGz = "GZ1:";
    private const string PrefixJs = "JS1:";

    private bool isSaving;
    private bool isSavePending;

    public bool IsReleaseClient
    {
#if RELEASE
        get => true;
#else
        get => false;
#endif
    }


    /// <summary>즉시 저장(디바운스 미적용). 보통은 RequestSave() 사용.</summary>
    public async UniTask Save()
    {
        if (IsIntroState.Value)
            return;

        if (isSaving)
        {
            isSavePending = true;
            return;
        }

        isSaving = true;

        try
        {
            while (true)
            {
                isSavePending = false;
                UserData.Player.LastSavedTime = GameTime.Instance.GetServerTimestampMs();
                var dto = UserData.ToDto();
                await SaveAsyncLocal(dto);

                // 저장 중 추가 요청이 없으면 종료
                if (!isSavePending)
                    break;
            }
        }
        finally
        {
            isSaving = false;
        }
    }

    string GenerateTestTossUserKey()
    {
        // Guid 기반 → 앞 10자리만 사용
        return Guid.NewGuid().ToString("N").Substring(0, 10);
    }

    public async UniTask<bool> LoadTossUserKey()
    {
#if UNITY_EDITOR
        tossUserKey = PlayerPrefs.GetString("Userkey", string.Empty);
        if (string.IsNullOrEmpty(tossUserKey))
        {
            tossUserKey = GenerateTestTossUserKey();
            PlayerPrefs.SetString("Userkey", tossUserKey);
            PlayerPrefs.Save();
        }
        // tossUserKey = "5i-OD3_Oeu6RD-n3WGtgSpm54kQ";

        Debug.Log($"[EDITOR] tossUserKey = {tossUserKey}");
        return true;
#else
    var result = await AIT.GetUserKeyForGame();
    if (result.IsSuccess)
    {
        tossUserKey = result._successData.Hash;
        Debug.Log($"tossUserKey {tossUserKey}");
        return true;
    }
    return false;
#endif
    }

    public async UniTask LoadUserData()
    {
        bool resetLocal = false;

        var localDto = await LoadAsyncLocal();
        if (localDto == null)
        {
            localDto = new UserDataDto();
            resetLocal = true;
        }
        else if (localDto.Player.IsReleaseAccount != IsReleaseClient)
        {
            localDto = new UserDataDto();
            localDto.Player.IsReleaseAccount = IsReleaseClient;
            resetLocal = true;
        }

        UserData = localDto.FromDto();

        if (resetLocal)
            SaveAsyncLocal(UserData.ToDto()).Forget(e => Debug.LogError($"[SaveLocal] Failed: {e}"));

        ServerLoadResult serverResult;
        try
        {
            serverResult = await LoadAsyncFromServer();
        }
        catch (Exception e)
        {
            serverResult = ServerLoadResult.Fail(e.ToString());
        }

        if (serverResult.Status == ServerLoadStatus.SuccessWithData)
        {
            var serverDto = serverResult.Dto;
            if (serverDto == null)
            {
                Debug.LogError("[UserData] SuccessWithData but serverDto is null.");
                await PopupConfirmCancel.ShowOkAsync("Connect Failed", "서버 데이터가 올바르지 않다.");
                throw new Exception("Server dto null. Entry blocked.");
            }

            if (IsReleaseClient != serverDto.Player.IsReleaseAccount)
            {
                Debug.LogError($"[UserData] Account type mismatch. client={IsReleaseClient}, server={serverDto.Player.IsReleaseAccount}");
                await PopupConfirmCancel.ShowOkAsync("계정 타입이 일치하지 않음", "서버 데이터를 기준으로 동기화한다.");

                UserData = serverDto.FromDto();
                SaveAsyncLocal(UserData.ToDto()).Forget(e => Debug.LogError($"[SaveLocal] Failed: {e}"));
                return;
            }

            if (UserData.Stone.StoneLevel.Value < serverDto.Stone.StoneLevel)
            {
                UserData = serverDto.FromDto();
                SaveAsyncLocal(UserData.ToDto()).Forget(e => Debug.LogError($"[SaveLocal] Failed: {e}"));
            }
            else
            {
                SaveToServer(false).Forget(e => Debug.LogError($"[SaveToServer] Failed: {e}"));
            }

            return;
        }
        else if (serverResult.Status == ServerLoadStatus.SuccessNoData)
        {
            Debug.Log("[UserData] New User (no server data).");
            UserData = new UserDataDto().FromDto();
            UserData.Player.IsReleaseAccount = IsReleaseClient;

            SaveAsyncLocal(UserData.ToDto()).Forget(e => Debug.LogError($"[SaveLocal] Failed: {e}"));
            SaveToServer(false).Forget(e => Debug.LogError($"[SaveToServer] Failed: {e}"));
            return;
        }
        else
        {
            Debug.LogError($"[UserData] Server load FAILED. status={serverResult.Status}, err={serverResult.Error}");
            await PopupConfirmCancel.ShowOkAsync("Connect Failed", LocalizationManager.Instance.GetText("server_receive_failed"));
            throw new Exception("Server load failed. Entry blocked.");
        }
    }

    // ====== IO ======
    public static bool SaveLocalFileExists()
    {
        try
        {
            var path = SavePath;
            Debug.Log($"[IO] Exists? {path}");
            // WebGL은 디렉토리 먼저 보장
            if (!Directory.Exists(DirPath))
            {
                Debug.Log("SafeFileExists 0");
                Directory.CreateDirectory(DirPath);
                Debug.Log("SafeFileExists 1");
            }
            Debug.Log("SafeFileExists 2");
            return File.Exists(path);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IO] File.Exists threw: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }
    public async UniTask DeleteLocalUserData()
    {
        Dispose();
#if UNITY_EDITOR
        await DeleteLocalFile();
#else
        await DeleteLocalToss();
#endif
    }
    private static async UniTask SaveAsyncLocal(UserDataDto dto)
    {
#if UNITY_EDITOR
        await SaveAsyncLocalFile(dto, CancellationToken.None);
#else
        await SaveAsyncToToss(dto, CancellationToken.None);
#endif
    }
    private static async UniTask<UserDataDto> LoadAsyncLocal()
    {
#if UNITY_EDITOR
        return await LoadAsyncLocalFile(CancellationToken.None);
#else
        return await LoadAsyncFromToss();
#endif
    }

    private async UniTask DeleteLocalToss()
    {
        await AIT.StorageRemoveItem(SaveKey);
    }

    private async UniTask DeleteLocalFile()
    {
        await SaveAsyncLocalFile(new UserDataDto(), default);
    }

    public void DeleteServerUserData()
    {
        UserDataDto dto = new UserDataDto();
        var saveData = EncodeDto(dto);
        NetworkManager.Instance.SaveToServerAsync(tossUserKey, saveData, false, default).Forget();
    }
    public async UniTask<bool> SaveToServer(bool showRetryPopup, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var dto = UserData.ToDto();
        var saveData = EncodeDto(dto);
        var success = await NetworkManager.Instance.SaveToServerAsync(tossUserKey, saveData, showRetryPopup, ct);
        return success;
    }

    public enum ServerLoadStatus
    {
        SuccessWithData,
        SuccessNoData,
        Fail
    }

    public readonly struct ServerLoadResult
    {
        public ServerLoadStatus Status { get; }
        public UserDataDto Dto { get; }
        public string Error { get; }

        private ServerLoadResult(ServerLoadStatus status, UserDataDto dto, string error)
        {
            Status = status;
            Dto = dto;
            Error = error;
        }

        public static ServerLoadResult WithData(UserDataDto dto) => new(ServerLoadStatus.SuccessWithData, dto, null);
        public static ServerLoadResult NoData() => new(ServerLoadStatus.SuccessNoData, null, null);
        public static ServerLoadResult Fail(string error) => new(ServerLoadStatus.Fail, null, error);
    }

    public async UniTask<ServerLoadResult> LoadAsyncFromServer()
    {
        try
        {
            var res = await NetworkManager.Instance.LoadFromServerAsync(tossUserKey, default);

            // 서버 시간이 중요하면 성공 응답 받은 시점에 먼저 동기화
            GameTime.Instance.InitServerTime(res.ServerTime);

            if (string.IsNullOrEmpty(res.Result.SaveData))
                return ServerLoadResult.NoData(); // 신규 유저(또는 서버에 저장된 적 없음)

            var dto = DecodeToDTO(res.Result.SaveData);
            if (dto == null)
                return ServerLoadResult.Fail("DecodeToDTO returned null");

            Debug.Log($"LoadAsyncFromServer IsReleaseClient {IsReleaseClient} uid {tossUserKey} level {dto.Stone.StoneLevel} ");

            return ServerLoadResult.WithData(dto);
        }
        catch (Exception e)
        {
            return ServerLoadResult.Fail(e.ToString());
        }
    }



    public static string EncodeDto(UserDataDto dto)
    {
        var rawJson = JsonConvert.SerializeObject(dto, _jsonSettings);
        if (IsCompressed)
            return PrefixGz + GzipUtil.CompressToBase64(rawJson);
        return PrefixJs + rawJson;
    }

    private static string DecodeToRawJson(string stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return string.Empty;

        if (stored.StartsWith(PrefixGz))
            return GzipUtil.DecompressFromBase64(stored.Substring(PrefixGz.Length));

        if (stored.StartsWith(PrefixJs))
            return stored.Substring(PrefixJs.Length);

        if (Util.IsBase64String(stored))
        {
            Debug.Log("IsBase64String");
            var bytes = Convert.FromBase64String(stored);
            return Encoding.UTF8.GetString(bytes);
        }

        Debug.Log("Nomal Json");
        // 구버전(헤더 없음) → 그대로 반환 (이미 JSON일 가능성)
        return stored;
    }

    public static UserDataDto DecodeToDTO(string stored)
    {
        string rawJson = null;
        try
        {
            rawJson = DecodeToRawJson(stored);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserData:IO] Decode failed. key={SaveKey}, ex={ex}");
            return new UserDataDto();
        }

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            Debug.Log($"[UserData:IO] Empty json after decode. key={SaveKey}");
            return new UserDataDto();
        }

        try
        {
            return JsonConvert.DeserializeObject<UserDataDto>(rawJson, _jsonSettings) ?? new UserDataDto();
        }
        catch (JsonException)
        {
            // 구버전이 사실 base64(gzip)였던 케이스 fallback
            try
            {
                var fallbackJson = GzipUtil.DecompressFromBase64(rawJson);
                return JsonConvert.DeserializeObject<UserDataDto>(fallbackJson, _jsonSettings) ?? new UserDataDto();
            }
            catch (Exception ex2)
            {
                Debug.LogError($"[UserData:IO] Deserialize failed (json+fallback). key={SaveKey}, ex={ex2}");
                return new UserDataDto();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserData:IO] Deserialize failed. key={SaveKey}, ex={ex}");
            return new UserDataDto();
        }
    }
    private static async UniTask<UserDataDto> LoadAsyncLocalFile(CancellationToken ct)
    {
        try
        {
            if (!SaveLocalFileExists())
                return new UserDataDto();

            // ✅ 저장은 UTF-8로 했으니 로드도 UTF-8로 고정
            var stored = await File.ReadAllTextAsync(SavePath, Encoding.UTF8, ct);

            if (string.IsNullOrWhiteSpace(stored))
                return new UserDataDto();
            return DecodeToDTO(stored);
        }
        catch (OperationCanceledException)
        {
            // 취소는 에러로 보지 않고 조용히 기본값 반환(정책에 맞게)
            return new UserDataDto();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserData:IO] Load Error: {ex}");
            return new UserDataDto();
        }
    }

    private static async UniTask SaveAsyncLocalFile(UserDataDto dto, CancellationToken ct)
    {
#if UNITY_EDITOR
        if (EditorApplication.isPaused)
            return;
#endif
        try
        {
            if (!Directory.Exists(DirPath))
                Directory.CreateDirectory(DirPath);

            var stored = EncodeDto(dto);
            var bytes = Encoding.UTF8.GetBytes(stored);

            var tmpPath = Path.Combine(DirPath, $"userdata.{Guid.NewGuid():N}.tmp");

            using (var fs = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096))
            {
                await fs.WriteAsync(bytes, 0, bytes.Length, ct);

                // ✅ flushToDisk=true: OS 중간 버퍼까지 flush 시도
                fs.Flush(true);
            }

            if (File.Exists(SavePath))
            {
                try
                {
                    File.Replace(tmpPath, SavePath, null);
                }
                catch (Exception rex)
                {
                    Debug.LogWarning($"[UserData:IO] File.Replace failed. fallback Move. ex={rex}");
                    File.Delete(SavePath);
                    File.Move(tmpPath, SavePath);
                }
            }
            else
            {
                File.Move(tmpPath, SavePath);
            }
        }
        catch (OperationCanceledException)
        {
            // 취소는 정상 흐름
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserData:IO] Save Error: {ex}");
        }
    }

    public static async UniTask SaveAsyncToToss(UserDataDto dto, CancellationToken ct = default)
    {
        if (dto == null)
        {
            Debug.LogWarning($"[UserData:IO] Save skipped. dto is null. key={SaveKey}");
            return;
        }

        string stored;
        try
        {
            stored = EncodeDto(dto);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserData:IO] Serialize/Encode failed. key={SaveKey}, ex={ex}");
            return;
        }

        try
        {
            await AIT.StorageSetItem(SaveKey, stored);
            // Debug.Log($"[UserData:IO] Save OK. key={SaveKey}, bytes={stored.Length}, compressed={IsCompressed}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserData:IO] StorageSetItem failed. key={SaveKey}, ex={ex}");
        }
    }

    private static async UniTask<UserDataDto> LoadAsyncFromToss()
    {
        string stored;
        try
        {
            stored = await AIT.StorageGetItem(SaveKey);
            Debug.Log($"stored {stored}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserData:IO] StorageGetItem failed. key={SaveKey}, ex={ex}");
            return new UserDataDto();
        }

        if (string.IsNullOrWhiteSpace(stored) || stored == "null")
        {
            Debug.Log($"[UserData:IO] No save data. key={SaveKey}");
            return new UserDataDto();
        }

        try
        {
            return DecodeToDTO(stored);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserData:IO] Decode failed. key={SaveKey}, ex={ex}");
            return new UserDataDto();
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