using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

public sealed class RecipeListPopup : PopupBase<RecipeListPopup.Args, Unit>
{
    public sealed class Args
    {
        public IReadOnlyList<string> Recipes { get; }
        public string Title { get; }

        public Args(IReadOnlyList<string> recipes, string title)
        {
            Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            Title = title;
        }
    }

    [SerializeField] private Text titleLabel;
    [SerializeField] private Text recipesLabel;

    public override async UniTask Show()
    {
        await base.Show();
        if (titleLabel != null) titleLabel.text = Arguments.Title;
        if (recipesLabel != null) recipesLabel.text = string.Join("\n", Arguments.Recipes);
    }

    public void OnConfirmClicked() => Complete(Unit.Default).Forget();
}
