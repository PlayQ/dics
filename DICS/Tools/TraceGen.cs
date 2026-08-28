using System;
using System.Collections.Generic;
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_WEBGL
using Newtonsoft.Json;
#else
using System.Text.Json;
#endif

// ReSharper disable InconsistentNaming

namespace DICS.Tools
{
    // - https://www.chromium.org/developers/how-tos/trace-event-profiling-tool/
    // - https://docs.google.com/document/d/1CvAClvFfyA5R-PhYUmn5OOQtYMH4h6I0nSsKchNAySU/preview?tab=t.0
    public static class TraceGen
    {
        public static string RenderProfile(List<Key> InstantiationOrder,
            Dictionary<Key, DateTime> Timestamps,
            Dictionary<Key, TimeSpan> Timings)
        {
            var epoch = new DateTime(1970, 1, 1);

            var traceEvents = new List<TraceEvent>();

            foreach (var key in InstantiationOrder)
            {
                // Traces use microseconds format
                var ts = (Timestamps[key] - epoch).TotalMilliseconds * 1000;
                var dur = Timings[key].TotalMilliseconds * 1000;

                var traceEvent = new TraceEvent
                {
                    name = key.ToString(),
                    cat = "Instantiation",
                    ph = "X",
                    ts = ts,
                    dur = dur,
                    pid = 0,
                    tid = key.ToString(),
                    args = new Dictionary<string, object>()
                };

                traceEvents.Add(traceEvent);
            }

            var traceObject = new Trace { traceEvents = traceEvents };

#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_WEBGL
            var json = JsonConvert.SerializeObject(traceObject);
#else
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(traceObject, options);
#endif

            return json;
        }

        public class TraceEvent
        {
            public string name { get; set; } = null!;
            public string cat { get; set; } = null!;
            public string ph { get; set; } = null!;
            public double ts { get; set; }
            public double dur { get; set; }
            public int pid { get; set; }
            public string tid { get; set; } = null!;
            public Dictionary<string, object> args { get; set; } = null!;
        }

        public class Trace
        {
            public List<TraceEvent> traceEvents { get; set; } = null!;
        }
    }
}