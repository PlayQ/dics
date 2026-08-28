using System;

namespace DICS
{
    public interface IDicsMeasurement
    {
        IDisposable StartTotal();
        IDisposable Start(Key key);
        IDisposable Start(string key);
        public string PlanToString(Plan plan);
        public string PlanToTrace(Plan plan);

        public static DefaultDicsMeasurement FromDefault() => new DefaultDicsMeasurement();
    }
}