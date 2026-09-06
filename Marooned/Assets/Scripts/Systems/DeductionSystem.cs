using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;

namespace Marooned.Systems
{
    /// <summary>
    /// The one place allowed to translate ground-truth NpcState into what a player
    /// (or the AI agent controlling the player through MCP) is actually allowed to
    /// know. Also resolves accusations. Max wrong-accusation cap enforces the loss
    /// condition described in the design doc.
    /// </summary>
    public class DeductionSystem
    {
        private const int MaxWrongAccusations = 3;

        private readonly NpcDirectorSystem _npcDirector;
        private readonly PlayerSurvivalState _player;
        private readonly Dictionary<string, IllnessDef> _illnessDefs;

        public DeductionSystem(NpcDirectorSystem npcDirector, GameStateProvider stateProvider, LubanDataService dataService)
        {
            _npcDirector = npcDirector;
            _player = stateProvider.GetPlayer();
            _illnessDefs = dataService.IllnessDefs;
        }

        /// <summary>Build the safe, MCP-facing view of every NPC the player can currently see (same location).</summary>
        public List<NpcObservableView> GetObservableNpcsAt(string locationId)
        {
            var result = new List<NpcObservableView>();
            foreach (var npc in _npcDirector.Npcs.Values.Where(n => n.CurrentLocationId == locationId))
            {
                result.Add(new NpcObservableView
                {
                    Id = npc.Id,
                    IsAlive = npc.IsAlive,
                    CurrentLocationId = npc.CurrentLocationId,
                    Activity = npc.Activity,
                    VisibleConditionCardIds = FilterVisible(npc.AllConditionCardIds),
                    Avatar = npc.Avatar
                });
            }
            return result;
        }

        private List<string> FilterVisible(List<string> conditionCardIds)
        {
            // Clues (blood stain, scratch mark) default to visible; illness cards check IllnessDef.Visible.
            return conditionCardIds
                .Where(id => !_illnessDefs.TryGetValue(id, out var def) || def.Visible)
                .ToList();
        }

        public (bool wasCorrect, bool win, bool loss, string resultText) Accuse(string targetNpcId)
        {
            if (!_npcDirector.Npcs.TryGetValue(targetNpcId, out var target))
                return (false, false, false, "unknown_npc");

            var correct = target.Role == NpcRole.Killer;

            if (correct)
            {
                target.IsAlive = false; // removed from play
                var anyKillersLeft = _npcDirector.Npcs.Values.Any(n => n.IsAlive && n.Role == NpcRole.Killer);
                return (true, !anyKillersLeft, false, anyKillersLeft ? "correct_but_more_killers_remain" : "all_killers_caught_win");
            }

            _player.WrongAccusations++;
            _player.Mood = System.Math.Max(0, _player.Mood - 15f);
            var lost = _player.WrongAccusations >= MaxWrongAccusations;
            return (false, false, lost, lost ? "too_many_wrong_accusations_loss" : "wrong_accusation");
        }
    }
}
