namespace Marooned.UI.Presenters
{
    /// <summary>Plain C#, transient. Reads CardInventorySystem, pushes to CardHandView.</summary>
    public class CardHandPresenter
    {
        // Constructor-injects CardInventorySystem + CardHandView reference; subscribes
        // to inventory-changed message and calls view.RenderHand(...).
    }
}
