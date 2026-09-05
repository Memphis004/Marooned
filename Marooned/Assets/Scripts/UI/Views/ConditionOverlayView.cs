using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Small HUD icons for the player's own active illness/injury cards (ActiveConditionCardIds). NPC-side visuals are handled by ChibiAnimatedRenderer's ConditionOverlays slots directly, not this view.</summary>
    public class ConditionOverlayView : MonoBehaviour
    {
        [SerializeField] private Transform iconContainer;
    }
}
