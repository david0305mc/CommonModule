
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using R3;

public class UICurrency : MonoBehaviour
{
    [SerializeField] private CurencyType currencyType;
    [SerializeField] private TextMeshProUGUI countText;

    void Awake()
    {
        switch (currencyType)
        {
            case CurencyType.Gold:
                {
                    UserDataManager.Instance.UserData.Currency.Gold.Subscribe(value =>
                    {
                        countText.SetText($"{value}");
                    }).AddTo(this);
                }
                break;
            case CurencyType.Gem:
            {
                    UserDataManager.Instance.UserData.Currency.Gem.Subscribe(value =>
                    {
                        countText.SetText($"{value}");
                    }).AddTo(this);
                }
                break;
            case CurencyType.Heart:
            {
                    UserDataManager.Instance.UserData.Currency.Heart.Subscribe(value =>
                    {
                        countText.SetText($"{value}");
                    }).AddTo(this);
                }
                break;
        }
    }
}
