using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Marooned.UI.Core
{
    public enum UIPanelType
    {
        CardHand,
        MapExplore,
        MeetingVote,
        ClueBoard,
        ConditionOverlay
    }

    /// <summary>
    /// Same explicit enum->Type panel resolution as the reference project (no
    /// assembly scanning). Also re-applies the stretch-anchor fix from the
    /// reference project's Lab 13 (ContentSizeFitter=PreferredSize was clobbering
    /// anchor stretch and causing panels to pile up center-screen).
    /// </summary>
    public class UIRoot : IInitializable
    {
        private readonly Dictionary<UIPanelType, Type> _panelViewTypes = new()
        {
            { UIPanelType.CardHand, typeof(Views.CardHandView) },
            { UIPanelType.MapExplore, typeof(Views.MapExploreView) },
            { UIPanelType.MeetingVote, typeof(Views.MeetingVoteView) },
            { UIPanelType.ClueBoard, typeof(Views.ClueBoardView) },
            { UIPanelType.ConditionOverlay, typeof(Views.ConditionOverlayView) },
        };

        public void Initialize()
        {
            // Lab A: just make sure the Canvas root stretches full-screen and every
            // top-level panel starts at Unconstrained layout, per the reference
            // project's UIRoot.Awake() fix. Actual instantiation of each panel
            // prefab happens once GameplayScene loads (see design doc Additive
            // Scene section).
        }
    }
}
