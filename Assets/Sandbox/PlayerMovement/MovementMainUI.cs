using UnityEngine;
using UnityEngine.UI;

namespace PaladinTest
{
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

}
