using UnityEngine;

namespace Marooned.UI.Views
{
    /// <summary>Detective-board style layout of collected ClueDef cards, for the player (and AI agent's get_clue_board query) to review.</summary>
    public class ClueBoardView : MonoBehaviour
    {
        [SerializeField] private Transform clueBoardContainer;
    }
}
