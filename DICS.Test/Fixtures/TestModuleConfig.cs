using DICS.Attribute;

// ReSharper disable PartialTypeWithSinglePart

namespace DICS.Test.Fixtures
{
    public abstract record SceneAxisPoint : IAxisPoint
    {
        public static SceneAxisPoint Managed = new PManaged();
        public static SceneAxisPoint Provided = new PProvided();

        public string AxisName()
        {
            return "TestSceneAxis";
        }

        public abstract string PointName();

        public record PManaged : SceneAxisPoint
        {
            public override string PointName()
            {
                return "Managed";
            }
        }

        public record PProvided : SceneAxisPoint
        {
            public override string PointName()
            {
                return "Provided";
            }
        }
    }

    public interface Greeter
    {
        string Hello(string name);
    }

    [LiftConstructor]
    public partial class RudeGreeter : Greeter
    {
        public string Hello(string name)
        {
            return $"You again, {name}";
        }
    }

    [LiftConstructor]
    public partial class KindGreeter : Greeter
    {
        public string Hello(string name)
        {
            return $"Welcome, {name}";
        }
    }

    public class TestModuleConfig : Module
    {
        public TestModuleConfig()
        {
            Make<Greeter>().In(SceneAxisPoint.Managed).From().Functoid(RudeGreeter.Lift());
            Make<Greeter>().In(SceneAxisPoint.Provided).From().Functoid(KindGreeter.Lift());
        }
    }
}