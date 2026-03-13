using System;
using UnityEngine;
using VContainer.Unity;

namespace Test
{

    public class HelloWorldPresenter : IStartable, IDisposable
    {
        private readonly HelloworldService _helloWorldService;
        private readonly HelloWorldView _helloWorldModel;
        public void Start()
        {
            _helloWorldModel.HelloWorldButton.onClick.AddListener(Print);
        }

        public void Dispose()
        {
            _helloWorldModel.HelloWorldButton.onClick.RemoveListener(Print);
        }

        public HelloWorldPresenter(HelloworldService helloWorldService, HelloWorldView helloWorldModel)
        {
            _helloWorldService = helloWorldService;
            _helloWorldModel = helloWorldModel;
        }

        private void Print() => _helloWorldService.Print();
    }

}
