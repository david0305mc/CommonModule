#pragma warning disable 114
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Scripting;

public class ConfigTable : Singleton<ConfigTable> {
	[Preserve]
	public int ShowDevilSayTextCoolTime;
	[Preserve]
	public int OfflineRewardMinTime;
	[Preserve]
	public int OfflineRewardMaxTime;
	[Preserve]
	public int OfflineRewardRate;
	[Preserve]
	public int FreeGachaCooltime;
	[Preserve]
	public int MaxFishCreate;
	[Preserve]
	public int FishTankSetDefaultCount;
	[Preserve]
	public int StoneTalkMax;
	[Preserve]
	public int  FishTalkMax;
	[Preserve]
	public int StoneGrowUp_1;
	[Preserve]
	public int StoneGrowUp_2;
	[Preserve]
	public int StoneGrowUp_3;
	[Preserve]
	public int StartHeart;
	[Preserve]
	public int StoneLevelMax;
	[Preserve]
	public int share_event_count;

	[Preserve]
	public void LoadConfig(Dictionary<string, Dictionary<string, object>> rowList)
	{
		foreach (var rowItem in rowList)
		{
			var field = typeof(ConfigTable).GetField(rowItem.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (field == null) continue;
			field.SetValue(this, rowItem.Value["value"]);
		}
	}
}
