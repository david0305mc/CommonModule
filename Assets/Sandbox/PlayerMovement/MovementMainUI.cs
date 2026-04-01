using UnityEngine;
using UnityEngine.UI;

public class MovementMainUI : MonoBehaviour
{
    [SerializeField] private Button skillButton;
    [SerializeField] private PaladinObj paladinObj;


    void Awake()
    {
        skillButton.onClick.AddListener(() =>
        {
            paladinObj.Attack();
        });
    }


}
