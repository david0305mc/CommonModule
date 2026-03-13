using R3;

public class UserData 
{
    public ReactiveProperty<int> Gold;

    public void InitData()
    {
        Gold = new ReactiveProperty<int>();
    }
    public void AddGold(int add)
    {
        Gold.Value += add;
    }

}
