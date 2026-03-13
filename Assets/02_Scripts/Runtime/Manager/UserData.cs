
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using AppsInToss;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;


/// <summary>
/// 저장 전용 DTO(직렬화 목적). 런타임 모델과 분리한다.
/// </summary>
/// 

/// <summary>
/// 런타임 모델(게임 로직에서 사용).
/// 내부 상태는 캡슐화하고, 읽기 전용 인터페이스로만 노출한다.
/// </summary>
/// 

#region CoralData
[Serializable]
public sealed class CoralDataDto
{
    public long CoralId;
    public int CoralLevel;
    public int BonusLevel;
}
public sealed class CoralData : IUnlockable
{
    public long CoralId { get; private set; }
    public ReactiveProperty<int> CoralLevel { get; private set; }
    public ReactiveProperty<int> BonusLevel { get; private set; }
    public ReadOnlyReactiveProperty<int> AddFishCount { get; private set; }
    public ReadOnlyReactiveProperty<BigInteger> HeartPerSec { get; private set; }
    public ReactiveProperty<BigInteger> UpgradeCost_1;
    public ReactiveProperty<BigInteger> UpgradeCost_10;
    public ReactiveProperty<BigInteger> UpgradeCost_100;
    public UnlockData UnlockData { get; private set; } = new UnlockData();
    public DataManager.Coral Table;


    private CoralCostCalculator coralCostCalculator;
    private CompositeDisposable _disposable;

    public void ApplyDto(CoralDataDto dto)
    {
        CoralId = dto.CoralId;
        CoralLevel.Value = dto.CoralLevel;
        BonusLevel.Value = dto.BonusLevel;
    }
    public CoralDataDto ToDto()
    {
        return new CoralDataDto
        {
            CoralId = this.CoralId,
            CoralLevel = this.CoralLevel.Value,
            BonusLevel = this.BonusLevel.Value
        };
    }

    public CoralData(long coralId)
    {
        CoralId = coralId;
        Table = DataManager.Instance.GetCoralData((int)coralId);
        BonusLevel = new ReactiveProperty<int>();
        CoralLevel = new ReactiveProperty<int>();
        UpgradeCost_1 = new ReactiveProperty<BigInteger>();
        UpgradeCost_10 = new ReactiveProperty<BigInteger>();
        UpgradeCost_100 = new ReactiveProperty<BigInteger>();
        coralCostCalculator = new CoralCostCalculator();

        AddFishCount = CoralLevel.DistinctUntilChanged().Select(level =>
        {
            if (level == 0)
                return 0;
            return Table.addfishmaxcount;
        }).ToReadOnlyReactiveProperty();

        _disposable?.Dispose();
        _disposable = new CompositeDisposable();

        CoralLevel
            .DistinctUntilChanged()
            .Subscribe(_ =>
            {
                UpgradeCost_1.Value = CalculateCoralUpgradeCost(1);
                UpgradeCost_10.Value = CalculateCoralUpgradeCost(10);
                UpgradeCost_100.Value = CalculateCoralUpgradeCost(100);
            })
            .AddTo(_disposable);

        UnlockData = UnlockController.Instance.RegisterUnlockData(this, coralId, Table.unlockdesc, Table.unlocktype, Table.unlocktarget, Table.unlockvalue);
    }

    public void BindHearPerSec()
    {
        var udm = UserDataManager.Instance;

        HeartPerSec =
            Observable.CombineLatest(
                CoralLevel,
                BonusLevel,
                udm.MoonSkillFactor,
                udm.UserData.FishTotalCount,
                udm.UserData.Stone.EvoFishCountByTearStone,
                udm.UserData.Stone.EvolutionBonus,
                (coral, bonus, moon, fish, evoFishByTearstone, evoBonus) =>
                    CalcAutoProduceHeart(coral, bonus) // BigInteger 반환
            )
            .DistinctUntilChanged() // BigInteger 비교 가능하면 그대로 OK
            .ToReadOnlyReactiveProperty(BigInteger.Zero);
    }
    private readonly object _costCacheLock = new();

    private BigInteger CalculateCoralUpgradeCost(int levelsToAdd)
    {
        if (levelsToAdd <= 0)
            return BigInteger.Zero;

        int coralId = (int)CoralId;
        int currentLevel = CoralLevel.Value;

        BigInteger total = BigInteger.Zero;

        return coralCostCalculator.CalculateStoneUpgradeCost(coralId, currentLevel, levelsToAdd);

        // // i=0일 때 level=currentLevel 비용부터 levelsToAdd개 합산
        // // cost(level) = level -> level+1 비용
        // for (int i = 0; i < levelsToAdd; i++)
        // {
        //     int level = currentLevel + i;
        //     total += GetCostAtLevelCached(coralId, level);
        // }

        // return total;
    }

    // private BigInteger GetCostAtLevelCached(int coralId, int level)
    // {
    //     if (level < 0)
    //         return BigInteger.Zero;

    //     lock (_costCacheLock)
    //     {
    //         var dict = GetOrCreateCostDict(coralId);

    //         if (dict.TryGetValue(level, out var cached))
    //             return cached;

    //         EnsureCostComputedUpToLevel_NoLock(coralId, level, dict);

    //         if (dict.TryGetValue(level, out cached))
    //             return cached;

    //         Debug.LogWarning($"[CostCache] Failed to compute cost for coralId={coralId}, level={level}. Returning 0.");
    //         return BigInteger.Zero;
    //     }
    // }

    // private Dictionary<int, BigInteger> GetOrCreateCostDict(int coralId)
    // {
    //     if (!_costCache.TryGetValue(coralId, out var dict))
    //     {
    //         dict = new Dictionary<int, BigInteger>(capacity: 256);
    //         _costCache[coralId] = dict;
    //     }
    //     return dict;
    // }

    // // lock 잡힌 상태에서만 호출
    // private void EnsureCostComputedUpToLevel_NoLock(int coralId, int targetLevel, Dictionary<int, BigInteger> dict)
    // {
    //     // ----- 현재 진행상태 로드 -----
    //     if (!_lastComputedLevel.TryGetValue(coralId, out int lastLevel))
    //         lastLevel = -1;

    //     if (!_lastComputedCost.TryGetValue(coralId, out BigInteger lastCost))
    //         lastCost = BigInteger.Zero;

    //     // ----- 시드(0레벨) 보장 -----
    //     // lastLevel == -1 이면 아직 cost(0)도 안 넣은 상태
    //     if (lastLevel < 0)
    //     {
    //         BigInteger seedCost0 = GetSeedCostLevel0_NoLock(coralId);
    //         dict[0] = seedCost0;

    //         lastLevel = 0;
    //         lastCost = seedCost0;

    //         _lastComputedLevel[coralId] = lastLevel;
    //         _lastComputedCost[coralId] = lastCost;
    //     }

    //     if (lastLevel >= targetLevel)
    //         return;

    //     // ----- 1부터 target까지 누적 계산 -----
    //     for (int level = lastLevel + 1; level <= targetLevel; level++)
    //     {
    //         BigInteger cost;

    //         if (coralId == 1)
    //         {
    //             // 1번 산호: 0~26은 테이블, 27부터 1.07 누적
    //             if (level <= 26)
    //             {
    //                 var costData = DataManager.Instance.GetLevelUpCost(level);
    //                 cost = costData != null ? costData.corallevelupcost : BigInteger.Zero;
    //             }
    //             else
    //             {
    //                 cost = (lastCost * NUM_BASE) / DEN_BASE; // floor(prev*1.07)
    //             }
    //         }
    //         else
    //         {
    //             // id != 1 : level>=1부터 1.07 누적
    //             cost = (lastCost * NUM_BASE) / DEN_BASE; // floor(prev*1.07)
    //         }

    //         dict[level] = cost;
    //         lastLevel = level;
    //         lastCost = cost;
    //     }

    //     _lastComputedLevel[coralId] = lastLevel;
    //     _lastComputedCost[coralId] = lastCost;
    // }

    // // lock 잡힌 상태에서만 호출
    // private BigInteger GetSeedCostLevel0_NoLock(int coralId)
    // {
    //     if (coralId == 1)
    //     {
    //         // 1번 산호: 테이블 0레벨 비용
    //         var costData = DataManager.Instance.GetLevelUpCost(0);
    //         return costData != null ? costData.corallevelupcost : BigInteger.Zero;
    //     }

    //     // id != 1 : cost(0) = Table.costcnt
    //     // BigInteger.Parse 반복 제거를 위해 캐시
    //     if (_baseCostCache.TryGetValue(coralId, out var cached))
    //         return cached;

    //     BigInteger baseCost;
    //     try
    //     {
    //         baseCost = BigInteger.Parse(Table.costcnt);
    //     }
    //     catch (Exception e)
    //     {
    //         Debug.LogError($"[CostCache] Failed to parse Table.costcnt='{Table.costcnt}' for coralId={coralId}. {e}");
    //         baseCost = BigInteger.Zero;
    //     }

    //     _baseCostCache[coralId] = baseCost;
    //     return baseCost;
    // }

    public BigInteger CalcAutoProduceHeart(int coralLevel, int bonusLevel)
    {
        if (Table == null || coralLevel <= 0)
            return 0;

        BigInteger coralBonus = BigInteger.Pow(2, bonusLevel);
        BigInteger baseAmount = BigInteger.Parse(Table.productamount);

        // (레벨당 10% 증가) → (10 + (level - 1)) / 10
        int numerator = 10 + (coralLevel - 1);
        BigInteger result = baseAmount * coralBonus * numerator / 10;

        // 2) 캐싱: 스톤 및 피쉬 수치
        var stone = UserDataManager.Instance.UserData.Stone;
        long evoPercent = stone.EvolutionBonus.Value;        // 예: 5 → 5%
        int fishCount = UserDataManager.Instance.UserData.FishTotalCount.Value;

        // 3) 곱셈 요소 계산 (BigInteger는 최소한만 사용)
        BigInteger fishMultiplier = BigInteger.Pow(2, fishCount);          // 매우 커질 수 있음
        BigInteger evoMultiplierNum = 100 + evoPercent;                    // (100 + X) / 100

        // 4) 메인 계산
        result *= fishMultiplier;                                          // 큰 값부터
        result *= UserDataManager.Instance.MoonSkillFactor.Value;                                   // 중간 계수
        result = (result * evoMultiplierNum) / 100;                        // 마지막에 퍼센트 적용

        // 5) 할당
        return result;
    }
    public void SetLevel(int level)
    {
        CoralLevel.Value = level;
    }

    public void Complete()
    {
    }

    public UnlockOwnerType GetUnlockOwnerType()
    {
        return UnlockOwnerType.Coral;
    }
}
#endregion

#region  ADData
public class ADDataDto
{
    public int Tid;
    public long CoolEndtimeMs;
}

public class ADData
{
    public int Tid;
    public long CoolEndtimeMs;
    public ReactiveProperty<long> CooldownRemainSec;
    public ReadOnlyReactiveProperty<bool> IsReady;
    private CancellationTokenSource cts;
    public DataManager.Ad Table { get; private set; }

    public ADData(int id)
    {
        Tid = id;
        Table = DataManager.Instance.GetAdData(id);
        CoolEndtimeMs = 0;
        CooldownRemainSec = new ReactiveProperty<long>();
        IsReady = CooldownRemainSec.DistinctUntilChanged().Select(sec => sec <= 0).ToReadOnlyReactiveProperty();
    }
    public void Dispose()
    {
        cts?.CancelAndDispose();
    }
    public void ApplyDto(ADDataDto dto)
    {
        Tid = dto.Tid;
        CoolEndtimeMs = dto.CoolEndtimeMs;
        RunCooldownLoop().Forget();
    }
    public ADDataDto ToDto()
    {
        return new ADDataDto
        {
            Tid = Tid,
            CoolEndtimeMs = CoolEndtimeMs
        };
    }
    public void StartCooldown(long durationSec)
    {
        CoolEndtimeMs = GameTime.Instance.GetServerTimestampMs() + durationSec * 1000;
        RunCooldownLoop().Forget();
    }
    private async UniTask RunCooldownLoop()
    {
        cts.CancelAndDispose();
        cts = new CancellationTokenSource();
        while (true)
        {
            long nowMs = GameTime.Instance.GetServerTimestampMs();           // ms 단위
            long remainMs = CoolEndtimeMs - nowMs;

            if (remainMs <= 0)
            {
                CooldownRemainSec.Value = 0;              // 초 단위 표시
                break;
            }

            // 초 단위로만 표시 (올림해서 사용자 체감과 맞춤: 1201ms -> 2초)
            long remainSec = Math.Max(0, (int)Math.Ceiling(remainMs / 1000.0));
            if (CooldownRemainSec.Value != remainSec)
                CooldownRemainSec.Value = remainSec;

            // 다음 "초 경계"까지 남은 시간만큼 대기 (경계 정렬)
            int waitMs = (int)(remainMs % 1000);
            if (waitMs == 0)
                waitMs = 1000;

            await UniTask.Delay(waitMs, cancellationToken: cts.Token);
            // 루프 재진입 시 다시 now/remain을 '절대시간' 기준으로 계산하므로 드리프트 적음
        }
    }
}

#endregion

#region FishData
[Serializable]
public class FishDataDto
{
    public long Tid;
    public int FishCountInWorld;
    public int FishCount;
    public bool Found;
    public bool IsViewed;
}
public class FishData : IUnlockable
{
    public long Tid;
    public ReactiveProperty<int> FishCountInWorld;
    public ReactiveProperty<int> FishTotalCount;
    public ReadOnlyReactiveProperty<int> FishTankCount;
    public ReactiveProperty<bool> IsViewed;
    public bool Found;
    public DataManager.FishInfo Table { get; private set; }
    public List<UnlockData> unlockDatas;

    public FishData(long id)
    {
        FishCountInWorld = new ReactiveProperty<int>();
        FishTotalCount = new ReactiveProperty<int>();
        FishTankCount = FishCountInWorld.CombineLatest(FishTotalCount, (inWorld, total) => total - inWorld).ToReadOnlyReactiveProperty();
        unlockDatas = new List<UnlockData>();
        IsViewed = new ReactiveProperty<bool>(false);
        Tid = id;
        Table = DataManager.Instance.GetFishInfoData((int)Tid);
        var unlockConditionList = DataManager.Instance.GetFishUnlockConditions((int)Tid);
        foreach (var item in unlockConditionList)
        {
            unlockDatas.Add(UnlockController.Instance.RegisterUnlockData(this, item.id, item.unlockdesc, item.unlocktype, item.unlocktarget, item.unlockvalue));
        }
    }

    public bool IsAllUnlockCompleted()
    {
        if (unlockDatas == null || unlockDatas.Count == 0)
            return false; // 아무 조건도 없으면 '완료'로 보지 않음 (명시적)

        return unlockDatas.All(x => x.IsCompleted);
    }

    public void AddFish(int add)
    {
        FishTotalCount.Value += add;
    }
    public void RemoveFish(int count)
    {
        int removable = Mathf.Min(count, FishTotalCount.Value);
        FishTotalCount.Value -= removable;
        if (FishTotalCount.Value < FishCountInWorld.Value)
        {
            FishCountInWorld.Value = FishTotalCount.Value;
        }
    }
    public void ApplyDto(FishDataDto dto)
    {
        Tid = dto.Tid;
        FishTotalCount.Value = dto.FishCount;
        FishCountInWorld.Value = dto.FishCountInWorld;
        Found = dto.Found;
        IsViewed.Value = dto.IsViewed;
    }
    public FishDataDto ToDto()
    {
        return new FishDataDto
        {
            Tid = Tid,
            FishCount = FishTotalCount.Value,
            FishCountInWorld = FishCountInWorld.Value,
            Found = Found,
            IsViewed = IsViewed.Value,
        };
    }

    public void Complete()
    {
        if (!Found)
        {
            if (unlockDatas.All(item => item.IsCompleted))
            {
                Found = true;
                if (Table.fishtype == FISHTYPE.HIDDEN)
                {
                    MessageDispatcher.Publish(EMessageType.UnlockHiddenFish, Table.id);
                }
            }
        }
    }

    public UnlockOwnerType GetUnlockOwnerType()
    {
        return UnlockOwnerType.Fish;
    }
}
#endregion

#region DailyMissionData
public class DailyMissionDataDto
{
    public long Tid;
    public bool IsCompleted;
    public bool IsRewardReceived;
}

public class DailyMissionData : IUnlockable
{
    public long Tid;
    public ReactiveProperty<bool> IsRewardReceived;
    public ReactiveProperty<bool> IsCompleted;
    public ReadOnlyReactiveProperty<bool> IsRewardable;
    public DataManager.DailyMission Table;
    public UnlockData UnlockData { get; private set; }
    public DailyMissionData(long id)
    {
        Table = DataManager.Instance.GetDailyMissionData((int)id);
        Tid = id;
        IsCompleted = new ReactiveProperty<bool>(false);
        IsRewardReceived = new ReactiveProperty<bool>(false);
        UnlockData = DailyUnlockController.Instance.RegisterUnlockData(this, id, Table.missiondesc, (UNLOCKTYPE)Table.missiontype, Table.missiontarget, Table.missionvalue);
        IsRewardable = IsCompleted.CombineLatest(IsRewardReceived, (completed, received) =>
        {
            return completed && !received && UnlockData.IsCompleted;
        }).ToReadOnlyReactiveProperty();

    }
    public void ApplyDto(DailyMissionDataDto dto)
    {
        Tid = dto.Tid;
        IsRewardReceived.Value = dto.IsRewardReceived;
    }
    public DailyMissionDataDto ToDto()
    {
        return new DailyMissionDataDto
        {
            Tid = Tid,
            IsRewardReceived = IsRewardReceived.Value
        };
    }

    public void Complete()
    {
        IsCompleted.Value = true;
        Debug.Log($"Daily Mission {Tid} completed.");
    }

    public UnlockOwnerType GetUnlockOwnerType()
    {
        return UnlockOwnerType.DailyMission;
    }
}
#endregion

#region CollectionData
public class CollectionDataDto
{
    public long Tid;
    public bool RewardReceived;
}

public class CollectionItemData : IUnlockable
{
    public DataManager.CollectionItem Table;
    public UnlockData unlockData;
    public ReactiveProperty<bool> IsCompleted { get; private set; }

    public void Init(long tid)
    {
        IsCompleted = new ReactiveProperty<bool>();
        Table = DataManager.Instance.GetCollectionItemData((int)tid);
        unlockData = UnlockController.Instance.RegisterUnlockData(this, tid, "have_fish_desc", UNLOCKTYPE.HAVE_FISH, Table.objectidx, Table.objectcnt);
    }

    public void Complete()
    {
        IsCompleted.Value = true;
    }

    public string GetUnlockDescKey()
    {
        return string.Empty;
    }

    public UnlockOwnerType GetUnlockOwnerType()
    {
        return UnlockOwnerType.CollectionItem;
    }
}
public class CollectionData
{
    public long Tid { get; private set; }
    public ReactiveProperty<bool> RewardReceived { get; private set; }
    public ReadOnlyReactiveProperty<bool> IsCompleted;
    public ReadOnlyReactiveProperty<bool> IsRewardable;
    public List<CollectionItemData> Items;

    public DataManager.Collection Table;

    public CollectionData(long id)
    {
        Tid = id;
        RewardReceived = new ReactiveProperty<bool>();
        Table = DataManager.Instance.GetCollectionData((int)id);
        Items = new List<CollectionItemData>();
        var itemDatas = DataManager.Instance.GetCollectionItemGroup((int)id);
        foreach (var item in itemDatas)
        {
            var data = new CollectionItemData();
            data.Init(item.id);
            Items.Add(data);
        }
        IsCompleted = Items.Select(item => item.IsCompleted)
                .CombineLatest()
                .Select(list => list.All(x => x))
                .ToReadOnlyReactiveProperty();

        IsRewardable = RewardReceived.CombineLatest(IsCompleted, (r, c) =>
        {
            return !r && c;
        }).ToReadOnlyReactiveProperty();
    }
    public void ApplyDto(CollectionDataDto dto)
    {
        Tid = dto.Tid;
        RewardReceived.Value = dto.RewardReceived;
    }

    public CollectionDataDto ToDto()
    {
        return new CollectionDataDto
        {
            Tid = Tid,
            RewardReceived = RewardReceived.Value
        };
    }

    public int GetCollectedItemCount()
    {
        return Items.Count(item => item.unlockData.IsCompleted);
    }
}
#endregion

#region  GuideMission
public class GuildeMissionDataDto
{
    public int Tid;
    public bool IsRewardReceived;
    public long CurrValue;
}
public class GuildeMissionData : IDisposable
{
    public ReactiveProperty<int> Tid { get; }
    public ReactiveProperty<long> CurrentValue { get; }
    public ReadOnlyReactiveProperty<bool> IsCompleted { get; }

    public DataManager.GuideMission Table { get; private set; }

    private readonly CompositeDisposable _disposables = new CompositeDisposable();

    public GuildeMissionData(int id)
    {
        // 기본 값 세팅
        Tid = new ReactiveProperty<int>(id).AddTo(_disposables);
        CurrentValue = new ReactiveProperty<long>(0).AddTo(_disposables);

        // 테이블 로딩
        Table = DataManager.Instance.GetGuideMissionData(id);

        // 미션 완료 여부 스트림 (한 번만 생성)
        IsCompleted = CurrentValue
            .Select(v => v >= Table.missionvalue) // Table 필드를 캡처하므로, 나중에 Table 교체해도 이후 값에서 새 missionvalue 사용
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
    }

    /// <summary>
    /// 다른 id로 재설정하거나, 처음 생성 직후 초기화 용도
    /// </summary>
    public void GenerateMissionData(int id)
    {
        CurrentValue.Value = 0;
        Tid.Value = id;
        Table = DataManager.Instance.GetGuideMissionData(id);
        if (Table != null)
        {
            StatisticData statistic = UserDataManager.Instance.UserData.GetStatistic((UNLOCKTYPE)Table.missiontype, Table.missiontarget);
            if (statistic != null)
            {
                CurrentValue.Value = statistic.Value;
                Debug.Log($"GenerateMissionData {id}, value {statistic.Value}");
            }
            else
            {
                CurrentValue.Value = 0;
            }
        }
    }

    public void ApplyDto(GuildeMissionDataDto dto)
    {
        // dto.Tid가 다르면 테이블도 같이 교체
        if (Tid.Value != dto.Tid)
        {
            Tid.Value = dto.Tid;
            Table = DataManager.Instance.GetGuideMissionData(dto.Tid);
        }
    }

    public GuildeMissionDataDto ToDto()
    {
        return new GuildeMissionDataDto
        {
            Tid = Tid.Value,
            CurrValue = CurrentValue.Value
        };
    }

    public UnlockOwnerType GetUnlockOwnerType()
    {
        return UnlockOwnerType.GuideMission;
    }

    public void AddProgress(long delta)
    {
        if (IsCompleted.Value || delta == 0)
            return;

        long newValue = CurrentValue.Value + delta;
        if (delta > 0 && newValue < CurrentValue.Value)
            newValue = long.MaxValue; // overflow → 상한 고정
        else if (delta < 0 && newValue > CurrentValue.Value)
            newValue = 0; // underflow → 0 고정
        CurrentValue.Value = Math.Max(0, newValue);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}

#endregion

#region Achievement
public class AchievementDataDto
{
    public long Tid;
    public bool RewardReceived;
}
public class AchievementData : IUnlockable
{
    public long Tid { get; private set; }
    public ReactiveProperty<bool> IsRewardReceived { get; private set; }
    public ReactiveProperty<bool> IsCompleted { get; private set; }
    public ReadOnlyReactiveProperty<bool> IsRewadable;
    public DataManager.Achievement Table;
    public UnlockData UnlockData { get; private set; }

    public AchievementData(long id)
    {
        Table = DataManager.Instance.GetAchievementData((int)id);
        Tid = id;
        IsRewardReceived = new ReactiveProperty<bool>();
        IsCompleted = new ReactiveProperty<bool>();
        UnlockData = UnlockController.Instance.RegisterUnlockData(this, id, Table.missiondesc, (UNLOCKTYPE)Table.missiontype, Table.missiontarget, Table.missionvalue);
        IsRewadable = IsRewardReceived.CombineLatest(IsCompleted, (r, c) =>
        {
            return !r && c;
        }).ToReadOnlyReactiveProperty();
    }
    public void ApplyDto(AchievementDataDto dto)
    {
        Tid = dto.Tid;
        IsRewardReceived.Value = dto.RewardReceived;
    }

    public AchievementDataDto ToDto()
    {
        return new AchievementDataDto
        {
            Tid = Tid,
            RewardReceived = IsRewardReceived.Value
        };
    }

    public void Complete()
    {
        IsCompleted.Value = true;
        MessageDispatcher.Publish(EMessageType.CompleteAchievement);
        Debug.Log($"Achievement {Tid} completed.");
    }

    public UnlockOwnerType GetUnlockOwnerType()
    {
        return UnlockOwnerType.AchievementItem;
    }
}
#endregion

#region Statistic

public class StatisticDataDto
{
    public string Id;
    public UNLOCKTYPE UnlockType;
    public long Target;
    public long Value;
}

public class StatisticData
{
    public string Id { get; private set; }
    public UNLOCKTYPE UnlockType { get; private set; }
    public long Target { get; private set; }
    public long Value { get; set; }
    public StatisticData(string id, UNLOCKTYPE type, long target)
    {
        Id = id;
        UnlockType = type;
        Target = target;
        Value = 0;
    }
    public void ApplyDto(StatisticDataDto dto)
    {
        Id = dto.Id;
        UnlockType = dto.UnlockType;
        Target = dto.Target;
        Value = dto.Value;
    }

    public StatisticDataDto ToDto()
    {
        return new StatisticDataDto
        {
            Id = this.Id,
            UnlockType = this.UnlockType,
            Target = this.Target,
            Value = this.Value,
        };
    }
}
#endregion

#region DailyStatistic
public class DailyStatisticDataDto
{
    public string Id;
    public UNLOCKTYPE UnlockType;
    public long Target;
    public long Value;
}
public class DailyStatisticData
{
    public string Id { get; private set; }
    public UNLOCKTYPE UnlockType { get; private set; }
    public long Target { get; private set; }
    public long Value { get; set; }
    public DailyStatisticData(string id, UNLOCKTYPE type, long target)
    {
        Id = id;
        UnlockType = type;
        Target = target;
        Value = 0;
    }
    public void ApplyDto(DailyStatisticDataDto dto)
    {
        Id = dto.Id;
        UnlockType = dto.UnlockType;
        Target = dto.Target;
        Value = dto.Value;
    }
    public DailyStatisticDataDto ToDto()
    {
        return new DailyStatisticDataDto
        {
            Id = this.Id,
            UnlockType = this.UnlockType,
            Target = this.Target,
            Value = this.Value,
        };
    }
}

#endregion

#region ArtifactData
public class ArtifactDataDto
{
    public long Tid { get; set; }
    public int Level { get; set; }
}

public class ArtifactData : IUnlockable
{
    public long Tid { get; private set; }
    public ReactiveProperty<int> Level { get; private set; }
    public DataManager.Artifact Table { get; private set; }
    public UnlockData UnlockData { get; private set; }

    public ArtifactData(long id)
    {
        Tid = id;
        Table = DataManager.Instance.GetArtifactData((int)id);
        Level = new ReactiveProperty<int>();
        UnlockData = UnlockController.Instance.RegisterUnlockData(this, id, Table.unlockdesc, Table.unlocktype, Table.unlocktarget, Table.unlockvalue);
    }
    public void ApplyDto(ArtifactDataDto dto)
    {
        Tid = dto.Tid;
        Level.Value = dto.Level;
    }

    public ArtifactDataDto ToDto()
    {
        return new ArtifactDataDto
        {
            Tid = Tid,
            Level = Level.Value
        };
    }
    public string GetEffectDesc()
    {
        string effectDesc = LocalizationManager.Instance.GetText(Table.artifactdesc);
        int targetLevel = Level.Value == 0 ? 1 : Level.Value;
        switch ((ArtifactType)Tid)
        {
            case ArtifactType.Nautilus:
                {
                    return string.Format(effectDesc, Table.GetEffect(targetLevel));
                }
            case ArtifactType.Clam:
                {
                    return string.Format(effectDesc, Table.GetEffect(targetLevel) * 10);
                }
            case ArtifactType.Conch:
                {
                    return string.Format(effectDesc, Table.GetEffect(targetLevel));
                }
        }
        return string.Empty;
    }
    public void LevelUp()
    {
        Level.Value++;
    }
    public int GetArtifactLevelCost(int targetLevel)
    {
        if (targetLevel >= 11)
        {
            return Table.levelupcost + (targetLevel - 10) * 100;
        }
        else
        {
            return Table.levelupcost;
        }
    }

    public void Complete()
    {
        MessageDispatcher.Publish(EMessageType.UnlockArtifact);
    }

    public UnlockOwnerType GetUnlockOwnerType()
    {
        return UnlockOwnerType.Artifact;
    }
}
#endregion

#region SkillData
public class SkillDataDto
{
    public long Tid;
    public int Level;
    public long SkillStartTimeMs;
    public long SkillEndTimeMs;
    public long CooldownEndTimeMs;
    public bool UnlockViewed;
}

public class SkillData : IUnlockable
{
    public long Tid;
    public ReactiveProperty<int> Level;

    public long SkillStartTimeMs;
    public long SkillEndTimeMs { get; private set; }
    public long CooldownEndTimeMs;
    public bool UnlockViewed;
    public ReactiveProperty<int> CooldownRemainTimeSec;
    public ReactiveProperty<int> SkillRemainTimeSec;
    public ReadOnlyReactiveProperty<bool> IsSkillActive;
    public DataManager.Skill Table;
    private CancellationTokenSource cooldownCts;
    private CancellationTokenSource skillCts;

    public UnlockData UnlockData { get; private set; }
    public string GetEffectDesc()
    {
        var durationMin = Table.GetDurationSec(Level.Value) / 60;
        int targetLevel = Level.Value == 0 ? 1 : Level.Value;
        switch ((SkillType)Table.id)
        {
            case SkillType.Volcano:
                {
                    return string.Format(LocalizationManager.Instance.GetText(Table.skilldesc), durationMin, Table.GetSkillEffect(targetLevel));
                }
            case SkillType.Food:
                {
                    return string.Format(LocalizationManager.Instance.GetText(Table.skilldesc), Table.GetSkillEffect(targetLevel));
                }
            case SkillType.MoonSkill:
                {
                    return string.Format(LocalizationManager.Instance.GetText(Table.skilldesc), durationMin, Table.GetSkillEffect(targetLevel) * 100);
                }
            case SkillType.MidasSkill:
                {
                    return "MidasSkill";
                }
        }
        return "Not Supported";
    }

    public int GetSkillLevelCost()
    {
        if (Level.Value == 0)
            return 0;
        if (Level.Value >= 11)
        {
            return Table.levelupcost + (Level.Value - 10) * 100;
        }
        else
        {
            return Table.levelupcost;
        }
    }

    public SkillStatus GetStatus()
    {
        long currTime = GameTime.Instance.GetServerTimestampMs();
        if (Level.Value <= 0 && UnlockData != null)
        {
            return SkillStatus.Lock;
        }

        if (currTime < CooldownEndTimeMs)
            return SkillStatus.Cooldown;

        if (currTime < SkillEndTimeMs)
            return SkillStatus.Activated;

        return SkillStatus.Ready;
    }

    public SkillData(long id)
    {
        Tid = id;
        Level = new ReactiveProperty<int>(0);
        SkillStartTimeMs = -1;
        SkillEndTimeMs = -1;
        CooldownEndTimeMs = -1;
        UnlockViewed = false;
        CooldownRemainTimeSec = new ReactiveProperty<int>(0);
        SkillRemainTimeSec = new ReactiveProperty<int>(0);
        Table = DataManager.Instance.GetSkillData((int)id);
        IsSkillActive = SkillRemainTimeSec
                        .Select(sec => sec > 0)
                        .DistinctUntilChanged()
                        .ToReadOnlyReactiveProperty();
        UnlockData = UnlockController.Instance.RegisterUnlockData(this, id, Table.unlockdesc, Table.unlocktype, Table.unlocktarget, Table.unlockvalue);
    }

    public void ApplyDto(SkillDataDto dto)
    {
        Tid = dto.Tid;
        Level.Value = dto.Level;
        SkillStartTimeMs = dto.SkillStartTimeMs;
        SkillEndTimeMs = dto.SkillEndTimeMs;
        CooldownEndTimeMs = dto.CooldownEndTimeMs;
        UnlockViewed = dto.UnlockViewed;
        RunCooldownLoop().Forget();
        RunSkillLoop().Forget();
    }

    public SkillDataDto ToDto()
    {
        return new SkillDataDto
        {
            Tid = Tid,
            Level = Level.Value,
            SkillStartTimeMs = SkillStartTimeMs,
            SkillEndTimeMs = SkillEndTimeMs,
            CooldownEndTimeMs = CooldownEndTimeMs,
            UnlockViewed = UnlockViewed
        };
    }

    public void Dispose()
    {
        cooldownCts?.CancelAndDispose();
        cooldownCts = null;

        skillCts?.CancelAndDispose();
        skillCts = null;
    }

    private async UniTask RunCooldownLoop()
    {
        cooldownCts.CancelAndDispose();
        cooldownCts = new CancellationTokenSource();
        while (true)
        {
            long nowMs = GameTime.Instance.GetServerTimestampMs();           // ms 단위
            long remainMs = CooldownEndTimeMs - nowMs;

            if (remainMs <= 0)
            {
                CooldownRemainTimeSec.Value = 0;              // 초 단위 표시
                break;
            }

            // 초 단위로만 표시 (올림해서 사용자 체감과 맞춤: 1201ms -> 2초)
            int remainSec = (int)Math.Ceiling(remainMs / 1000.0);
            if (CooldownRemainTimeSec.Value != remainSec)
                CooldownRemainTimeSec.Value = remainSec;

            // 다음 "초 경계"까지 남은 시간만큼 대기 (경계 정렬)
            int waitMs = (int)(remainMs % 1000);
            if (waitMs == 0) waitMs = 1000;

            await UniTask.Delay(waitMs, cancellationToken: cooldownCts.Token);
            // 루프 재진입 시 다시 now/remain을 '절대시간' 기준으로 계산하므로 드리프트 적음
        }
    }

    private async UniTask RunSkillLoop()
    {
        skillCts.CancelAndDispose();
        skillCts = new CancellationTokenSource();
        while (true)
        {
            long nowMs = GameTime.Instance.GetServerTimestampMs();           // ms 단위
            long remainMs = SkillEndTimeMs - nowMs;

            if (remainMs <= 0)
            {
                SkillRemainTimeSec.Value = 0;              // 초 단위 표시
                break;
            }

            // 초 단위로만 표시 (올림해서 사용자 체감과 맞춤: 1201ms -> 2초)
            int remainSec = (int)Math.Ceiling(remainMs / 1000.0);
            if (SkillRemainTimeSec.Value != remainSec)
                SkillRemainTimeSec.Value = remainSec;

            // 다음 "초 경계"까지 남은 시간만큼 대기 (경계 정렬)
            int waitMs = (int)(remainMs % 1000);
            if (waitMs == 0) waitMs = 1000;

            await UniTask.Delay(waitMs, cancellationToken: skillCts.Token);
            // 루프 재진입 시 다시 now/remain을 '절대시간' 기준으로 계산하므로 드리프트 적음
        }
    }
    private async UniTaskVoid StartSkillCooltime(long coolEndtimeMs)
    {
        CooldownEndTimeMs = coolEndtimeMs;
        await RunCooldownLoop();
    }
    public void ResetSkillCooltime()
    {
        CooldownEndTimeMs = -1;
        SkillEndTimeMs = -1;
        SkillStartTimeMs = -1;
        cooldownCts?.CancelAndDispose();
        skillCts?.CancelAndDispose();
        CooldownRemainTimeSec.Value = 0;              // 초 단위 표시
        SkillRemainTimeSec.Value = 0;
    }
    public async UniTask Castkill(long cooldurationMs)
    {
        long durationSec = Table.GetDurationSec(Level.Value);
        var currtime = GameTime.Instance.GetServerTimestampMs();
        var coolEndtimeMs = GameTime.Instance.GetServerTimestampMs() + cooldurationMs;
        SkillStartTimeMs = currtime;
        SkillEndTimeMs = currtime + durationSec * 1000;
        if (SkillEndTimeMs > coolEndtimeMs)
        {
            SkillEndTimeMs = coolEndtimeMs;
            Debug.LogError("SkillEndTimeMs = coolEndtimeMs");
        }
        StartSkillCooltime(coolEndtimeMs).Forget();
        await RunSkillLoop();
    }

    public void LevelUpSkill()
    {
        Level.Value++;
    }

    public void Complete()
    {
        if (UnlockViewed)
            return;
        UnlockViewed = true;
        Debug.Log("MessageDispatcher.Publish(EMessageType.UnlockSkill)");
        MessageDispatcher.Publish(EMessageType.UnlockSkill, Table.id);
    }

    public UnlockOwnerType GetUnlockOwnerType()
    {
        throw new NotImplementedException();
    }
}

#endregion

#region PlayerData
[Serializable]
public class PlayerDataDto
{
    public long UidSeed;
    public bool heartCheat;
    public long LastSavedTime;
    public bool IsReleaseAccount;
    public int TutorialStep;
    public int IsBgmEnabled;
    public int IsSfxEnabled;
    public int IsAtmosphereEnabled;

    public PlayerDataDto()
    {
        UidSeed = 0;
        IsBgmEnabled = 1;
        IsSfxEnabled = 1;
        IsAtmosphereEnabled = 1;
    }
}

public class PlayerData
{
    public long UidSeed;
    public long LastSavedTime { get; set; }
    public bool IsReleaseAccount { get; set; }
    public ReactiveProperty<int> TutorialStep { get; set; }
    public ReactiveProperty<int> IsBgmEnabled { get; set; }
    public ReactiveProperty<int> IsSfxEnabled { get; set; }
    public ReactiveProperty<int> IsAtmosphereEnabled { get; set; }


    public void InitData()
    {
        TutorialStep = new ReactiveProperty<int>();
        IsBgmEnabled = new ReactiveProperty<int>();
        IsSfxEnabled = new ReactiveProperty<int>();
        IsAtmosphereEnabled = new ReactiveProperty<int>();
    }
    public void ApplyDto(PlayerDataDto playerDataDto)
    {
        UidSeed = playerDataDto.UidSeed;
        LastSavedTime = playerDataDto.LastSavedTime;
        IsReleaseAccount = playerDataDto.IsReleaseAccount;
        TutorialStep.Value = playerDataDto.TutorialStep;
        IsBgmEnabled.Value = playerDataDto.IsBgmEnabled;
        IsAtmosphereEnabled.Value = playerDataDto.IsAtmosphereEnabled;
        IsSfxEnabled.Value = playerDataDto.IsSfxEnabled;
    }
    public long GenerateUid()
    {
        UidSeed++;
        if (UidSeed >= long.MaxValue)
        {
            Debug.LogError("UidSeed가 최대값에 도달했습니다. 1로 초기화합니다.");
            UidSeed = 1;
        }
        return UidSeed;
    }

    public PlayerDataDto ToDto()
    {
        return new PlayerDataDto
        {
            UidSeed = UidSeed,
            LastSavedTime = LastSavedTime,
            IsReleaseAccount = IsReleaseAccount,
            TutorialStep = TutorialStep.Value,
            IsBgmEnabled = IsBgmEnabled.Value,
            IsSfxEnabled = IsSfxEnabled.Value,
            IsAtmosphereEnabled = IsAtmosphereEnabled.Value,
        };
    }
}
#endregion

#region StoneData
[Serializable]
public class StoneDataDto
{
    public BigInteger Heart; // BigInteger 직렬화 대응
    public BigInteger GemPaid;
    public BigInteger GemFree;
    public BigInteger Pearl;
    public BigInteger TearStone;
    public long FishTicket;
    public int UsedFishTicket;
    public int EvoFishCountByTearStone;
    public long EvolutionBonus;
    public int StoneLevel = 1;
    public int BonusLevel = 0;
    public int AttendanceDay;   // 출석한 날. 출석 보상을 받으면 해당 Day를 저장. 최초 -1
    public long NextMidnightTimeMs;
    public long NextAttendanceTimeMs;
    public long RefreshAttendanceTimeMs;
    public int ShareRewardCount;

    public StoneDataDto()
    {
        Heart = ConfigTable.Instance.StartHeart;
        ShareRewardCount = ConfigTable.Instance.share_event_count;
        FishTicket = 0;
        UsedFishTicket = 0;
        EvoFishCountByTearStone = 0;
    }

}

public class StoneData : IDisposable
{
    public ReactiveProperty<BigInteger> Heart;
    public ReactiveProperty<BigInteger> GemPaid;
    public ReactiveProperty<BigInteger> GemFree;
    public ReactiveProperty<BigInteger> Pearl;
    public ReactiveProperty<BigInteger> TearStone;
    public ReactiveProperty<long> FishTicket;
    public ReactiveProperty<int> UsedFishTicket;
    public ReactiveProperty<int> EvoFishCountByTearStone;
    public ReadOnlyReactiveProperty<BigInteger> GemTotal;
    public ReactiveProperty<long> EvolutionBonus;
    public ReactiveProperty<int> StoneLevel;
    public ReadOnlyReactiveProperty<int> StoneStep;
    public ReactiveProperty<int> BonusLevel;
    public ReactiveProperty<long> DailyMissionResetRemainTimeSec;
    public ReactiveProperty<int> AttendanceDay;
    public long NextMidnightTimeMs { get; set; }
    public long NextAttendanceTimeMs { get; set; }
    public long RefreshAttendanceTimeMs { get; set; }
    public ReactiveProperty<int> ShareRewardCount;
    public ReactiveProperty<long> NextAttendanceRemainTimeSec;
    public ReactiveProperty<long> RefreshAttendanceRemainTimeSec;
    private CancellationTokenSource dailyMissionCts;
    private CancellationTokenSource nextAttendanceCts;
    private CancellationTokenSource refreshAttendanceCts;

    public void InitData()
    {
        StoneLevel = new ReactiveProperty<int>();
        BonusLevel = new ReactiveProperty<int>();
        EvolutionBonus = new ReactiveProperty<long>();
        Heart = new ReactiveProperty<BigInteger>();
        Pearl = new ReactiveProperty<BigInteger>(0);
        TearStone = new ReactiveProperty<BigInteger>(0);
        FishTicket = new ReactiveProperty<long>(0);
        UsedFishTicket = new ReactiveProperty<int>();
        EvoFishCountByTearStone = new ReactiveProperty<int>(0);
        GemPaid = new ReactiveProperty<BigInteger>(0);
        GemFree = new ReactiveProperty<BigInteger>(0);
        GemTotal = GemPaid.CombineLatest(GemFree, (paid, free) => paid + free).ToReadOnlyReactiveProperty();
        DailyMissionResetRemainTimeSec = new ReactiveProperty<long>();
        NextAttendanceRemainTimeSec = new ReactiveProperty<long>();
        RefreshAttendanceRemainTimeSec = new ReactiveProperty<long>();
        AttendanceDay = new ReactiveProperty<int>(-1);
        ShareRewardCount = new ReactiveProperty<int>();
        InitStoneStep();
        InitStoneLevelScore();
    }
    public void ApplyDto(StoneDataDto stoneDataDto)
    {
        Heart.Value = stoneDataDto.Heart;
        Pearl.Value = stoneDataDto.Pearl;
        TearStone.Value = stoneDataDto.TearStone;
        FishTicket.Value = stoneDataDto.FishTicket;
        UsedFishTicket.Value = stoneDataDto.UsedFishTicket;
        EvoFishCountByTearStone.Value = stoneDataDto.EvoFishCountByTearStone;
        StoneLevel.Value = stoneDataDto.StoneLevel;
        EvolutionBonus.Value = stoneDataDto.EvolutionBonus;
        BonusLevel.Value = stoneDataDto.BonusLevel;
        GemPaid.Value = stoneDataDto.GemPaid;
        GemFree.Value = stoneDataDto.GemFree;
        AttendanceDay.Value = stoneDataDto.AttendanceDay;
        NextMidnightTimeMs = stoneDataDto.NextMidnightTimeMs;
        NextAttendanceTimeMs = stoneDataDto.NextAttendanceTimeMs;
        RefreshAttendanceTimeMs = stoneDataDto.RefreshAttendanceTimeMs;
        ShareRewardCount.Value = stoneDataDto.ShareRewardCount;
    }
    public StoneDataDto ToDto()
    {
        return new StoneDataDto
        {
            Heart = Heart.Value,
            Pearl = Pearl.Value,
            TearStone = TearStone.Value,
            FishTicket = FishTicket.Value,
            UsedFishTicket = UsedFishTicket.Value,
            EvoFishCountByTearStone = EvoFishCountByTearStone.Value,
            StoneLevel = StoneLevel.Value,
            EvolutionBonus = EvolutionBonus.Value,
            BonusLevel = BonusLevel.Value,
            GemPaid = GemPaid.Value,
            GemFree = GemFree.Value,
            AttendanceDay = AttendanceDay.Value,
            NextMidnightTimeMs = NextMidnightTimeMs,
            NextAttendanceTimeMs = NextAttendanceTimeMs,
            RefreshAttendanceTimeMs = RefreshAttendanceTimeMs,
            ShareRewardCount = ShareRewardCount.Value
        };
    }
    private void InitStoneStep()
    {
        StoneStep = StoneLevel
                .Select(level =>
                {
                    if (level >= ConfigTable.Instance.StoneGrowUp_3)
                        return 2;

                    if (level >= ConfigTable.Instance.StoneGrowUp_2)
                        return 1;

                    return 0;
                })
                .ToReadOnlyReactiveProperty();
    }
    private void InitStoneLevelScore()
    {
        StoneLevel
        .DistinctUntilChanged()
        .Subscribe(async level =>
        {
#if UNITY_EDITOR
            return;
#endif
            var param = new SubmitGameCenterLeaderBoardScoreParams()
            {
#if RELEASE
                Score = UserDataManager.Instance.UserData.Stone.StoneLevel.Value.ToString()
#else
                Score = "1"
#endif
            };
            await AIT.SubmitGameCenterLeaderBoardScore(param);
        });
    }

    public AttendanceStatus GetAttendanceStatus(int day)
    {
        // 이미 받은 날 (현재까지 받은 day 이하)
        if (day <= AttendanceDay.Value)
        {
            return AttendanceStatus.Received;
        }

        // 오늘 받을 수 있는 후보 day (다음 날)
        if (day == AttendanceDay.Value + 1)
        {
            // 잠금 시간(다음 출석 가능 시간)이 아직 설정되지 않았거나,
            // 0 이하(바로 받을 수 있는 상태)라면 즉시 Receivable 처리
            if (NextAttendanceTimeMs <= 0)
            {
                return AttendanceStatus.Receivable;
            }

            long now = GameTime.Instance.GetServerTimestampMs();
            return now >= NextAttendanceTimeMs
                ? AttendanceStatus.Receivable
                : AttendanceStatus.UnReceivable;
        }

        // 그 외(미래의 day) → 아직 받을 수 없음
        return AttendanceStatus.UnReceivable;
    }

    public async UniTask StartDailyMissionResetTimer()
    {
        dailyMissionCts?.Cancel();
        dailyMissionCts?.Dispose();

        var cts = dailyMissionCts = new CancellationTokenSource();
        var token = cts.Token;

        long nowMs = GameTime.Instance.GetServerTimestampMs();

        // ✅ 의도: 초기값(0) 또는 과거면 시작 즉시 1회 발행
        if (nowMs >= NextMidnightTimeMs)
        {
            MessageDispatcher.Publish(EMessageType.DailyReset);
        }

        // ✅ 즉시 "미래" next로 재설정 (다음 자정)
        NextMidnightTimeMs = GameTime.Instance.CalcNextDayResetTimeKstMs();

        try
        {
            while (!token.IsCancellationRequested)
            {
                nowMs = GameTime.Instance.GetServerTimestampMs();
                long remainMs = NextMidnightTimeMs - nowMs;

                if (remainMs <= 0)
                {
                    DailyMissionResetRemainTimeSec.Value = 0;
                    MessageDispatcher.Publish(EMessageType.DailyReset);

                    // 다음 자정으로 갱신
                    NextMidnightTimeMs = GameTime.Instance.CalcNextDayResetTimeKstMs();

                    // ✅ 바로 continue 하면 nowMs/NextMidnightTimeMs 조합에 따라
                    // remainMs가 계속 0 이하로 계산될 여지가 있어요(특히 계산함수가 경계에서 흔들릴 때).
                    // 짧게라도 yield 해서 프레임/시간 진행을 보장.
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    continue;
                }

                // ✅ ceil 적용 (경계에서 2초씩 떨어지는 현상 방지)
                long remainSec = (remainMs + 999) / 1000;
                DailyMissionResetRemainTimeSec.Value = remainSec;

                int waitMs = (int)(remainMs % 1000);
                if (waitMs <= 0) waitMs = 1000;
                waitMs = Math.Clamp(waitMs, 5, 1000);

                await UniTask.Delay(waitMs, cancellationToken: token, delayTiming: PlayerLoopTiming.Update);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async UniTask StartNextAttendanceTimer()
    {
        // 기존 타이머 정리
        nextAttendanceCts?.Cancel();
        nextAttendanceCts?.Dispose();

        nextAttendanceCts = new CancellationTokenSource();
        var token = nextAttendanceCts.Token;

        // 타이머가 필요 없는 상태(0 이거나 과거 시간이면) 바로 정리
        if (NextAttendanceTimeMs <= 0)
        {
            NextAttendanceRemainTimeSec.Value = 0;
            return;
        }

        while (!token.IsCancellationRequested)
        {
            long nowMs = GameTime.Instance.GetServerTimestampMs();
            long remainMs = NextAttendanceTimeMs - nowMs;

            if (remainMs <= 0)
            {
                NextAttendanceRemainTimeSec.Value = 0;
                break;
            }

            long remainSec = (remainMs + 999) / 1000; // 올림
            NextAttendanceRemainTimeSec.Value = remainSec;

            int waitMs = (int)(remainMs % 1000);
            if (waitMs <= 0)
                waitMs = 1000;

            waitMs = Math.Clamp(waitMs, 5, 1000);

            try
            {
                await UniTask.Delay(
                    waitMs,
                    cancellationToken: token,
                    delayTiming: PlayerLoopTiming.Update
                );
            }
            catch (OperationCanceledException)
            {
                // Cancel 시 조용히 빠져나오기
                break;
            }
        }
    }

    public async UniTask StartRefreshAttendanceTimer()
    {
        // 이전 타이머 취소 및 정리
        refreshAttendanceCts?.Cancel();
        refreshAttendanceCts?.Dispose();

        refreshAttendanceCts = new CancellationTokenSource();
        var token = refreshAttendanceCts.Token;

        while (!token.IsCancellationRequested)
        {
            long nowMs = GameTime.Instance.GetServerTimestampMs();
            long remainMs = RefreshAttendanceTimeMs - nowMs;

            // 리셋 시점 도달 또는 지난 경우
            if (remainMs <= 0)
            {
                // 다음 달 리셋 시간 계산
                RefreshAttendanceTimeMs = GameTime.Instance.CalcNextMonthResetTimeKstMs();
                NextAttendanceTimeMs = 0;
                AttendanceDay.Value = -1;

                // 다음 타겟 시간 기준으로 다시 루프 진행
                // (재귀 호출 없이 while 루프 안에서 계속 동작)
                continue;
            }

            // 남은 초(올림)
            long remainSec = (remainMs + 999) / 1000;
            RefreshAttendanceRemainTimeSec.Value = remainSec;

            // 다음 딜레이(ms) 계산
            int waitMs = (int)(remainMs % 1000);
            if (waitMs <= 0)
                waitMs = 1000;

            waitMs = Math.Clamp(waitMs, 5, 1000);

            try
            {
                await UniTask.Delay(
                    waitMs,
                    cancellationToken: token,
                    delayTiming: PlayerLoopTiming.Update
                );
            }
            catch (OperationCanceledException)
            {
                // Cancel로 끝낼 때 조용히 종료
                break;
            }
        }
    }

    public void Dispose()
    {
        dailyMissionCts?.Cancel();
        dailyMissionCts?.Dispose();
        dailyMissionCts = null;
    }
}
#endregion

#region UserData
[Serializable]
public sealed class UserDataDto
{
    public int Version = 1;                    // 스키마 마이그레이션 대비
    public PlayerDataDto Player = new PlayerDataDto();
    public StoneDataDto Stone = new StoneDataDto();
    public Dictionary<long, CoralDataDto> Corals = new Dictionary<long, CoralDataDto>();
    public Dictionary<long, FishDataDto> Fishes;
    public Dictionary<long, SkillDataDto> Skills;
    public Dictionary<long, ADDataDto> Ads;
    public Dictionary<long, ArtifactDataDto> Artifacts;
    public Dictionary<long, CollectionDataDto> collections;
    public Dictionary<long, AchievementDataDto> achievements;
    public Dictionary<string, StatisticDataDto> statistics;
    public Dictionary<string, DailyStatisticDataDto> dailyStatistics;
    public Dictionary<long, DailyMissionDataDto> dailyMissions;
    public GuildeMissionDataDto GuildeMissionData;
}

public sealed class UserData
{
    public Dictionary<long, SkillData> Skills { get; private set; }
    public SkillData GetSkill(long id) => Skills.TryGetValue(id, out var skillData) ? skillData : null;
    public Dictionary<long, ArtifactData> Artifacts;
    public ArtifactData GetArtifact(long id) => Artifacts.TryGetValue(id, out var artifactData) ? artifactData : null;
    public Dictionary<long, CoralData> Corals;
    public CoralData GetCoral(long id) => Corals.TryGetValue(id, out var coralData) ? coralData : null;
    public Dictionary<long, FishData> Fishes;
    public FishData GetFish(long id) => Fishes.TryGetValue(id, out var fishData) ? fishData : null;
    public Dictionary<long, CollectionData> Collections;
    public CollectionData GetCollection(long id) => Collections.TryGetValue(id, out var data) ? data : null;
    public Dictionary<long, AchievementData> Achievements;
    public AchievementData GetAchievement(long id) => Achievements.TryGetValue(id, out var data) ? data : null;
    public GuildeMissionData GuideMissionData { get; set; }
    public Dictionary<string, StatisticData> Statistics;
    public StatisticData GetStatistic(string id) => Statistics.TryGetValue(id, out var data) ? data : null;
    public StatisticData GetStatistic(UNLOCKTYPE type, long target) => GetStatistic($"{(int)type}_{target}");
    public Dictionary<string, DailyStatisticData> DailyStatistics;
    public DailyStatisticData GetDailyStatistic(string id) => DailyStatistics.TryGetValue(id, out var data) ? data : null;
    public Dictionary<long, DailyMissionData> DailyMissions;
    public DailyMissionData GetDailyMission(long id) => DailyMissions.TryGetValue(id, out var data) ? data : null;
    public ReadOnlyReactiveProperty<bool> DailyMissionRewardable { get; private set; }
    public ReadOnlyReactiveProperty<bool> CollectionRewardable { get; private set; }
    public ReadOnlyReactiveProperty<bool> AchievementRewardable { get; private set; }

    public Dictionary<long, ADData> Ads;
    public ADData GetAd(long id) => Ads.TryGetValue(id, out var data) ? data : null;

    public ReadOnlyReactiveProperty<Dictionary<int, int>> FishKindCounts { get; private set; }
    public ReadOnlyReactiveProperty<Dictionary<int, int>> FishKindCountInWorld { get; private set; }
    public ReadOnlyReactiveProperty<int> NormalFishSpeciesCount { get; private set; }
    public ReadOnlyReactiveProperty<int> HiddenFishSpeciesCount { get; private set; }
    public ReadOnlyReactiveProperty<int> EvolFishSpeciesCount { get; private set; }
    public ReadOnlyReactiveProperty<int> EvnetFishSpeciesCount { get; private set; }
    public ReadOnlyReactiveProperty<int> NormalFishCount { get; private set; }
    public ReadOnlyReactiveProperty<int> HiddenFishCount { get; private set; }
    public ReadOnlyReactiveProperty<int> EvolFishCount { get; private set; }
    public ReadOnlyReactiveProperty<int> EvnetFishCount { get; private set; }
    public ReadOnlyReactiveProperty<int> FishTotalCount { get; private set; }
    public ReadOnlyReactiveProperty<int> FishCountInWorld { get; private set; }
    public ReadOnlyReactiveProperty<int> WorldFishLimit { get; private set; }
    public ReadOnlyReactiveProperty<BigInteger> NextFishCost { get; private set; }
    public int FishCreateCostCount => NormalFishCount.Value + HiddenFishCount.Value + EvolFishCount.Value - Stone.UsedFishTicket.Value;

    private CompositeDisposable disposables;
    private CompositeDisposable fishDisposables;

    public PlayerData Player = new();
    public StoneData Stone = new();

    public UserData()
    {
        Debug.Log("UserData");
    }

    public long GetStatisticValue(UNLOCKTYPE type, long target)
    {
        var statisticData = GetStatistic($"{(int)type}_{target}");
        if (statisticData != null)
            return statisticData.Value;
        return 0;
    }
    public long GetDailyStatisticValue(UNLOCKTYPE type, long target)
    {
        var statisticData = GetDailyStatistic($"{(int)type}_{target}");
        if (statisticData != null)
            return statisticData.Value;
        return 0;
    }

    public void AddItem(ItemParam itemParam)
    {
        switch (itemParam.Type)
        {
            case ITEMTYPE.HEART:
                {
                    Stone.Heart.Value += itemParam.Count;
                }
                break;
            case ITEMTYPE.GEM:
                {
                    Stone.GemPaid.Value += itemParam.Count;
                }
                break;
            case ITEMTYPE.GEM_FREE:
                {
                    Stone.GemFree.Value += itemParam.Count;
                }
                break;
            case ITEMTYPE.PEARL:
                {
                    Stone.Pearl.Value += itemParam.Count;
                }
                break;
            case ITEMTYPE.TEARSTONE:
                {
                    Stone.TearStone.Value += itemParam.Count;
                }
                break;
            case ITEMTYPE.FISHTICKET:
                {
                    Stone.FishTicket.Value += (long)itemParam.Count;
                }
                break;
            case ITEMTYPE.FISH:
                {
                    AddFish(itemParam.ItemId, (int)itemParam.Count);
                }
                break;
        }
    }
    public void RemoveItem(ItemParam itemParam)
    {
        BigInteger amount = itemParam.Count;
        long amountLong = amount > long.MaxValue ? long.MaxValue : (long)amount;
        switch (itemParam.Type)
        {
            case ITEMTYPE.HEART:
                {
                    AddStatistic(UNLOCKTYPE.USE_HEART, 0, amountLong);
                    Stone.Heart.Value -= amount;
                    if (Stone.Heart.Value < 0)
                        Stone.Heart.Value = 0;
                }
                break;
            case ITEMTYPE.GEM:
                {
                    AddStatistic(UNLOCKTYPE.USE_GEM, 0, amountLong);
                    if (Stone.GemFree.Value >= amount)
                    {
                        Stone.GemFree.Value -= amount;
                    }
                    else
                    {
                        BigInteger diff = amount - Stone.GemFree.Value;
                        Stone.GemFree.Value = 0;

                        Stone.GemPaid.Value -= diff;
                        if (Stone.GemPaid.Value < 0)
                            Stone.GemPaid.Value = 0;
                    }
                }
                break;
            case ITEMTYPE.GEM_FREE:
                {
                    Debug.LogError("GEM_FREE Not Allowed");
                }
                break;
            case ITEMTYPE.PEARL:
                {
                    AddStatistic(UNLOCKTYPE.USE_PEARL, 0, amountLong);
                    Stone.Pearl.Value -= amount;
                    if (Stone.Pearl.Value < 0)
                        Stone.Pearl.Value = 0;
                }
                break;
            case ITEMTYPE.TEARSTONE:
                {
                    Stone.TearStone.Value -= amount;
                    if (Stone.TearStone.Value < 0)
                        Stone.TearStone.Value = 0;
                }
                break;
            case ITEMTYPE.FISHTICKET:
                {
                    Stone.FishTicket.Value -= (long)amount;
                    if (Stone.FishTicket.Value < 0)
                        Stone.FishTicket.Value = 0;
                }
                break;
            case ITEMTYPE.FISH:
                {
                    int itemAmt = (int)itemParam.Count;
                    RemoveFish(itemParam.ItemId, itemAmt);
                }
                break;
        }
    }
    public BigInteger GetItemCount(ITEMTYPE Type, int itemId)
    {
        switch (Type)
        {
            case ITEMTYPE.HEART:
                {
                    return Stone.Heart.Value;
                }
            case ITEMTYPE.GEM:
            case ITEMTYPE.GEM_FREE:
                {
                    return Stone.GemTotal.Value;
                }
            case ITEMTYPE.PEARL:
                {
                    return Stone.Pearl.Value;
                }
            case ITEMTYPE.TEARSTONE:
                {
                    return Stone.TearStone.Value;
                }
            case ITEMTYPE.FISHTICKET:
                {
                    return Stone.FishTicket.Value;
                }
            case ITEMTYPE.FISH:
                {
                    return GetFish(itemId).FishTotalCount.Value;
                }
        }
        return 0;
    }
    public bool HasEnoughItem(ItemParam param)
    {
        BigInteger itemCount = GetItemCount(param.Type, param.ItemId);
        return itemCount >= param.Count;
    }

    private FishData AddFish(int tid, int count)
    {
        if (!Fishes.TryGetValue(tid, out var fishData))
        {
            Debug.LogWarning($"Fish tid {tid} Not found");
            return default;
        }
        fishData.AddFish(count);

        return fishData;
    }
    private void RemoveFish(int id, int count)
    {
        var fishData = GetFish(id);
        if (fishData != null)
        {
            fishData.RemoveFish(count);
        }
    }
    public List<long> GetRandomFishesInTank(int count)
    {
        // 탱크 안(아직 월드에 안 나간) 개체들 목록 생성
        List<long> availableFishPool = new List<long>();

        foreach (var kv in Fishes)
        {
            var fishId = kv.Key;
            var fishData = kv.Value;

            // 아직 월드에 배치되지 않은 마리 수
            int remainCount = fishData.FishTotalCount.Value - fishData.FishCountInWorld.Value;
            if (remainCount > 0)
            {
                // 한 종에서 remainCount 만큼 리스트에 추가
                for (int i = 0; i < remainCount; i++)
                {
                    availableFishPool.Add(fishId);
                }
            }
        }

        // 랜덤 선택
        if (availableFishPool.Count == 0)
            return new List<long>();

        if (availableFishPool.Count <= count)
            return new List<long>(availableFishPool);

        var random = new System.Random();
        return availableFishPool
            .OrderBy(_ => random.Next())
            .Take(count)
            .ToList();
    }

    public void PlaceFishesInWorld(int tid)
    {
        if (!Fishes.TryGetValue(tid, out var fishData))
        {
            Debug.LogWarning($"Fish tid {tid} Not found");
        }
        fishData.FishCountInWorld.Value++;
    }

    public long PickRandomFishInWorld(long exceptFishID)
    {
        var fishList = Fishes.Where(fish => fish.Value.FishCountInWorld.Value > 0 && fish.Key != exceptFishID).ToList();
        if (fishList.Count == 0)
            return -1;
        var randNum = UnityEngine.Random.Range(0, fishList.Count);
        return fishList[randNum].Key;
    }

    public bool RetrieveFishesToTank(int tid)
    {
        if (!Fishes.TryGetValue(tid, out var fishData))
        {
            Debug.LogWarning($"Fish tid {tid} Not found");
        }
        if (fishData.FishCountInWorld.Value <= 0)
        {
            return false;
        }
        else
        {
            fishData.FishCountInWorld.Value--;
            return true;
        }
    }
    public void SetPlayTimeStatistic(UNLOCKTYPE type, long target, long playTime)
    {
        var maxPlayTimeMinute = GetStatisticValue(UNLOCKTYPE.COUNT_PLAY_TIME, 0);
        if (maxPlayTimeMinute < playTime)
        {
            SetNormalStatistic(type, target, playTime);
            SetGuideMissionStatistic(type, (int)target, playTime);
        }
        var dailyPlayTimeMinute = GetDailyStatisticValue(UNLOCKTYPE.COUNT_PLAY_TIME, 0);
        if (dailyPlayTimeMinute < playTime)
        {
            SetDailyStatistic(type, target, playTime);
        }
    }

    public void SetStatistic(UNLOCKTYPE type, long target, long value)
    {
        SetNormalStatistic(type, target, value);
        SetDailyStatistic(type, target, value);
        SetGuideMissionStatistic(type, (int)target, value);
    }
    public void AddStatistic(UNLOCKTYPE type, long target, long delta)
    {
        AddNormalStatistic(type, target, delta);
        AddDailyStatistic(type, target, delta);
        AddGuideMissionStatistic(type, (int)target, delta);
    }
    public void SetNormalStatistic(UNLOCKTYPE type, long target, long value)
    {
        string id = $"{(int)type}_{target}";
        var stat = GetStatistic(id);
        if (stat == null)
        {
            AddNormalStatistic(type, target, value);
        }
        else
        {
            AddNormalStatistic(type, target, value - stat.Value);
        }
    }
    private void AddNormalStatistic(UNLOCKTYPE type, long target, long delta)
    {
        string id = $"{(int)type}_{target}";
        if (!Statistics.TryGetValue(id, out var stat))
        {
            stat = new StatisticData(id, type, target);
            Statistics[id] = stat;
        }

        long newValue = stat.Value + delta;
        if (delta > 0 && newValue < stat.Value)
            newValue = long.MaxValue; // overflow → 상한 고정
        else if (delta < 0 && newValue > stat.Value)
            newValue = 0; // underflow → 0 고정
        stat.Value = Math.Max(0, newValue);

        var targetMap = UnlockController.Instance.GetOrCreateTargetMap(type);
        if (targetMap != null && targetMap.TryGetValue(target, out var unlockSet))
        {
            var unlockList = unlockSet.ToList();
            for (int i = 0; i < unlockList.Count; i++)
            {
                unlockList[i].SetAbsoluteProgress(stat.Value);       //(type,target)에 매핑된 모든 targetValue에 절대 진행치 반영
                // unlockList[i].AddProgress(delta);               // 증가치 방식이 필요하면: data.AddProgress(delta);
            }
        }
    }
    public void SetDailyStatistic(UNLOCKTYPE type, long target, long value)
    {
        string id = $"{(int)type}_{target}";
        var stat = GetDailyStatistic(id);
        if (stat == null)
        {
            AddDailyStatistic(type, target, value);
        }
        else
        {
            AddDailyStatistic(type, target, value - stat.Value);
        }
    }
    private void AddDailyStatistic(UNLOCKTYPE type, long target, long delta)
    {
        string id = $"{(int)type}_{target}";
        if (!DailyStatistics.TryGetValue(id, out var stat))
        {
            stat = new DailyStatisticData(id, type, target);
            DailyStatistics[id] = stat;
        }

        long newValue = stat.Value + delta;
        if (delta > 0 && newValue < stat.Value)
            newValue = long.MaxValue; // overflow → 상한 고정
        else if (delta < 0 && newValue > stat.Value)
            newValue = 0; // underflow → 0 고정
        stat.Value = Math.Max(0, newValue);

        var targetMap = DailyUnlockController.Instance.GetOrCreateTargetMap(type);
        if (targetMap != null && targetMap.TryGetValue(target, out var unlockSet))
        {
            foreach (var data in unlockSet)
            {
                data.SetAbsoluteProgress(stat.Value);   // (type,target)에 매핑된 모든 targetValue에 절대 진행치 반영
                // data.AddProgress(delta);        // 증가치 방식이 필요하면: kv.Value.AddProgress(delta);
            }
        }
    }
    public void ResetDailyStatistics()
    {
        foreach (var stat in DailyStatistics.Values)
        {
            var targetMap = DailyUnlockController.Instance.GetOrCreateTargetMap(stat.UnlockType);
            if (targetMap != null && targetMap.TryGetValue(stat.Target, out var unlockSet))
            {
                foreach (var data in unlockSet)
                {
                    data.Reset();
                }
            }
            stat.Value = 0;
        }
    }
    private void SetGuideMissionStatistic(UNLOCKTYPE type, int target, long value)
    {
        var delta = value - GuideMissionData.CurrentValue.Value;
        AddGuideMissionStatistic(type, target, delta);
    }
    private void AddGuideMissionStatistic(UNLOCKTYPE type, int target, long delta)
    {
        if (GuideMissionData.Table == null)
            return;

        if (type != (UNLOCKTYPE)GuideMissionData.Table.missiontype)
        {
            return;
        }
        if (target != GuideMissionData.Table.missiontarget)
        {
            return;
        }
        GuideMissionData.AddProgress(delta);
    }

    public bool RemoveCoral(long coralId) => Corals.Remove(coralId);

    public void InitData()
    {
        disposables?.Clear();
        disposables?.Dispose();
        UnlockController.Instance.ClearAll();
        DailyUnlockController.Instance.ClearAll();
        disposables = new CompositeDisposable();
        Player.InitData();
        InitFish();
        InitStone();
        InitAd();
        InitSkills();
        InitArtifacts();
        InitCollections();
        InitAchievement();
        InitGuideMission();
        InitStatistic();
        InitDailyStatistic();
        InitDailyMission();
        InitCorals();
    }

    public void InitWithData()
    {
        // 0) 기존 fishDisposables 정리
        if (fishDisposables != null)
        {
            fishDisposables.Dispose();            // 내부 구독 모두 Dispose
            disposables.Remove(fishDisposables);  // 상위 Composite에서 제거 (있다면)
        }
        fishDisposables = new CompositeDisposable();
        disposables.Add(fishDisposables);

        // 여러 번 열거 방지용 캐싱
        var coralList = Corals.Values.ToArray();
        var fishList = Fishes.Values.ToArray();

        // 1) 산호 기반 WorldFishLimit
        //    - Corals가 없으면 항상 0 더해주는 스트림으로 대체
        IObservable<int> addFishSumStream =
            coralList.Length > 0
                ? coralList
                    .Select(c => c.AddFishCount.StartWith(c.AddFishCount.Value))
                    .CombineLatest()
                    .Select(xs => xs.Sum())
                : Observable.Return(0);

        WorldFishLimit = addFishSumStream
            .Select(sum => ConfigTable.Instance.FishTankSetDefaultCount + sum)
            .ToReadOnlyReactiveProperty()
            .AddTo(fishDisposables);

        // 2) 전체 물고기 수 합계
        //    - fishList가 비어 있어도 안전하게 0으로 시작
        FishTotalCount =
            (fishList.Length > 0
                ? fishList
                    .Select(f => f.FishTotalCount)
                    .CombineLatest()
                    .Select(xs => xs.Sum())
                : Observable.Return(0))
            .ToReadOnlyReactiveProperty()
            .AddTo(fishDisposables);

        // 3) 월드 내 전체 물고기 수
        FishCountInWorld =
            (fishList.Length > 0
                ? fishList
                    .Select(f => f.FishCountInWorld)
                    .CombineLatest()
                    .Select(xs => xs.Sum())
                : Observable.Return(0))
            .ToReadOnlyReactiveProperty()
            .AddTo(fishDisposables);

        // 4) 그룹별 총 물고기 수 (인벤토리 기준)
        FishKindCounts =
            (fishList.Length > 0
                ? fishList
                    .Select(f =>
                        f.FishTotalCount.Select(count => new
                        {
                            GroupId = f.Table.fishgroupid,
                            Count = count
                        }))
                    .CombineLatest()
                    .Select(list =>
                    {
                        var dict = new Dictionary<int, int>();
                        foreach (var e in list)
                        {
                            if (!dict.TryGetValue(e.GroupId, out var cur))
                                cur = 0;
                            dict[e.GroupId] = cur + e.Count;
                        }
                        return dict;
                    })
                : Observable.Return(new Dictionary<int, int>()))
            .ToReadOnlyReactiveProperty()
            .AddTo(fishDisposables);

        // 4-1) 그룹별 총 물고기 수 변화 → 통계 갱신 (HAVE_FISH_KIND)
        var prevKindCount = new Dictionary<int, int>();
        FishKindCounts
            .Subscribe(current =>
            {
                foreach (var kv in current)
                {
                    if (!prevKindCount.TryGetValue(kv.Key, out var prevValue) ||
                        prevValue != kv.Value)
                    {
                        SetStatistic(UNLOCKTYPE.HAVE_FISH_KIND, kv.Key, kv.Value);
                    }
                }

                // prev 동기화
                prevKindCount.Clear();
                foreach (var kv in current)
                {
                    prevKindCount[kv.Key] = kv.Value;
                }
            })
            .AddTo(fishDisposables);

        // 5) 그룹별 월드 내 물고기 수
        FishKindCountInWorld =
            (fishList.Length > 0
                ? fishList
                    .Select(f =>
                        f.FishCountInWorld.Select(count => new
                        {
                            GroupId = f.Table.fishgroupid,
                            Count = count
                        }))
                    .CombineLatest()
                    .Select(list =>
                    {
                        var dict = new Dictionary<int, int>();
                        foreach (var e in list)
                        {
                            if (!dict.TryGetValue(e.GroupId, out var cur))
                                cur = 0;
                            dict[e.GroupId] = cur + e.Count;
                        }
                        return dict;
                    })
                : Observable.Return(new Dictionary<int, int>()))
            .ToReadOnlyReactiveProperty()
            .AddTo(fishDisposables);

        // 5-1) 그룹별 월드 내 물고기 수 변화 → 통계 갱신 (SET_FISH_KIND)
        var prevKindCountInWorld = new Dictionary<int, int>();
        FishKindCountInWorld
            .Subscribe(current =>
            {
                // 바뀐 것만 통계 갱신
                foreach (var kv in current)
                {
                    if (!prevKindCountInWorld.TryGetValue(kv.Key, out var prevVal) ||
                        prevVal != kv.Value)
                    {
                        SetStatistic(UNLOCKTYPE.SET_FISH_KIND, kv.Key, kv.Value);
                    }
                }

                // prev 동기화
                prevKindCountInWorld.Clear();
                foreach (var kv in current)
                {
                    prevKindCountInWorld[kv.Key] = kv.Value;
                }
            })
            .AddTo(fishDisposables);

        List<FishData> normalTypeFishList = fishList.Where(fish => fish.Table.fishtype == FISHTYPE.NORMAL).ToList();
        List<FishData> hiddenTypeFishList = fishList.Where(fish => fish.Table.fishtype == FISHTYPE.HIDDEN).ToList();
        List<FishData> evolTypeFishList = fishList.Where(fish => fish.Table.fishtype == FISHTYPE.EVOLUTION).ToList();
        List<FishData> eventTypeFishList = fishList.Where(fish => fish.Table.fishtype == FISHTYPE.EVENT).ToList();

        ReadOnlyReactiveProperty<int> CreateSpeciesCount(List<FishData> fishes, CompositeDisposable cd)
        {
            if (fishes == null || fishes.Count == 0)
            {
                return Observable
                    .Return(0)
                    .ToReadOnlyReactiveProperty()
                    .AddTo(cd);
            }

            return fishes
                .Select(fish => fish.FishTotalCount)              // IObservable<int> 들
                .CombineLatest()                                  // IReadOnlyList<int> counts
                .Select(counts => counts.Count(c => c > 0))       // 0마리 초과인 종 수
                .ToReadOnlyReactiveProperty()
                .AddTo(cd);
        }

        NormalFishSpeciesCount = CreateSpeciesCount(normalTypeFishList, fishDisposables);
        HiddenFishSpeciesCount = CreateSpeciesCount(hiddenTypeFishList, fishDisposables);
        EvolFishSpeciesCount = CreateSpeciesCount(evolTypeFishList, fishDisposables);
        EvnetFishSpeciesCount = CreateSpeciesCount(eventTypeFishList, fishDisposables);

        NormalFishCount = CreateFishCountProperty(normalTypeFishList, fishDisposables);
        HiddenFishCount = CreateFishCountProperty(hiddenTypeFishList, fishDisposables);
        EvolFishCount = CreateFishCountProperty(evolTypeFishList, fishDisposables);
        EvnetFishCount = CreateFishCountProperty(eventTypeFishList, fishDisposables);

        Observable.CombineLatest(NormalFishSpeciesCount, HiddenFishSpeciesCount, (normal, hidden) => normal + hidden)
        .DistinctUntilChanged()
        .Subscribe(total =>
        {
            SetStatistic(UNLOCKTYPE.HAVE_FISH_NORMAL_TYPE, 0, total);
        }).AddTo(fishDisposables);

        HiddenFishSpeciesCount
            .DistinctUntilChanged()
            .Subscribe(count =>
            {
                SetStatistic(UNLOCKTYPE.HAVE_FISH_HIDDEN_TYPE, 0, count);
            })
            .AddTo(fishDisposables);

        EvolFishSpeciesCount
            .DistinctUntilChanged()
            .Subscribe(count =>
            {
                SetStatistic(UNLOCKTYPE.HAVE_FISH_EVOL_TYPE, 0, count);
            })
            .AddTo(fishDisposables);

        // 7) 다음 물고기 가격 (NextFishCost)
        NextFishCost = Observable
        .CombineLatest(
            NormalFishCount.DistinctUntilChanged(),
            HiddenFishCount.DistinctUntilChanged(),
            Stone.UsedFishTicket.DistinctUntilChanged(),
            Stone.EvoFishCountByTearStone.DistinctUntilChanged(),
            (normalFishCount, hiddenFishCount, usedTicket, evoFishCount) =>
            {
                var cost = CalcNextFishCost();
                Debug.Log($"NormalFish {normalFishCount}, HiddenFish {hiddenFishCount}, UsedTicket {usedTicket}, EvoFish {evoFishCount} => NextCost {cost}");
                return cost;
            }
        )
        // ✅ 입력은 바뀌었는데 결과 cost는 같을 때 UI/구독 낭비 방지
        .DistinctUntilChanged()
        .ToReadOnlyReactiveProperty(
            CalcNextFishCost()
        )
        .AddTo(fishDisposables);
        BindCoralHeartPerSec();

        GuideMissionData.GenerateMissionData(GuideMissionData.Tid.Value);
    }

    private ReadOnlyReactiveProperty<int> CreateFishCountProperty(
    IList<FishData> fishList,
    CompositeDisposable disposables)
    {
        if (fishList == null || fishList.Count == 0)
        {
            // 리스트가 비어 있으면 항상 0
            return new ReadOnlyReactiveProperty<int>(Observable.Return(0)).AddTo(disposables);
        }

        return fishList
            .Select(fish => fish.FishTotalCount)
            .CombineLatest()
            .Select(counts => counts.Sum())
            .ToReadOnlyReactiveProperty(0)
            .AddTo(disposables);
    }

    private BigInteger CalcNextFishCost()
    {
        // 보유(또는 누적 기반 계산값) = 일반 + 히든 - 티켓사용 - 진화소모
        var effectiveHave = FishCreateCostCount;

        // ✅ 음수 방지 (타이밍/초기값/리셋 시점에서 흔히 터짐)
        if (effectiveHave < 0) 
            effectiveHave = 0;

        return FishCostCalc.GetNextCostFromHaveCount(effectiveHave);
    }
    public void Dispose()
    {
        foreach (var item in Skills)
        {
            item.Value.Dispose();
        }
        foreach (var item in Ads)
        {
            item.Value.Dispose();
        }
        GuideMissionData.Dispose();
        Stone.Dispose();
        Skills.Clear();
        disposables?.Clear();
        disposables?.Dispose();
    }

    private void InitStone()
    {
        Stone = new StoneData();
        Stone.InitData();
    }

    private void InitAd()
    {
        Ads = new Dictionary<long, ADData>();
        foreach (var item in DataManager.Instance.AdArray)
        {
            Ads[item.id] = new ADData(item.id);
        }
    }

    private void InitFish()
    {
        Fishes = new Dictionary<long, FishData>();
        foreach (var item in DataManager.Instance.FishinfoArray)
        {
            Fishes[item.id] = new FishData(item.id);
        }
    }
    private void InitCorals()
    {
        Corals = new Dictionary<long, CoralData>();
        foreach (var item in DataManager.Instance.CoralArray)
        {
            Corals[item.id] = new CoralData(item.id);
        }
    }

    private void BindCoralHeartPerSec()
    {
        foreach (var item in Corals)
        {
            item.Value.BindHearPerSec();
        }
    }
    private void InitSkills()
    {
        Skills = new Dictionary<long, SkillData>();
        foreach (var item in DataManager.Instance.SkillArray)
        {
            Skills[item.id] = new SkillData(item.id);
        }
    }
    private void InitArtifacts()
    {
        Artifacts = new Dictionary<long, ArtifactData>();
        foreach (var item in DataManager.Instance.ArtifactArray)
        {
            Artifacts[item.id] = new ArtifactData(item.id);
        }
    }
    private void InitCollections()
    {
        Collections = new Dictionary<long, CollectionData>();
        foreach (var item in DataManager.Instance.CollectionArray)
        {
            Collections[item.id] = new CollectionData(item.id);
        }
        CollectionRewardable = Observable.CombineLatest(Collections.Select(item => item.Value.IsRewardable))
        .Select(list => list.Any(item => item)).ToReadOnlyReactiveProperty();
    }

    private void InitAchievement()
    {
        Achievements = new Dictionary<long, AchievementData>();
        foreach (var item in DataManager.Instance.AchievementArray)
        {
            Achievements[item.id] = new AchievementData(item.id);
        }
        AchievementRewardable = Observable.CombineLatest(Achievements.Select(item => item.Value.IsRewadable))
        .Select(list => list.Any(item => item)).ToReadOnlyReactiveProperty();
    }
    private void InitGuideMission()
    {
        var guideMissionInfo = DataManager.Instance.GetGuideMissionData(1);
        GuideMissionData = new GuildeMissionData(guideMissionInfo.id);
    }
    private void InitStatistic()
    {
        Statistics = new Dictionary<string, StatisticData>();
    }
    private void InitDailyStatistic()
    {
        DailyStatistics = new Dictionary<string, DailyStatisticData>();
    }
    private void InitDailyMission()
    {
        DailyMissions = new Dictionary<long, DailyMissionData>();
        foreach (var item in DataManager.Instance.DailymissionArray)
        {
            DailyMissions[item.id] = new DailyMissionData(item.id);
        }
        DailyMissionRewardable = Observable.CombineLatest(DailyMissions.Select(item => item.Value.IsRewardable))
        .Select(list => list.Any(item => item)).ToReadOnlyReactiveProperty();
    }
    public void ResetDailyMission()
    {
        foreach (var mission in DailyMissions.Values)
        {
            mission.IsRewardReceived.Value = false;
        }
    }
}
#endregion