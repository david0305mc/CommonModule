# PopupManager: 타입 안전 입력

기존 `PopupBase<TResult>` / `ShowPopup<TPopup, TResult>(params object[] args)`는 그대로 사용할 수 있습니다. 새 팝업은 `PopupBase<TArgs, TResult>`를 상속하고, `Arguments` 또는 `OnInitialize(TArgs args)`로 입력을 사용합니다. `Arguments`라는 이름은 팝업 안에 중첩 타입 `Args`를 선언할 수 있도록 선택했습니다.

## RecipeListPopup 예시

전체 예제: [Samples~/Typed/RecipeListPopup.cs](Samples~/Typed/RecipeListPopup.cs). 이 프로젝트에서 사용하는 `R3.Unit`을 반환 타입으로 사용합니다. `Unit`은 PopupManager가 강제하는 타입이 아니며 프로젝트의 다른 Unit 타입이나 일반 결과 타입도 사용할 수 있습니다.

```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;

public sealed class RecipeListPopup : PopupBase<RecipeListPopup.Args, Unit>
{
    public sealed class Args
    {
        public IReadOnlyList<string> Recipes { get; }
        public string Title { get; }

        public Args(IReadOnlyList<string> recipes, string title)
        {
            Recipes = recipes;
            Title = title;
        }
    }

    public override async UniTask Show()
    {
        await base.Show();
        // Arguments.Recipes / Arguments.Title로 UI 구성.
        // object[] 인덱스 검사나 캐스팅이 필요하지 않습니다.
    }

    public UniTask ConfirmAsync() => Complete(Unit.Default);
}
```

`RecipeListPopup` 컴포넌트를 붙인 같은 이름의 프리팹을 `Resources/Popup/RecipeListPopup`에 저장하거나 매니저의 `popupPrefabs`에 등록합니다. 예제 파일은 프로젝트에 복사하여 사용합니다.

```csharp
var args = new RecipeListPopup.Args(recipes, "레시피 목록");

var popup = await PopupManager.Instance
    .ShowPopup<RecipeListPopup, RecipeListPopup.Args, Unit>(args);

// 열기 애니메이션까지 기다리기
var animated = await PopupManager.Instance
    .ShowPopup<RecipeListPopup, RecipeListPopup.Args, Unit>(waitOpenAni: true, args: args);

// 결과만 받기 (팝업을 여는 별도의 호출 예시)
Unit result = await PopupManager.Instance
    .ShowPopupForResult<RecipeListPopup, RecipeListPopup.Args, Unit>(args);

// 결과 API에도 같은 애니메이션 대기 옵션 제공
Unit animatedResult = await PopupManager.Instance
    .ShowPopupForResult<RecipeListPopup, RecipeListPopup.Args, Unit>(waitOpenAni: true, args: args);
```

두 API 모두 `where TPopup : PopupBase<TArgs, TResult>` 제약을 사용합니다. 예를 들어 `ShowPopup<RecipeListPopup, string, Unit>("wrong")`이나 반환 타입을 `bool`로 바꾼 호출은 컴파일되지 않습니다. 참조 타입, 구조체, nullable 입력을 지원하며 값 자체의 유효성은 Args 생성자나 `OnInitialize`에서 검사할 수 있습니다.

## 생성, 표시, 반환 순서

1. 새 프리팹은 비활성 부모 아래에서 생성합니다. 풀에서 빌린 인스턴스도 비활성 상태를 유지합니다.
2. 이번 호출의 결과/애니메이션 대기 상태와 닫기 콜백을 만들고, 새 `Arguments`를 주입한 뒤 `OnInitialize(args)`를 호출합니다.
3. 활성화하여 `Awake`(최초 활성화 시), `OnEnable`을 실행한 다음 `Show()`를 호출합니다. 따라서 활성화 콜백에서도 새 입력을 사용할 수 있습니다.
4. 매니저는 `Show()`의 반환 작업을 await하여 동기/비동기 예외를 호출자에게 전달합니다. `waitOpenAni`는 그 이후 열기 애니메이션 완료까지 기다릴지를 결정합니다. `Show()` override는 `base.Show()`를 호출해야 합니다.
5. 닫기 애니메이션이 끝나면 활성 목록에서 제거하고 비활성화합니다. `Arguments`와 기존 `_args`, 닫기 콜백을 정리하고 해당 호출의 취소 토큰을 취소한 후 풀에 반환합니다. 입력의 기본값은 참조 타입에서는 `null`, 구조체에서는 `default`입니다.

`OnInitialize`는 풀 재사용 때마다 호출됩니다. 첫 생성에서는 `Awake`보다 먼저 실행되므로 `Awake`에서 초기화한 필드에 의존하지 마세요. 직렬화된 참조나 Args 자체를 사용하고, 활성화 이후 필요한 작업은 `Show()`에 둡니다. 직접 별도 필드에 복사한 참조와 이벤트 구독은 해당 팝업이 정리해야 합니다.

`Complete(result)`와 닫기 버튼/ESC의 기본 결과, 닫기 애니메이션 동작은 유지됩니다. `Complete`의 결과는 닫기 애니메이션 완료 전에 전달될 수 있습니다. `CloseAsync()`를 await하면 닫기 및 반환 완료까지 기다립니다. 열기가 끝나기 전에 반환하면 아직 대기 중인 `WaitForShowAsync()`는 취소됩니다. 비동기 작업에는 `PopupCancellationToken`을 전달하고, await 전에 필요한 입력을 로컬에 저장하세요. 반환 이후 이전 작업이 다시 `Arguments`를 읽으면 다음 호출의 입력일 수 있습니다.

초기화나 `Show` 실패 시 활성 목록에서 제거하고 Args를 정리한 뒤 해당 인스턴스를 폐기합니다. 부분 초기화된 객체는 다시 풀에 넣지 않습니다. 프리팹 컴포넌트 누락도 인스턴스를 폐기합니다. 중복 반환은 무시하며 이전 호출의 늦은 완료/예외는 새 호출을 닫지 않습니다. Unity 자체가 로그로 처리하는 `Awake`/`OnEnable` 이벤트 예외는 매니저에서 잡을 수 없으므로, 호출자에게 전달해야 하는 검증은 `OnInitialize` 또는 `Show`에서 수행하세요.

## 기존 코드 마이그레이션

기존:

```csharp
public class RecipeListPopup : PopupBase<Unit>
{
    public override async UniTask Show()
    {
        await base.Show();
        var recipes = (IReadOnlyList<string>)_args[0];
        var title = (string)_args[1];
    }
}

await manager.ShowPopup<RecipeListPopup, Unit>(recipes, title);
```

변경:

1. 입력을 `RecipeListPopup.Args`로 묶고 기반 클래스를 `PopupBase<RecipeListPopup.Args, Unit>`으로 바꿉니다.
2. `_args` 검사/캐스팅을 `Arguments.Recipes`, `Arguments.Title`로 바꿉니다. 매번 필요한 비활성 초기화는 `OnInitialize(Args args)`에 둡니다.
3. 호출을 `manager.ShowPopup<RecipeListPopup, RecipeListPopup.Args, Unit>(new RecipeListPopup.Args(recipes, title))`로 바꿉니다. `ShowPopupForResult`와 `waitOpenAni` 오버로드도 같은 방식입니다.
4. 기존 `Complete`, `OnClickClose`, `CloseAsync`, 애니메이터/버튼 연결은 유지합니다.

마이그레이션하지 않은 팝업과 호출부는 수정할 필요가 없습니다. 타입 안전 기반 클래스로 바꾼 팝업을 이전 2개 제네릭 인자 API로 호출하면 `InvalidOperationException`을 발생시켜, 입력이 주입되지 않은 상태로 표시되는 것을 막습니다. 기존에는 `Show()` override가 fire-and-forget으로 실행되었지만 이제 매니저가 완료와 예외를 관찰합니다. `Show()`에는 표시 준비 작업을 넣고 사용자 응답은 `WaitForResultAsync()`로 기다리세요.

## 테스트

`Tests/Runtime`에 Unity Test Framework PlayMode 테스트가 있습니다. 패키지 테스트가 Test Runner에 표시되지 않으면 프로젝트의 `Packages/manifest.json`에 `"testables": ["com.david.popupmanager"]`를 추가합니다. Test Runner의 PlayMode에서 `PopupManagerTests`를 실행하세요.

테스트는 타입 제약, 참조/값/null 입력, 최초 생성 및 풀 재사용의 활성화 순서, 입력 참조 정리, 결과와 기본 닫기, 애니메이션 대기 옵션, 중복 반환, 초기화/표시 실패 및 이전 비동기 호출과 새 호출의 분리를 검증합니다.
