

using R3;

public class UserCurrencyData
{
    public ReactiveProperty<long> Gold = new ReactiveProperty<long>();
    public ReactiveProperty<long> Gem = new ReactiveProperty<long>();
    public ReactiveProperty<long> Energy = new ReactiveProperty<long>();
}
public class SkillData
{
    public int SkillID;
    public ReactiveProperty<long> Level = new ReactiveProperty<long>();
}