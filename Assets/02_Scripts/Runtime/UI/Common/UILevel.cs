using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using R3;

public class UILevel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelText;
    void Awake()
    {
        UserDataManager.Instance.User.Hero.Level.Subscribe(value =>
        {
            levelText.SetText($"{value}");
        }).AddTo(this);
    }

}
