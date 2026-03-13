
using System;
using System.Collections.Generic;
using System.Linq;

public static class UserDataMapper
{
    public static Dictionary<TKey, TDto> MapDict<TKey, TVal, TDto>(
        IReadOnlyDictionary<TKey, TVal> src,
        Func<TVal, TDto> mapper)
    {
        var dst = new Dictionary<TKey, TDto>(src.Count);
        foreach (var kv in src) dst[kv.Key] = mapper(kv.Value);
        return dst;
    }

    public static List<TDto> MapList<TSrc, TDto>(IReadOnlyList<TSrc> src, Func<TSrc, TDto> mapper)
    {
        var list = new List<TDto>(src.Count);
        for (int i = 0; i < src.Count; i++) list.Add(mapper(src[i]));
        return list;
    }
    public static UserDataDto ToDto(this UserData runtime)
    {
        var dto = new UserDataDto
        {
            Version = 1,
            Stone = runtime.Stone.ToDto(),
            Player = runtime.Player.ToDto(),
        };
        foreach (var kv in runtime.Corals)
        {
            dto.Corals ??= new Dictionary<long, CoralDataDto>();
            dto.Corals[kv.Key] = kv.Value.ToDto();
        }
        foreach (var kv in runtime.Fishes)
        {
            dto.Fishes ??= new Dictionary<long, FishDataDto>();
            dto.Fishes[kv.Key] = kv.Value.ToDto();
        }
        foreach (var kv in runtime.Skills)
        {
            dto.Skills ??= new Dictionary<long, SkillDataDto>();
            dto.Skills[kv.Key] = kv.Value.ToDto();
        }
        foreach (var kv in runtime.Artifacts)
        {
            dto.Artifacts ??= new Dictionary<long, ArtifactDataDto>();
            dto.Artifacts[kv.Key] = kv.Value.ToDto();
        }
        foreach (var kv in runtime.Collections)
        {
            dto.collections ??= new Dictionary<long, CollectionDataDto>();
            dto.collections[kv.Key] = kv.Value.ToDto();
        }
        foreach (var kv in runtime.Achievements)
        {
            dto.achievements ??= new Dictionary<long, AchievementDataDto>();
            dto.achievements[kv.Key] = kv.Value.ToDto();
        }
        if(runtime.GuideMissionData != null)
        {
            dto.GuildeMissionData ??= new GuildeMissionDataDto();
            dto.GuildeMissionData = runtime.GuideMissionData.ToDto();
        }
        foreach (var kv in runtime.Statistics)
        {
            dto.statistics ??= new Dictionary<string, StatisticDataDto>();
            dto.statistics[kv.Key] = kv.Value.ToDto();
        }
        foreach (var kv in runtime.DailyStatistics)
        {
            dto.dailyStatistics ??= new Dictionary<string, DailyStatisticDataDto>();
            dto.dailyStatistics[kv.Key] = kv.Value.ToDto();
        }
        foreach (var kv in runtime.DailyMissions)
        {
            dto.dailyMissions ??= new Dictionary<long, DailyMissionDataDto>();
            dto.dailyMissions[kv.Key] = kv.Value.ToDto();
        }
        foreach (var kv in runtime.Ads)
        {
            dto.Ads ??= new Dictionary<long, ADDataDto>();
            dto.Ads[kv.Key] = kv.Value.ToDto();
        }
        return dto;
    }
    public static UserData FromDto(this UserDataDto dto)
    {
        var runtime = new UserData();
        runtime.InitData();
        runtime.Player.ApplyDto(dto.Player);
        runtime.Stone.ApplyDto(dto.Stone);

        if (dto.Fishes != null)
        {
            foreach (var kv in dto.Fishes)
                runtime.Fishes.GetOrLog(kv.Key).ApplyDto(kv.Value);
        }
        if (dto.Corals != null)
        {
            foreach (var kv in dto.Corals)
                runtime.Corals.GetOrLog(kv.Key).ApplyDto(kv.Value);
        }
        if (dto.Skills != null)
        {
            foreach (var kv in dto.Skills)
            {
                runtime.Skills.GetOrLog(kv.Key).ApplyDto(kv.Value);
            }
        }
        if (dto.Artifacts != null)
        {
            foreach (var kv in dto.Artifacts)
            {
                runtime.Artifacts.GetOrLog(kv.Key).ApplyDto(kv.Value);
            }
        }
        if (dto.collections != null)
        {
            foreach (var kv in dto.collections)
            {
                runtime.Collections.GetOrLog(kv.Key).ApplyDto(kv.Value);
            }
        }
        if (dto.achievements != null)
        {
            foreach (var kv in dto.achievements)
            {
                runtime.Achievements.GetOrLog(kv.Key).ApplyDto(kv.Value);
            }
        }
        if (dto.GuildeMissionData != null)
        {
            runtime.GuideMissionData.ApplyDto(dto.GuildeMissionData);
        }
        if (dto.dailyMissions != null)
        {
            foreach (var kv in dto.dailyMissions)
            {
                runtime.DailyMissions.GetOrLog(kv.Key).ApplyDto(kv.Value);
            }
        }

        if (dto.Ads != null)
        {
            foreach (var kv in dto.Ads)
            {
                runtime.Ads.GetOrLog(kv.Key).ApplyDto(kv.Value);
            }
        }

        if (dto.statistics != null)
        {
            foreach (var kv in dto.statistics)
            {
                runtime.SetNormalStatistic(kv.Value.UnlockType, kv.Value.Target, kv.Value.Value);
            }
        }
        if (dto.dailyStatistics != null)
        {
            foreach (var kv in dto.dailyStatistics)
            {
                runtime.SetDailyStatistic(kv.Value.UnlockType, kv.Value.Target, kv.Value.Value);
            }
        }

        // 필요시 Version 스위치로 마이그레이션
        return runtime;
    }
}