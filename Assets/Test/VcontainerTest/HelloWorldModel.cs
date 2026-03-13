using UnityEngine;
using UnityEngine.UI;

namespace Test
{
    public class HelloWorldModel : MonoBehaviour
    {

        [SerializeField] private Button _helloWorldButton;

        public Button HelloWorldButton => _helloWorldButton;
    }

}
