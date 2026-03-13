using VContainer;
using VContainer.Unity;
using UnityEngine;
namespace Test
{
    [RequireComponent(typeof(HelloWorldView))]
    public class HelloworldLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<HelloworldService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<HelloWorldPresenter>();
            builder.RegisterComponent(GetComponent<HelloWorldView>());
        }
    }

}
