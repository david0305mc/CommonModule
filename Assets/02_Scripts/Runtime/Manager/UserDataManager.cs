using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using UniRx;
using System.Numerics;
using System.Threading;
using System.Collections.Generic;
using AppsInToss;

public partial class UserDataManager : Singleton<UserDataManager>, IDisposable
{
    private static readonly string _fileName = "userdata.json";
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
    //private readonly Subject<UniRx.Unit> _saveRequest = new();
    private CompositeDisposable _disposables = new CompositeDisposable();
    private CancellationTokenSource _nautilusLoopCts;
    private IDisposable _autoSaveSubscription;

    public ReactiveProperty<bool> IsIntroState { get; set; } = new ReactiveProperty<bool>();

    public UserData UserData { get; private set; } = new();
    private Dictionary<int, int> achievementSlotMap = new();
    public IReadOnlyDictionary<int, int> AchievementSlotMap => achievementSlotMap;

    public ReadOnlyReactiveProperty<BigInteger> HeartPerStoneTouch { get; private set; }

    public ReactiveProperty<BigInteger> HeartPerSec { get; private set; }
    public ReactiveProperty<int> MoonSkillFactor;
    public ReactiveProperty<long> PlayingTimeMin;
    private string tossUserKey;
    public string TossUserKey => tossUserKey;

    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Populate,
        Formatting = Formatting.Indented,
        Converters = { new BigIntegerAsStringConverter() }
    };

    /// <summary>
    /// Non-MB 싱글톤 초기화 훅. 여기서 스트림/디바운스 준비를 한다.
    /// </summary>
    protected override void init()
    {
        HeartPerSec ??= new ReactiveProperty<BigInteger>();
        MoonSkillFactor ??= new ReactiveProperty<int>(1);
        PlayingTimeMin = new ReactiveProperty<long>(0);
    }
    public void InitilizeFromUserData()
    {
        _disposables.Clear();
        UserData.InitWithData();

        InitStoneTouchHeart();
        InitCoralHeartPerSec();
        InitDailyReset();
        UserData.AddStatistic(UNLOCKTYPE.LOGIN_COUNT_PER_DAY, 0, 1);
    }

    public void InitializeOnMainScene()
    {
        InitMoonSkill();
        InitNautilusArtifact();
        InitAchievementSlotMap();
        StartPlayTime();
    }

    private void StartPlayTime()
    {
        GameTime.Instance.InitLoginTime();
        Observable.Timer(TimeSpan.Zero, TimeSpan.FromMinutes(1))
        .ObserveOnMainThread()
        .Subscribe(_ =>
        {
            var elpseed = GameTime.Instance.GetServerDateTimeUtc() - GameTime.Instance.LoginTimeUtc;
            PlayingTimeMin.Value = (long)elpseed.TotalMinutes;
        }).AddTo(_disposables);

        PlayingTimeMin.Subscribe(playTime =>
        {
            UserData.SetPlayTimeStatistic(UNLOCKTYPE.COUNT_PLAY_TIME, 0, playTime);
            Debug.Log($"PlayingTimeMin {playTime}");
        });
    }

    public void InitDailyReset()
    {
        MessageDispatcher.AsObservable(EMessageType.DailyReset)
            .Subscribe(_ =>
            {
                ResetDaily();
            }).AddTo(_disposables);
        UserData.Stone.StartDailyMissionResetTimer().Forget();
        UserData.Stone.StartRefreshAttendanceTimer().Forget();
        UserData.Stone.StartNextAttendanceTimer().Forget();
        Save().Forget();
    }

    private void InitAchievementSlotMap()
    {
        achievementSlotMap.Clear();
        foreach (var item in UserData.Achievements.Values)
        {
            RefreshAchievementSlot(item.Table.slot);
        }
    }
    private void ResetDaily()
    {
        UserData.Stone.ShareRewardCount.Value = ConfigTable.Instance.share_event_count;
        ResetLoginCountPerDay();
        UserData.ResetDailyStatistics();
        UserData.ResetDailyMission();
        Debug.Log("-------------------DailyReset-------------------");
    }
    private void ResetLoginCountPerDay()
    {
        UserData.SetStatistic(UNLOCKTYPE.LOGIN_COUNT_PER_DAY, 0, 0);
    }
    private void RefreshAchievementSlot(int slot)
    {
        // 같은 슬롯 + 미수령 중 최저 step (없으면 null)
        int? nextStep = UserData.Achievements.Values
            .Where(a => a.Table.slot == slot && !a.IsRewardReceived.Value)
            .Select(a => (int?)a.Table.step)
            .Min();

        if (!nextStep.HasValue)
        {
            nextStep = DataManager.Instance.GetMaxAchievementStep(slot) + 1;
        }
        achievementSlotMap[slot] = nextStep.Value;
    }
    private void InitCoralHeartPerSec()
    {
        var coralStreams = UserData.Corals.Values.Select(x => x.HeartPerSec.AsUnitObservable());
        Observable.Merge(coralStreams)
        .SampleFrame(1)
        .Subscribe(_ =>
        {
            ReCalculateHeartPerSec();
        }).AddTo(_disposables);
        ReCalculateHeartPerSec();
    }

    private void ReCalculateHeartPerSec()
    {
        // 1) Coral 합산
        BigInteger coralTotal = BigInteger.Zero;
        foreach (var coral in UserData.Corals.Values)
            coralTotal += coral.HeartPerSec.Value;

        // // 2) 캐싱: 스톤 및 피쉬 수치
        // var stone = UserData.Stone;
        // long evoPercent = stone.EvolutionBonus.Value;        // 예: 5 → 5%
        // int fishCount = UserData.FishTotalCount.Value;

        // // 3) 곱셈 요소 계산 (BigInteger는 최소한만 사용)
        // BigInteger fishMultiplier = BigInteger.Pow(2, fishCount);          // 매우 커질 수 있음
        // BigInteger evoMultiplierNum = 100 + evoPercent;                    // (100 + X) / 100

        // // 4) 메인 계산
        // BigInteger result = coralTotal;
        // result *= fishMultiplier;                                          // 큰 값부터
        // result *= MoonSkillFactor.Value;                                   // 중간 계수
        // result = (result * evoMultiplierNum) / 100;                        // 마지막에 퍼센트 적용

        // // 5) 할당
        // HeartPerSec.Value = result;
        HeartPerSec.Value = coralTotal;

    }
    public BigInteger CalculateHeartPerStoneTouch(int stoneLevel)
    {
        var stoneData = UserData.Stone;
        var artifact = UserData.GetArtifact((int)ArtifactType.Clam);

        // 정수 파트
        BigInteger baseValue = stoneLevel + 9;

        BigInteger fishMultiplier = BigInteger.Pow(2, UserData.FishTotalCount.Value);
        BigInteger bonusMultiplier = BigInteger.Pow(2, stoneData.BonusLevel.Value);

        // clamMultiplier = (10 + L) / 10
        BigInteger clamNumerator = 10 + artifact.Level.Value;
        BigInteger clamDenom = 10;

        // evoMultiplier = (100 + evo) / 100  (evo가 % 정수라고 가정)
        BigInteger evoNumerator = 100 + stoneData.EvolutionBonus.Value;
        BigInteger evoDenom = 100;

        // 모두 BigInteger로 곱한 뒤, 마지막에 나눔(내림)
        BigInteger numerator =
            baseValue *
            fishMultiplier *
            bonusMultiplier *
            clamNumerator *
            evoNumerator;

        BigInteger denom = clamDenom * evoDenom;

        BigInteger baseAmount = numerator / denom; // 내림(floor)

        return baseAmount * MoonSkillFactor.Value; // MoonSkillFactor도 BigInteger라고 가정
    }

    private void InitStoneTouchHeart()
    {
        var stone = UserData.Stone;
        var clam = UserData.GetArtifact((int)ArtifactType.Clam);

        HeartPerStoneTouch =
            Observable.Merge(
                stone.StoneLevel.DistinctUntilChanged().AsUnitObservable(),
                UserData.FishTotalCount.DistinctUntilChanged().AsUnitObservable(),
                UserData.Stone.EvoFishCountByTearStone.DistinctUntilChanged().AsUnitObservable(),
                stone.EvolutionBonus.DistinctUntilChanged().AsUnitObservable(),
                stone.BonusLevel.DistinctUntilChanged().AsUnitObservable(),
                MoonSkillFactor.DistinctUntilChanged().AsUnitObservable(),
                clam.Level.DistinctUntilChanged().AsUnitObservable()
            )
            .ThrottleFrame(1)
            .Select(_ => CalculateHeartPerStoneTouch(stone.StoneLevel.Value))
            .ToReadOnlyReactiveProperty(
                CalculateHeartPerStoneTouch(stone.StoneLevel.Value) // 초기값
            )
            .AddTo(_disposables);
    }


    private void InitMoonSkill()
    {
        CameraManager.Instance
        .IsReady.CombineLatest(GameManager.Instance.IsReady, (camera, game) => camera && game)
        .Where(bothReady => bothReady)
        .First()
        .Subscribe(_ =>
        {
            InitMoonSkillInternal();
        }).AddTo(_disposables);
    }

    private void InitMoonSkillInternal()
    {
        var moonSkill = UserData.GetSkill((int)SkillType.MoonSkill);
        if (moonSkill != null)
        {
            var activeStream = moonSkill.IsSkillActive
            .StartWith(moonSkill.IsSkillActive.Value)
            .DistinctUntilChanged()
            .AsUnitObservable();

            Observable.Merge(moonSkill.Level.AsUnitObservable(), activeStream)
            .Subscribe(_ =>
            {
                var skillData = UserData.GetSkill((int)SkillType.MoonSkill);
                if (skillData.IsSkillActive.Value)
                {
                    MoonSkillFactor.Value = (int)skillData.Table.GetSkillEffect(skillData.Level.Value);
                    CameraManager.Instance.SetCameraEffectMaterial(ResourceManager.Instance.CameraEffectMoonSkillMat);

                }
                else
                {
                    MoonSkillFactor.Value = 1;
                    CameraManager.Instance.SetCameraEffectMaterial(ResourceManager.Instance.CameraEffectNormalMat);
                }
            }).AddTo(_disposables);
        }
        else
        {
            Debug.LogError("there is no moonSkill");
            MoonSkillFactor.Value = 1;
        }
    }

    private void InitNautilusArtifact()
    {
        var artifactData = UserData.GetArtifact((int)ArtifactType.Nautilus);
        artifactData.Level.DistinctUntilChanged()
        .Subscribe(level =>
        {
            StartNautilusLoop(level, artifactData.Table.GetEffect(level)).Forget();
        }).AddTo(_disposables);
    }

    private async UniTask StartNautilusLoop(int level, long autoTapPerMinute)
    {
        _nautilusLoopCts?.Cancel();
        _nautilusLoopCts?.Dispose();

        if (level > 0)
        {
            _nautilusLoopCts = new CancellationTokenSource();
            float tapInterval = 60f / autoTapPerMinute;
            while (!_nautilusLoopCts.IsCancellationRequested)
            {
                ProduceHeartOnTouch();
                await UniTask.Delay(TimeSpan.FromSeconds(tapInterval), cancellationToken: _nautilusLoopCts.Token);
            }
        }
    }
    public void InitAutoSave()
    {
        StopAutoSave();

        _autoSaveSubscription = Observable
            .Interval(TimeSpan.FromSeconds(5))
            .ObserveOnMainThread()
            .Subscribe(async _ =>
            {
                await Save();
            });
    }

    public void StopAutoSave()
    {
        _autoSaveSubscription?.Dispose();
        _autoSaveSubscription = null;
    }

    public void Dispose()
    {
        StopAutoSave();
        _disposables?.Clear();
        _disposables?.Dispose();
        _disposables = new CompositeDisposable(); // 재사용 가능하게 초기화(선택)
        UserData.Dispose();
    }

    // ====== Public API ======

    // ====== Domain Helpers (변경 + 저장 트리거) ======

    public CoralData SetCoralLevel(long coralId, int coralLevel)
    {
        if (UserData.Corals.TryGetValue(coralId, out var coralData))
        {
            coralData.SetLevel(coralLevel);
        }
        return coralData;
    }

    public void ProduceHeartAuto()
    {
        AddItem(ITEMTYPE.HEART, HeartPerSec.Value);
        // 자동 수확은 저장 안함
        // RequestSave();
    }
    public void ProduceHeartOnTouch()
    {
        AddItem(ITEMTYPE.HEART, HeartPerStoneTouch.Value);
    }

    private void ProduceFoodSkillHeart()
    {
        var foodSkill = UserData.GetSkill((int)SkillType.Food);
        long effect = foodSkill.Table.GetSkillEffect(foodSkill.Level.Value);
        AddItem(ITEMTYPE.HEART, HeartPerStoneTouch.Value * effect);
    }

    private void ResetAllSkillCooltime()
    {
        UserData.GetSkill((int)SkillType.Volcano).ResetSkillCooltime();
        UserData.GetSkill((int)SkillType.Food).ResetSkillCooltime();
        UserData.GetSkill((int)SkillType.MoonSkill).ResetSkillCooltime();
    }

    public void LevelUpCoral(long coralID, int addLevel)
    {
        if (!UserData.Corals.TryGetValue(coralID, out var coral))
        {
            Debug.LogError($"Not found Coral {coralID}");
            return;
        }
        BigInteger cost;
        switch (addLevel)
        {
            case 10:
                cost = coral.UpgradeCost_10.Value;
                break;
            case 100:
                cost = coral.UpgradeCost_100.Value;
                break;
            default:
                cost = coral.UpgradeCost_1.Value;
                break;
        }

        if (!HasEnoughItem(new ItemParam(ITEMTYPE.HEART, 0, cost)))
        {
            ToastMessage.ShowInsufficientItemMessage(ITEMTYPE.HEART);
            return;
        }
        RemoveItem(ITEMTYPE.HEART, cost);
        var coralData = SetCoralLevel(coralID, coral.CoralLevel.Value + addLevel);
        UserData.AddStatistic(UNLOCKTYPE.CORAL_LEVELUP_ALL, 0, addLevel);
        UserData.SetStatistic(UNLOCKTYPE.CORAL_LEVELUP, coralID, coralData.CoralLevel.Value);
        if (coralData.CoralLevel.Value == 1)
        {
            UserData.SetStatistic(UNLOCKTYPE.BUY_CORAL, coralID, 1);
        }
        MessageDispatcher.Publish(EMessageType.LevelUpCoral, (coralID, addLevel));
        SoundManager.Instance.PlayPositiveSfx();
        Save().Forget();
    }
    public void UpgradeCoralBonusLevel(long coralID)
    {
        if (!UserData.Corals.TryGetValue(coralID, out var coral))
        {
            // Debug.LogError($"Not found Coral {coralID}");
            return;
        }
        int possibleBonusLevel = coral.CoralLevel.Value / 25;
        coral.BonusLevel.Value = possibleBonusLevel;
        Save().Forget();
    }

    public void ToggleHeartCheat()
    {
        UserData.Stone.Heart.Value = long.MaxValue;
        UserData.Stone.Heart.Value *= 1000;
        Save().Forget();
    }

    public void UpgradeStoneBonusLevel()
    {
        int possibleBonusLevel = UserData.Stone.StoneLevel.Value / 25;
        UserData.Stone.BonusLevel.Value = possibleBonusLevel;
        Save().Forget();
    }

    public long GetOfflineTimeSec()
    {
        if (UserData.Player.LastSavedTime <= 0)
        {
            Debug.LogError("UserData.Player.LastSavedTime <= 0");
            return 0;
        }

        long nowMs = GameTime.Instance.GetServerTimestampMs();
        long diffSec = (nowMs - UserData.Player.LastSavedTime) / 1000;
        return Math.Clamp(diffSec, 0, ConfigTable.Instance.OfflineRewardMaxTime);
    }
    public void LevelUpStone(int add)
    {
        var cost = CalculateStoneUpgradeCost(add);
        if (!HasEnoughItem(new ItemParam(ITEMTYPE.HEART, 0, cost)))
        {
            ToastMessage.ShowInsufficientItemMessage(ITEMTYPE.HEART);
            return;
        }

        RemoveItem(ITEMTYPE.HEART, cost);

        int prev = UserData.Stone.StoneLevel.Value;
        int next = prev + add;

        UserData.Stone.StoneLevel.Value = next;
        UserData.SetStatistic(UNLOCKTYPE.STONE_LEVELUP, 0, next);
        SoundManager.Instance.PlayPositiveSfx();

        // ✅ prev~next 사이에 있는 milestone을 전부 로깅
        LogEventMilestones(EVENTLOGTYPE.STONE_LEVEL, prev, next);

        Save().Forget();
    }

    public void ReceiveCollectionReward(long collectionId)
    {
        CollectionData collection = UserData.GetCollection(collectionId);

        if (collection.IsRewardable.Value)
        {
            collection.RewardReceived.Value = true;
            AddItem((ITEMTYPE)collection.Table.rewardtype, collection.Table.rewardcnt);
            PopupMultiReward.Show((ITEMTYPE)collection.Table.rewardtype, collection.Table.rewardidx, collection.Table.rewardcnt);
            Save().Forget();
        }
    }

    public async UniTask ReceiveDailyMissionReward(long missionId)
    {
        DailyMissionData dailyMission = UserData.GetDailyMission(missionId);

        if (dailyMission.UnlockData.IsCompleted && !dailyMission.IsRewardReceived.Value)
        {
            UserData.AddStatistic(UNLOCKTYPE.DAILY_MISSION_CLEAR, 0, 1);
            dailyMission.IsRewardReceived.Value = true;
            AddItem((ITEMTYPE)dailyMission.Table.rewardtype, dailyMission.Table.rewardcnt);
            if (missionId == GameDefine.DailyAllClearMissionId)
            {
                LogEvent("daily_mission_done");
            }
            Save().Forget();
            MessageDispatcher.Publish(EMessageType.ReceiveDailyMissionReward, missionId);
            await PopupMultiReward.Show((ITEMTYPE)dailyMission.Table.rewardtype, dailyMission.Table.rewardid, dailyMission.Table.rewardcnt);
        }
    }

    public void ReceiveAchievementReward(long achievementId)
    {
        AchievementData achievement = UserData.GetAchievement(achievementId);

        if (achievement.UnlockData.IsCompleted && !achievement.IsRewardReceived.Value)
        {
            achievement.IsRewardReceived.Value = true;
            AddItem((ITEMTYPE)achievement.Table.rewardtype, achievement.Table.rewardid, achievement.Table.rewardcnt);
            PopupMultiReward.Show((ITEMTYPE)achievement.Table.rewardtype, achievement.Table.rewardid, achievement.Table.rewardcnt).Forget();
            RefreshAchievementSlot(achievement.Table.slot);
            Save().Forget();
            MessageDispatcher.Publish(EMessageType.ReceiveAchievementReward, achievementId);
        }
    }

    public BigInteger CalculateStoneUpgradeCost(int levelsToAdd)
    {
        return StoneCostCalculator.CalculateStoneUpgradeCost(UserData.Stone.StoneLevel.Value, levelsToAdd);
    }



    public void PlaceFishInWorld(int tid)
    {
        if (UserData.FishCountInWorld.Value >= UserData.WorldFishLimit.Value)
        {
            var retrieveFishID = UserData.PickRandomFishInWorld(tid);
            if (retrieveFishID == -1)
            {
                return;
            }
            RetrieveFishesToTank((int)retrieveFishID);
        }
        UserData.PlaceFishesInWorld(tid);
    }

    public bool RetrieveFishesToTank(int tid)
    {
        UserData.RetrieveFishesToTank(tid);
        return true;
    }

    public long GetSkillCooltime(long id)
    {
        var skillData = UserData.GetSkill(id);
        long cooltime = skillData.Table.cooltime > skillData.Table.activetime ? skillData.Table.cooltime : skillData.Table.activetime;
        long cooltimeMs = cooltime * 1000;
        // if (id == (int)SkillType.ResetAllSkillCooltime)
        {
            var conchData = UserData.GetArtifact((int)ArtifactType.Conch);
            cooltimeMs = cooltimeMs - cooltimeMs * conchData.Table.GetEffect(conchData.Level.Value) / 100;
        }
        return cooltimeMs;
    }

    public async UniTask CastSkill(long id)
    {
        if ((SkillType)id == SkillType.ResetAllSkillCooltime)
        {
            bool success = await ShowAD(GameDefine.AD_ResetSkill);
            if (!success)
                return;
        }

        long cooltimeMs = GetSkillCooltime(id);
        var skillData = UserData.GetSkill(id);
        skillData.Castkill(cooltimeMs).Forget();

        switch ((SkillType)id)
        {
            case SkillType.Food:
                {
                    ProduceFoodSkillHeart();
                }
                break;
            case SkillType.ResetAllSkillCooltime:
                {
                    ResetAllSkillCooltime();
                }
                break;
        }
        UserData.AddStatistic(UNLOCKTYPE.USE_SKILL, 0, 1);
        UserData.AddStatistic(UNLOCKTYPE.USE_TARGET_SKILL, id, 1);
        SoundManager.Instance.PlaySkillSfx();
        Save().Forget();
    }
    public void LevelUpArtifact(long id)
    {
        var artifactData = UserData.GetArtifact(id);
        int cost = artifactData.GetArtifactLevelCost(artifactData.Level.Value);
        int prevLevel = artifactData.Level.Value;
        artifactData.LevelUp();
        if (prevLevel == 0)
        {
            GameManager.Instance.PlayOpenArtifact(id).Forget();
        }
        RemoveItem(ITEMTYPE.GEM, cost);
        UserData.AddStatistic(UNLOCKTYPE.ARTIFACT_LEVELUP, id, 1);
        int minLevel = UserData.Artifacts.Min(artifact => artifact.Value.Level.Value);
        UserData.SetStatistic(UNLOCKTYPE.ARTIFACT_LEVELUP_ALL, 0, minLevel);
        SoundManager.Instance.PlayPositiveSfx();
        Save().Forget();
    }
    public void LevelUpSkill(long id)
    {
        var skillData = UserData.GetSkill(id);
        int cost = skillData.GetSkillLevelCost();
        skillData.LevelUpSkill();
        UserData.AddStatistic(UNLOCKTYPE.SKILL_LEVELUP, id, 1);

        int minLevel = UserData.Skills.Where(skill =>
            skill.Value.Table.id == (int)SkillType.Volcano ||
            skill.Value.Table.id == (int)SkillType.Food ||
            skill.Value.Table.id == (int)SkillType.MoonSkill
        )
        .Min(skill => skill.Value.Level.Value);
        UserData.SetStatistic(UNLOCKTYPE.SKILL_LEVELUP_ALL, 0, minLevel);

        RemoveItem(ITEMTYPE.GEM, cost);
        SoundManager.Instance.PlayPositiveSfx();
        Save().Forget();
    }
    public void MarkFishAsViewd(long tid)
    {
        var fishData = UserData.GetFish(tid);
        fishData.IsViewed.Value = true;
        Save().Forget();
    }

    public void AddItem(ITEMTYPE itemType, BigInteger count)
    {
        if (count <= 0)
            return;
        AddItem(new ItemParam(itemType, 0, count));
    }
    public void AddItem(ITEMTYPE itemType, int itemId, BigInteger count)
    {
        AddItem(new ItemParam(itemType, itemId, count));
    }
    private void AddItem(ItemParam itemParam)
    {
        if (itemParam.Type == ITEMTYPE.HEARTTIMEREWARD)
        {
            var delta = HeartPerSec.Value * itemParam.Count;
            itemParam = new ItemParam(ITEMTYPE.HEART, 0, delta);
        }

        UserData.AddItem(itemParam);
        switch (itemParam.Type)
        {
            case ITEMTYPE.GEM:
            case ITEMTYPE.PEARL:
            case ITEMTYPE.GEM_FREE:
            case ITEMTYPE.HEART:
                {

                }
                break;
            case ITEMTYPE.FISH:
                {
                    var fishData = UserData.GetFish(itemParam.ItemId);
                    fishData.IsViewed.Value = true;
                    UserData.SetStatistic(UNLOCKTYPE.HAVE_FISH, itemParam.ItemId, fishData.FishTotalCount.Value);
                    UserData.SetStatistic(UNLOCKTYPE.HAVE_FISH_ALL, 0, UserData.FishTotalCount.Value);
                    UserData.AddStatistic(UNLOCKTYPE.FISH_CREATE, itemParam.ItemId, (long)itemParam.Count);
                    UserData.AddStatistic(UNLOCKTYPE.FISH_CREATE_ALL, 0, (long)itemParam.Count);
                    for (int i = 0; i < itemParam.Count; i++)
                    {
                        PlaceFishInWorld(itemParam.ItemId);
                    }
                    // UserData.AddStatistic(UNLOCKTYPE.CREATE_NEW_FISH_TYPE, fishData.Table.fishgroupid, (long)itemParam.Count);
                }
                break;
            case ITEMTYPE.HEARTTIMEREWARD:
                {

                }
                break;
        }
    }
    public void RemoveItem(ITEMTYPE type, int itemId, BigInteger amount)
    {
        RemoveItem(new ItemParam(type, itemId, amount));
    }
    public void RemoveItem(ITEMTYPE type, BigInteger amount)
    {
        RemoveItem(new ItemParam(type, 0, amount));
    }

    public void UseFishTicket()
    {
        UserData.Stone.UsedFishTicket.Value++;
    }
    private void RemoveItem(ItemParam itemParam)
    {
        UserData.RemoveItem(itemParam);
        switch (itemParam.Type)
        {
            case ITEMTYPE.FISH:
                {
                    var fishData = UserData.GetFish(itemParam.ItemId);
                    UserData.SetStatistic(UNLOCKTYPE.HAVE_FISH, itemParam.ItemId, fishData.FishTotalCount.Value);
                    UserData.SetStatistic(UNLOCKTYPE.HAVE_FISH_ALL, 0, UserData.FishTotalCount.Value);
                }
                break;
        }
    }

    public bool HasEnoughItem(ItemParam param) => UserData.HasEnoughItem(param);

    public bool HasEnoughItems(List<ItemParam> param)
    {
        return param.All(HasEnoughItem);
    }

    public async UniTask<bool> ShowAD(int adId)
    {
        // TouchBlockManager.Instance.Add();
        // SoundManager.Instance.PauseAll(0);

        bool success = await ADManager.Instance.ShowRewardAdAsync();
        if (!success)
        {
            // SoundManager.Instance.ResumeAll(0);
            // TouchBlockManager.Instance.Remove();
            await PopupConfirmCancel.ShowConfirmAsync(ConfirmPopupType.OK, "(Load Ad Failed)", "OK");
            return false;
        }
#if UNITY_EDITOR
        ToastMessage.Show("AD Watch");
#endif
        UserData.AddStatistic(UNLOCKTYPE.AD_VIEW, 0, 1);
        LogEvent(EVENTLOGTYPE.WATCH_AD, adId);
        // TouchBlockManager.Instance.Remove();
        // SoundManager.Instance.ResumeAll(0);
        return true;
    }
    public async UniTask ReceiveMysteryBoxReward()
    {
        bool success = await ShowAD(GameDefine.Ad_MysteryBox);
        if (!success)
            return;
        var adData = UserData.GetAd(GameDefine.Ad_MysteryBox);
        AddItem(ITEMTYPE.HEART, HeartPerSec.Value * GameDefine.FreeHeartProductionSec);
        UserData.AddStatistic(UNLOCKTYPE.OPEN_MYSTERY_BOX, 0, 1);
        adData.StartCooldown(adData.Table.cooltime);
        Save().Forget();
        PopupMultiReward.Show(ITEMTYPE.HEART, 0, Instance.HeartPerSec.Value * GameDefine.FreeHeartProductionSec).Forget();
    }

    public async UniTask ReceiveFreeHeart()
    {
        bool success = await ShowAD(GameDefine.Ad_FreeHeart);
        if (!success)
            return;
        var adData = UserData.GetAd(GameDefine.Ad_FreeHeart);
        BigInteger heartPerSec = HeartPerSec.Value == 0 ? 1 : HeartPerSec.Value;
        AddItem(ITEMTYPE.HEART, heartPerSec * GameDefine.FreeHeartProductionSec);
        adData.StartCooldown(adData.Table.cooltime);
        Save().Forget();
        PopupMultiReward.Show(ITEMTYPE.HEART, 0, Instance.HeartPerSec.Value * GameDefine.FreeHeartProductionSec).Forget();
    }

    public async UniTask CastMidasSkill()
    {
        var shopInfo = DataManager.Instance.GetShopData(GameDefine.HandOfMidasShopID);
        bool success = await ShowAD(shopInfo.itemidx);
        if (!success)
            return;
        var adData = UserData.GetAd(shopInfo.itemidx);
        adData.StartCooldown(adData.Table.cooltime);
        GameManager.Instance.CastSkill((int)SkillType.MidasSkill).Forget();
        Save().Forget();
    }

    public async UniTask<List<DataManager.Reward>> ReceiveShopReward(int shopID)
    {
        var shopInfo = DataManager.Instance.GetShopData(shopID);
        if (shopInfo.itemtype == ITEMTYPE.AD)
        {
            bool success = await ShowAD(shopInfo.itemidx);
            if (!success)
                return null;
            var adData = UserData.GetAd(shopInfo.itemidx);
            adData.StartCooldown(adData.Table.cooltime);
        }
        else
        {
            RemoveItem(shopInfo.itemtype, shopInfo.itemcnt);
        }
        var rewards = DataManager.Instance.GetRewardsByGroupID(shopInfo.rewardgroupidx);
        foreach (var item in rewards)
        {
            AddItem(item.itemtype, item.itemcnt);
        }
        LogEvent(EVENTLOGTYPE.BUY_DIA, shopID);

        Save().Forget();
        return rewards;
    }

    public async UniTask<bool> ReceiveAdBonus(int gemCount)
    {
        bool success = await ShowAD(GameDefine.Ad_DailyMission_Bonus);
        if (!success)
            return false;
        AddItem(ITEMTYPE.GEM_FREE, gemCount);
        Save().Forget();
        PopupMultiReward.Show(ITEMTYPE.GEM_FREE, 0, gemCount).Forget();
        return true;
    }
    public async UniTask ReceiveOfflineReward(BigInteger cnt)
    {
        AddItem(ITEMTYPE.HEART, cnt);
        LogEvent("offline_bonus");
        Save().Forget();
        await PopupMultiReward.Show(ITEMTYPE.HEART, 0, cnt);
    }
    public async UniTask ReceiveOfflineRewardADBonus(BigInteger cnt)
    {
        BigInteger rewardCnt = cnt;
        bool success = await ShowAD(GameDefine.Ad_Normal);
        if (success)
        {
            rewardCnt += cnt;
        }
        AddItem(ITEMTYPE.HEART, rewardCnt);
        LogEvent("ad_offline_2bonus");
        Save().Forget();
        await PopupMultiReward.Show(ITEMTYPE.HEART, 0, rewardCnt);
    }
    public async UniTask ReceiveIapReward(BigInteger cnt)
    {
        AddItem(ITEMTYPE.GEM, cnt);
        Save().Forget();
        await PopupMultiReward.Show(ITEMTYPE.GEM, 0, cnt);
    }
    public async UniTask ReceiveShareReward(BigInteger cnt)
    {
        UserData.Stone.ShareRewardCount.Value--;
        if (UserData.Stone.ShareRewardCount.Value < 0)
        {
            UserData.Stone.ShareRewardCount.Value = 0;
        }

        AddItem(ITEMTYPE.GEM, cnt);
        LogEvent("share_reward");
        Save().Forget();
    }
    static void LogEventMilestones(EVENTLOGTYPE type, int prev, int next)
    {
        var list = DataManager.Instance?.EventlogArray;
        if (list == null) return;

        // milestone 정의가 eventlogArray에 들어있다고 가정
        foreach (var e in list)
        {
            if (e.logtype != type) continue;

            int milestone = e.targetvalue; // (오타면 tatgetvalue로)
            if (milestone > prev && milestone <= next)
            {
                LogEvent(e.logkey);
            }
        }
    }
    public static void LogEvent(EVENTLOGTYPE type, int targetValue)
    {
        var eventLogInfo = DataManager.Instance.EventlogArray.FirstOrDefault(item => item.logtype == type && item.targetvalue == targetValue);
        if (eventLogInfo != null)
        {
            LogEvent(eventLogInfo.logkey);
            // Debug.LogError($"LogEvent {eventLogInfo.logkey}, {type}, {targetValue}");
        }
    }
    public static void LogEvent(string name)
    {
        // Debug.LogError($"LogEvent {name} ");
        AIT.EventLog(new EventLogParams()
        {
            Log_name = name
        });
    }
}

