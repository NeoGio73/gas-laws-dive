using System;
using System.Runtime.InteropServices;

// Today's date on the student's own clock, as YYYY-MM-DD. In a browser build .NET's clock can
// read in UTC, which would put an evening finish on the next day, so the browser is asked instead
// (Plugins/WebGL/DiveBridge.jslib), as the JavaScript games do.
namespace DiveGame.Study
{
    public static class LocalDate
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string DiveLocalDay();
        public static string Today() => DiveLocalDay();
#else
        public static string Today() => DateTime.Now.ToString("yyyy-MM-dd");
#endif
    }
}
