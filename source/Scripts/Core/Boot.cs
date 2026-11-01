using UnityEngine;

// Settings for the browser build, made once as the game starts.
namespace DiveGame
{
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Start()
        {
            Application.targetFrameRate = -1;   // follow the browser's own frame timing
#if UNITY_WEBGL && !UNITY_EDITOR
            // Open the phone's on-screen keyboard when a text box (the username, the survey) is tapped.
            WebGLInput.mobileKeyboardSupport = true;
#endif
        }
    }
}
