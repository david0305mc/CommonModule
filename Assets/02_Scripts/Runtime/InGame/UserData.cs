using R3;
using UnityEngine;

public class UserData
{
    public ReactiveProperty<int> Gold;

    public void InitData()
    {
        Gold = new ReactiveProperty<int>();
    }
    
}
