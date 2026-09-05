using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Displays the player's card hand/inventory; drag onto CraftingSystem or UseCard action.</summary>
    public class CardHandView : MonoBehaviour
    {
        [SerializeField] private Transform cardSlotContainer;
        [SerializeField] private GameObject cardSlotPrefab; // pooled, per reference project's grid button pooling lesson

        // Presenter calls RenderHand(cardId -> count) to refresh slots.
    }
}
