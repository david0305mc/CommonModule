using Cysharp.Threading.Tasks;

public partial class UserDataManager : Singleton<UserDataManager>
{
    public void AddLevel(int amt = 1)
    {
        User.Hero.Level.Value += amt;
        SaveLocalDataAsync().Forget();
    }
    public void AddGem(long amount = 1)
    {
        if (amount <= 0)
            return;

        User.Currency.Gem.Value += amount;
        SaveLocalDataAsync().Forget();
    }

    public void AddGold(long amount)
    {
        if (amount <= 0)
            return;

        User.Currency.Gold.Value += amount;
        SaveLocalDataAsync().Forget();
    }

    public void AddHeart(long amount)
    {
        if (amount <= 0)
            return;

        User.Currency.Heart.Value += amount;
        SaveLocalDataAsync().Forget();
    }

    public void SetSkillLevel(int skillId, long level)
    {
        if (skillId <= 0)
            return;

        if (!User.Skills.TryGetValue(skillId, out var skill))
        {
            skill = new SkillData(skillId);
            User.Skills[skillId] = skill;
        }

        skill.Level.Value = level;
        SaveLocalDataAsync().Forget();
    }
}