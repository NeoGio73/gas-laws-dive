using DiveGame.Dives;
using UnityEngine;

// The reef's animals and the dive buddy (handoff Section 11). Everything is driven only by the clock
// since the scene started and fixed numbers chosen in the scene builder, so every student sees the
// same animals at the same moments, in both versions. Animals keep to the left part of the view
// (away from the side panel), stay a few metres from the diver, and are hidden while a question is
// on screen. No animal comes at the diver; nothing is touched or fed.
// (Each component has its own file, named after it: Unity can save a component in a scene only then.
// Swimmer.cs, School.cs, Buddy.cs, DiveSand.cs.)
namespace DiveGame.Scene
{
    // Hides the animals while questions are asked (seductive details stay out of the question screens).
    public class Wildlife : MonoBehaviour
    {
        bool shown = true;

        void LateUpdate()
        {
            bool show = !(DiveDirector.Current && DiveDirector.Current.Phase == "questions");
            if (show == shown) return;
            shown = show;
            foreach (Transform child in transform) child.gameObject.SetActive(show);
        }
    }
}
