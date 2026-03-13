using R3;
using UnityEngine;

public class UserData : Singleton<UserData>
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
