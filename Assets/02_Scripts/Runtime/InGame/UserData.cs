using R3;



public class UserCurrencyData
{
    public ReactiveProperty<long> Gold = new ReactiveProperty<long>();
    public ReactiveProperty<long> Gem = new ReactiveProperty<long>();
    public ReactiveProperty<long> Heart = new ReactiveProperty<long>();
}

public class UserCurrencyDataDTO
{
    public long Gold;
    public long Gem;
    public long Heart;
}


public class SkillData
{
    public int SkillID;
    public ReactiveProperty<long> Level = new ReactiveProperty<long>();
}
public class SkillDataDTO
{
    public int SkillID;
    public long Level;
}