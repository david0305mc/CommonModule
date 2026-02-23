#pragma warning disable 114
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

public partial class DataManager {
	[Preserve]
	public partial class Coral {
		public int id;
		public string coralname;
		public int order;
		public string iconname;
		public UNLOCKTYPE unlocktype;
		public int unlocktarget;
		public int unlockvalue;
		public string unlockdesc;
		public int addfishmaxcount;
		public int costtype;
		public int costid;
		public string costcnt;
		public string productamount;
	}
	public Coral[] CoralArray { get; private set; }
	public Dictionary<int, Coral> CoralDic { get; private set; }
	[Preserve]
	public void BindCoralData(Type type, string text) {
		var deserializedData = CSVSerializer.Deserialize(text, type);
		GetType().GetProperty(nameof(CoralArray))?.SetValue(this, deserializedData, null);
		CoralDic = CoralArray?.ToDictionary(i => i.id) ?? new Dictionary<int, Coral>();
	}
	[Preserve]
	public Coral GetCoralData(int _id) {
		if (CoralDic != null && CoralDic.TryGetValue(_id, out Coral value)) {
			return value;
		}
		Debug.LogError($"테이블에 ID가 없습니다: {_id}");
		return null;
	}
}
