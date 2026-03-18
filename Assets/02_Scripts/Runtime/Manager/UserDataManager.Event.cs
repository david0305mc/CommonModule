using Cysharp.Threading.Tasks;

public partial class UserDataManager : Singleton<UserDataManager>
{
    public void AddGem(long amount = 1)
    {
        if (amount <= 0)
            return;

        UserData.Currency.Gem.Value += amount;
        SaveLocalDataAsync().Forget();
    }

    public void AddGold(long amount)
    {
        if (amount <= 0)
            return;

        UserData.Currency.Gold.Value += amount;
        SaveLocalDataAsync().Forget();
    }

    public void AddHeart(long amount)
    {
        if (amount <= 0)
            return;

        UserData.Currency.Heart.Value += amount;
        SaveLocalDataAsync().Forget();
    }

    public void SetSkillLevel(int skillId, long level)
    {
        if (skillId <= 0)
            return;

        if (!UserData.SkillMap.TryGetValue(skillId, out var skill))
        {
            skill = new SkillData(skillId);
            UserData.SkillMap[skillId] = skill;
        }

        skill.Level.Value = level;
        SaveLocalDataAsync().Forget();
    }
}