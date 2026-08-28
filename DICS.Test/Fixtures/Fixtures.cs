using System;
using System.Collections.Generic;
using DICS.Attribute;

// ReSharper disable FieldCanBeMadeReadOnly.Local

// ReSharper disable PartialTypeWithSinglePart

namespace DICS.Test.Fixtures
{
    [LiftFactory(FactoryKind.Constructor)]
    public partial record TestGenFac(
        ITestSuper Dep2,
        [Local] int Arg
    );

    [LiftInitializer]
    public sealed partial class TestGenSealed
    {
        [Inject] private ITestSuper? _dep1;

        public ITestSuper? GetSuperDep()
        {
            return _dep1;
        }
    }


    [LiftInitializer]
    public partial class TestGenInitSuper
    {
        [Inject] protected ITestSuper? Dep1;

        public ITestSuper? GetSuperDep()
        {
            return Dep1;
        }
    }

    [LiftFactory(FactoryKind.Initializer)]
    public partial class TestGenInit : TestGenInitSuper
    {
        [Inject] [Local] protected string A2 = "xxx";
        [Inject] [Local] protected int A3;
        [Inject] protected ITestSuper? Dep2;

        public string Dump()
        {
            return $"{A2}:{A3}";
        }

        public bool DepCorrect()
        {
            return Dep2 != null && Dep2 == GetSuperDep();
        }
    }

    [LiftInitializer]
    public abstract partial class SomeSuper
    {
        [Inject] [Id("test-super")] protected string A1 = "xxx";
        [Inject] protected string A2 = "xxx";

        protected SomeSuper()
        {
            Prop = "xxx";
        }

        [Inject] [Id("test-prop")] protected string Prop { get; set; }


        public string DumpSuper()
        {
            return $"{A1}:{A2}:{Prop}";
        }
    }

    [LiftInitializer]
    public partial class SomeSub : SomeSuper
    {
        [Inject] [Id("test-sub")] protected int B1;
        [Inject] protected int B2;

        public string DumpSub()
        {
            return $"{B1}:{B2}";
        }
    }

    // public partial class SomeSub2 : SomeSuper
    // {
    // }

    public interface ITestSuper
    {
    }

    public interface ITestDep : ITestSuper
    {
    }

    public class TestDep : ITestDep
    {
    }

    [LiftConstructor]
    public partial class C1
    {
    }

    [LiftConstructor]
    public partial class C2 : C1
    {
    }

    [LiftInitializer]
    public partial class C3
    {
    }

    [LiftInitializer]
    public partial class C4 : C3
    {
    }


    [LiftConstructor]
    internal partial class SomeShite
    {
    }

    [LiftConstructor]
    public partial class Cg1<TA, TB>
    {
    }

    [LiftInitializer]
    public partial class Cg2<TA, TB>
    {
        public class Fa : UnsafeFactoryFunctoidImpl<Cg2<TA, TB>>
        {
            public Fa(IFunctoid functoid, ILocator locator, IInitializer? initializer)
                : base(functoid, locator, initializer)
            {
            }
        }
    }

    [LiftConstructor]
    [LiftFactory(FactoryKind.Constructor)]
    public partial record TestClass(
        [Id("testname")] ITestDep Dep,
        [Id("xxx")] int Arg,
        ITestSuper Dep2,
        [Id("test-set")] ISet<string> Strings,
        [Id("test-set-empty")] [Local] ISet<string> StringsEmpty,
        MagicMutableDicsReference<LocatorMeta> MetaRef,
        MagicMutableDicsReference<ILocator> LocatorRef
    );

    [LiftConstructor]
    public partial record TestClass2(TestClass Dep);

    [LiftConstructor]
    public partial record TestClass4(TestClass Dep, int Arg);

    [LiftInitializer]
    public partial class TestClass5
    {
        [Inject] protected int Arg;
        [Inject] protected TestClass? Dep { get; set; }

        public override string ToString()
        {
            return $"{nameof(Arg)}: {Arg}, {nameof(Dep)}: {Dep}";
        }
    }


    public interface IBehaviour
    {
    }

    [LiftInitializer]
    public partial class TestBehaviour : IBehaviour
    {
        [Inject] protected TestClass Dep = null!;
        [Inject] [Id("test-set")] protected ISet<string> Dep1 = null!;

        public void Initialize(TestClass d)
        {
            Dep = d;
        }
    }

    public class BehaviourCreator
    {
        public T Create<T>()
            where T : class
        {
            var dummy = new TestBehaviour();
            if (dummy is T behaviour) return behaviour;
            throw new Exception("");
        }
    }
}