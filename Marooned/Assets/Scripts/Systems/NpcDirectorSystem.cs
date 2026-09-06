using System;
using System.Collections.Generic;
using System.Linq;
using Marooned.Shared;
using MessagePipe;

namespace Marooned.Systems
{
    /// <summary>
    /// Owns every NpcState (ground truth, including hidden Role). Moves NPCs between
    /// locations, lets the Killer NPC attempt eliminations when unwitnessed, and
    /// spawns Clue cards on the resulting body / nearby NPCs.
    ///
    /// IMPORTANT: never hand out NpcState directly to MCP query handlers — always go
    /// through DeductionSystem.BuildObservableView (see DeductionVisibilityRules).
    /// </summary>
    public class NpcDirectorSystem
    {
        private readonly Dictionary<string, NpcState> _npcs = new();
        private readonly Dictionary<string, LocationDef> _locations;
        private readonly IPublisher<NpcEliminatedMessage> _eliminatedPublisher;
        private readonly IPublisher<NpcLocationChangedMessage> _npcLocationPublisher;
        private readonly Random _rng = new();

        public IReadOnlyDictionary<string, NpcState> Npcs => _npcs;

        public NpcDirectorSystem(LubanDataService dataService, IPublisher<NpcEliminatedMessage> eliminatedPublisher,
            IPublisher<NpcLocationChangedMessage> npcLocationPublisher)
        {
            _locations = dataService.LocationDefs;
            _eliminatedPublisher = eliminatedPublisher;
            _npcLocationPublisher = npcLocationPublisher;
        }

        /// <summary>
        /// ย้าย NPC ไป location ใหม่ + Publish NpcLocationChangedMessage (Lab B)
        /// ให้ Visual layer (เช่น ChibiSpawnerView) subscribe แทนการ polling
        /// เป็นจุดเดียวที่อนุญาตให้ mutate CurrentLocationId — ระบบอื่นต้องเรียก method นี้
        /// </summary>
        public void MoveNpc(string npcId, string newLocationId)
        {
            if (!_npcs.TryGetValue(npcId, out var npc)) return;

            var oldLocationId = npc.CurrentLocationId;
            if (oldLocationId == newLocationId) return;

            npc.CurrentLocationId = newLocationId;
            _npcLocationPublisher.Publish(new NpcLocationChangedMessage
            {
                NpcId = npcId,
                OldLocationId = oldLocationId,
                NewLocationId = newLocationId
            });
        }

        /// <summary>
        /// Sets up a round: picks killerCount out of npcIds to be Killer, rest Innocent.
        /// Ratio guidance (confirmed): ~1 killer per 4-8 innocents, Among Us style
        /// (e.g. 5 NPC -> 1 killer, 10 NPC -> 2 killers).
        /// </summary>
        public void SetupRound(IEnumerable<string> npcIds, int killerCount)
        {
            _npcs.Clear();
            var ids = npcIds.ToList();
            var killerIds = ids.OrderBy(_ => _rng.Next()).Take(killerCount).ToHashSet();

            foreach (var id in ids)
            {
                _npcs[id] = new NpcState
                {
                    Id = id,
                    Role = killerIds.Contains(id) ? NpcRole.Killer : NpcRole.Innocent,
                    IsAlive = true,
                    Activity = NpcActivityState.Idle
                };
            }
        }

        public void Tick(float deltaSeconds)
        {
            foreach (var npc in _npcs.Values.Where(n => n.IsAlive))
            {
                TickBehavior(npc, deltaSeconds);
                if (npc.Role == NpcRole.Killer)
                    TryAttemptElimination(npc, deltaSeconds);
            }
        }

        private void TickBehavior(NpcState npc, float deltaSeconds)
        {
            // Placeholder schedule: random idle/gather/rest/travel switching.
            // Replace with a proper daily-schedule table (Luban) in a later lab.
        }

        private void TryAttemptElimination(NpcState killer, float deltaSeconds)
        {
            killer.KillCooldownRemaining = Math.Max(0, killer.KillCooldownRemaining - deltaSeconds);
            if (killer.KillCooldownRemaining > 0) return;

            var sameLocation = _npcs.Values
                .Where(n => n.IsAlive && n.Id != killer.Id && n.CurrentLocationId == killer.CurrentLocationId)
                .ToList();

            // "No witness" rule: only killer + exactly one victim present, nobody else.
            if (sameLocation.Count != 1) return;

            var victim = sameLocation[0];
            if (_rng.NextDouble() > 0.15) return; // small per-tick chance, tune later

            victim.IsAlive = false;
            killer.KillCooldownRemaining = 180f; // seconds

            var clueIds = SpawnClues(killer, victim);
            _eliminatedPublisher.Publish(new NpcEliminatedMessage
            {
                VictimNpcId = victim.Id,
                LocationId = victim.CurrentLocationId,
                SpawnedClueCardIds = clueIds
            });
        }

        private List<string> SpawnClues(NpcState killer, NpcState victim)
        {
            // Simple v1: always drop one visible clue on the victim's location, and a
            // weaker chance of a red herring clue somewhere else. Replace with
            // ClueDef-driven weighted rolls once DataTables/ClueDef.csv is populated.
            var clues = new List<string> { "clue_blood_stain" };
            if (_rng.NextDouble() < 0.3) clues.Add("clue_scratch_mark");
            victim.AllConditionCardIds.AddRange(clues);
            return clues;
        }
    }
}
