using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

public partial class UserDataManager : Singleton<UserDataManager>
{
    public FishSpawnParam SpawnFish(int fishId, int count)
    {
        bool isNewFish = UserData.GetStatisticValue(UNLOCKTYPE.FISH_CREATE, fishId) == 0;

        AddItem(ITEMTYPE.FISH, fishId, count);
        if (UserData.Stone.FishTicket.Value > 0)
        {
            RemoveItem(ITEMTYPE.FISHTICKET, 1);
            UseFishTicket();
        }
        else
        {
            System.Numerics.BigInteger cost = FishCostCalc.CalcTotalCost(UserData.FishCreateCostCount, count);
            RemoveItem(ITEMTYPE.HEART, cost);
        }
        Save().Forget();
        MessageDispatcher.Publish(EMessageType.UIMenuSlideDown);
        return new FishSpawnParam(FishSpawnType.Normal, fishId, count, isNewFish);
    }

    public FishSpawnParam EvolveFishByGem(int evoId)
    {
        var evoInfo = DataManager.Instance.GetEvolutionData(evoId);
        var evoResult = DataManager.Instance.GenerateEvolutionResult(evoInfo.groupid);
        int resultFishId = evoResult.resultid;
        bool isNewFish = UserData.GetStatisticValue(UNLOCKTYPE.FISH_CREATE, resultFishId) == 0;
        AddItem(ITEMTYPE.FISH, resultFishId, 1);
        long evolutonBonus = isNewFish ? evoInfo.evnewbonus : evoInfo.evdupbonus;
        UserData.Stone.EvolutionBonus.Value += evolutonBonus;
        long tearStone = isNewFish ? 0 : evoResult.tearstone;
        AddItem(ITEMTYPE.TEARSTONE, tearStone);
        RemoveItem(evoInfo.evreqtype, evoInfo.evreqcost);
        RemoveItem(evoInfo.reqtype_1, evoInfo.reqid_1, evoInfo.reqcnt_1);
        RemoveItem(evoInfo.reqtype_2, evoInfo.reqid_2, evoInfo.reqcnt_2);
        LogEvent(EVENTLOGTYPE.EVOLUTION, evoId);
        Save().Forget();

        FishSpawnParam param = new FishSpawnParam(FishSpawnType.Evolution, resultFishId, 1, isNewFish, evolutonBonus, tearStone);
        MessageDispatcher.Publish(EMessageType.UIMenuSlideDown);
        return param;
    }
    public FishSpawnParam EvolveGuaranteedFishByStone(int evoId, int evoResultId)
    {
        var evoInfo = DataManager.Instance.GetEvolutionData(evoId);
        var evoResult = DataManager.Instance.GetEvolutionResultData(evoResultId);
        int resultFishId = evoResult.resultid;
        bool isNewFish = UserData.GetStatisticValue(UNLOCKTYPE.FISH_CREATE, resultFishId) == 0;
        AddItem(ITEMTYPE.FISH, resultFishId, 1);
        long evolutonBonus = isNewFish ? evoInfo.evnewbonus : evoInfo.evdupbonus;
        UserData.Stone.EvolutionBonus.Value += evolutonBonus;
        long tearStone = 0;
        AddItem(ITEMTYPE.TEARSTONE, tearStone);
        RemoveItem(ITEMTYPE.TEARSTONE, evoResult.buycost);
        RemoveItem(evoInfo.reqtype_1, evoInfo.reqid_1, evoInfo.reqcnt_1);
        RemoveItem(evoInfo.reqtype_2, evoInfo.reqid_2, evoInfo.reqcnt_2);
        LogEvent(EVENTLOGTYPE.EVOLUTION, evoId);
        Save().Forget();

        FishSpawnParam param = new FishSpawnParam(FishSpawnType.Evolution, resultFishId, 1, isNewFish, evolutonBonus, tearStone);
        MessageDispatcher.Publish(EMessageType.UIMenuSlideDown);
        return param;
    }

    public async UniTask<FishSpawnParam> EvolveFishByAD(int evoId)
    {
        var evoInfo = DataManager.Instance.GetEvolutionData(evoId);
        var evoResult = DataManager.Instance.GenerateEvolutionResult(evoInfo.groupid);
        int resultFishId = evoResult.resultid;
        bool isNewFish = UserData.GetStatisticValue(UNLOCKTYPE.FISH_CREATE, resultFishId) == 0;
        var result = await ShowAD(evoInfo.adid);
        if(!result)
        {
            return null;
        }
        var adData = UserData.GetAd(evoInfo.adid);
        adData.StartCooldown(adData.Table.cooltime);
        long evolutonBonus = isNewFish ? evoInfo.evnewbonus : evoInfo.evdupbonus;
        long tearStone = isNewFish ? 0 : evoResult.tearstone;
        UserData.Stone.EvolutionBonus.Value += evolutonBonus;
        AddItem(ITEMTYPE.FISH, resultFishId, 1);
        RemoveItem(evoInfo.reqtype_1, evoInfo.reqid_1, evoInfo.reqcnt_1);
        RemoveItem(evoInfo.reqtype_2, evoInfo.reqid_2, evoInfo.reqcnt_2);
        LogEvent(EVENTLOGTYPE.EVOLUTION, evoId);
        Save().Forget();
        FishSpawnParam param = new FishSpawnParam(FishSpawnType.Evolution, resultFishId, 1, isNewFish, evolutonBonus, tearStone);
        MessageDispatcher.Publish(EMessageType.UIMenuSlideDown);
        MessageDispatcher.Publish(EMessageType.UIMenuSlideDown);
        return param;
    }
    public void ReceiveAttendanceFishReward()
    {
        int nextDay = UserData.Stone.AttendanceDay.Value + 1;
        var rewardInfo = DataManager.Instance.GetAttendanceRewardByDay(nextDay);
        if (rewardInfo == null)
        {
            Debug.LogError($"[Attendance] Reward info not found for day: {nextDay}");
            return;
        }
        UserData.Stone.NextAttendanceTimeMs = GameTime.Instance.CalcNextDayResetTimeKstMs();
        UserData.Stone.AttendanceDay.Value = nextDay;
        UserData.Stone.StartNextAttendanceTimer().Forget();
        UserData.AddStatistic(UNLOCKTYPE.ATTENDANCE_COUNT, 0, 1);
        AddItem(ITEMTYPE.FISH, rewardInfo.rewardid, rewardInfo.rewardvalue);
        LogEvent("attendance_confirm");
        Save().Forget();
    }
    public void ReceiveAttendanceReward()
    {
        int nextDay = UserData.Stone.AttendanceDay.Value + 1;
        var rewardInfo = DataManager.Instance.GetAttendanceRewardByDay(nextDay);
        if (rewardInfo == null)
        {
            Debug.LogError($"[Attendance] Reward info not found for day: {nextDay}");
            return;
        }
        UserData.Stone.NextAttendanceTimeMs = GameTime.Instance.CalcNextDayResetTimeKstMs();
        UserData.Stone.AttendanceDay.Value = nextDay;
        UserData.Stone.StartNextAttendanceTimer().Forget();
        UserData.AddStatistic(UNLOCKTYPE.ATTENDANCE_COUNT, 0, 1);
        AddItem(rewardInfo.rewardtype, rewardInfo.rewardvalue);
        LogEvent("attendance_confirm");
        Save().Forget();
        PopupMultiReward
            .Show(rewardInfo.rewardtype, 0, rewardInfo.rewardvalue)
            .Forget();
    }

    public void ReceiveGuideMission()
    {
        if (!UserData.GuideMissionData.IsCompleted.Value)
        {
            return;
        }
        var guideMissionInfo = DataManager.Instance.GetGuideMissionData(UserData.GuideMissionData.Tid.Value);
        AddItem(guideMissionInfo.rewardtype, guideMissionInfo.rewardid, guideMissionInfo.rewardcnt);
        LogEvent(EVENTLOGTYPE.GUIDE_MISSION, guideMissionInfo.id);
        UserData.AddStatistic(UNLOCKTYPE.GUIDE_MISSION_CLEAR, 0, 1);
        PopupMultiReward.Show(guideMissionInfo.rewardtype, guideMissionInfo.rewardid, guideMissionInfo.rewardcnt).Forget();
        UserData.GuideMissionData.GenerateMissionData(guideMissionInfo.nextmissionid);
        Save().Forget();
    }
    public void SetBGMOn(bool on)
    {
        UserData.Player.IsBgmEnabled.Value = on ? 1 : 0;
        Save().Forget();
    }
    public void SetAtmospereOn(bool on)
    {
        UserData.Player.IsAtmosphereEnabled.Value = on ? 1 : 0;
        Save().Forget();
    }
    public void SetSfxOn(bool on)
    {
        UserData.Player.IsSfxEnabled.Value = on ? 1 : 0;
        Save().Forget();
    }
    public void PlaceFishInWorldFromUI(int tid)
    {
        PlaceFishInWorld(tid);
        Save().Forget();
    }
    public void RetrieveFishesToTankFromUI(int tid)
    {
        RetrieveFishesToTank(tid);
        Save().Forget();
    }
}
