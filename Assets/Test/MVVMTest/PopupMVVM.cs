using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupMVVM : PopupBase<Unit>
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button addButton;
    [SerializeField] private Button minusButton;

    private PopupViewModel _vm;
    private CompositeDisposable _cd;

    public override void Awake()
    {
        base.Awake();

        // ViewModel 생성(주입)
        _vm = new PopupViewModel(UserDataManager.Instance);
        _cd = new CompositeDisposable();

        // === Binding: ViewModel -> View ===
        _vm.TitleText
            .Subscribe(text => titleText.SetText(text))
            .AddTo(_cd);

        // === Binding: View -> ViewModel (Command) ===
        addButton.onClick.AddListener(_vm.Add);
        minusButton.onClick.AddListener(_vm.Minus);
    }
        
    private void OnDestroy()
    {
        // PopupBase가 Dispose를 지원하는 구조라면 거기에 맞추면 됨
        _cd?.Dispose();
        _vm?.Dispose();
    }
}