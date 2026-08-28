using System;
using System.Collections.Generic;
using DICS.Test.Fixtures;
using NUnit.Framework;

namespace DICS.Test
{
    public class InjectorTestConfig
    {
        [Test]
        public void TestConfig()
        {
            var module = new TestModuleConfig();
            var frozen = module.Freeze();
            var injector = new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), String.Empty, frozen);

            var plan = injector.Plan(
                new HashSet<Key>
                {
                    Key.Of<Greeter>()
                },
                new HashSet<IAxisPoint>
                {
                    SceneAxisPoint.Provided
                }
            );

            var loc = injector.Produce(plan);
            var g = loc.Get<Greeter>();
            Assert.That(g.Hello("Joe"), Is.EqualTo("Welcome, Joe"));
        }
    }
}