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
	[Preserve]
	public partial class Skill {
		public int id;
		public string skillname;
		public string skilldesc;
		public string skilllevelupdesc;
		public string iconname;
		public int cooltime;
		public int activetime;
		public int adview;
		public int levelupcost;
		public UNLOCKTYPE unlocktype;
		public int unlocktarget;
		public int unlockvalue;
		public string unlockdesc;
	}
	public Skill[] SkillArray { get; private set; }
	public Dictionary<int, Skill> SkillDic { get; private set; }
	[Preserve]
	public void BindSkillData(Type type, string text) {
		var deserializedData = CSVSerializer.Deserialize(text, type);
		GetType().GetProperty(nameof(SkillArray))?.SetValue(this, deserializedData, null);
		SkillDic = SkillArray?.ToDictionary(i => i.id) ?? new Dictionary<int, Skill>();
	}
	[Preserve]
	public Skill GetSkillData(int _id) {
		if (SkillDic != null && SkillDic.TryGetValue(_id, out Skill value)) {
			return value;
		}
		Debug.LogError($"테이블에 ID가 없습니다: {_id}");
		return null;
	}
	[Preserve]
	public partial class Artifact {
		public int id;
		public string artifactname;
		public string artifactdesc;
		public string artifactlevelupdesc;
		public string iconname;
		public string modelname;
		public int maxlevel;
		public int levelupcost;
		public UNLOCKTYPE unlocktype;
		public int unlocktarget;
		public int unlockvalue;
		public string unlockdesc;
	}
	public Artifact[] ArtifactArray { get; private set; }
	public Dictionary<int, Artifact> ArtifactDic { get; private set; }
	[Preserve]
	public void BindArtifactData(Type type, string text) {
		var deserializedData = CSVSerializer.Deserialize(text, type);
		GetType().GetProperty(nameof(ArtifactArray))?.SetValue(this, deserializedData, null);
		ArtifactDic = ArtifactArray?.ToDictionary(i => i.id) ?? new Dictionary<int, Artifact>();
	}
	[Preserve]
	public Artifact GetArtifactData(int _id) {
		if (ArtifactDic != null && ArtifactDic.TryGetValue(_id, out Artifact value)) {
			return value;
		}
		Debug.LogError($"테이블에 ID가 없습니다: {_id}");
		return null;
	}
}
