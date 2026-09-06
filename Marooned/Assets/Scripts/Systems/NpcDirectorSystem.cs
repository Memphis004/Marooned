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
    /// Phase 4 (Player-as-Killer): TryEliminate/CanEliminate เป็น method กลางที่ทั้ง
    /// AI killer (Tick) และ player (weapon card ผ่าน UseCardHandler) ใช้ร่วมกัน
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

        /// <summary>
        /// Phase 4 (Player-as-Killer): เช็คเงื่อนไขการ eliminate โดยไม่ mutate state ใดๆ (dry-run)
        /// — UseCardHandler เรียกก่อนหักการ์ด เพื่อกันการ์ดหายฟรีเมื่อลงมือไม่สำเร็จ (Safe UX)
        /// </summary>
        public (bool success, string reason) CanEliminate(string killerEntityId, string victimNpcId, string killerLocationId)
        {
            if (!_npcs.TryGetValue(victimNpcId, out var victim))
                return (false, "unknown_target");
            if (!victim.IsAlive)
                return (false, "target_already_dead");
            if (victim.CurrentLocationId != killerLocationId)
                return (false, "target_not_same_location");

            // กฎ "no witness" — นับ NPC อื่นที่มีชีวิตใน location เดียวกัน (ยกเว้น killer + victim)
            var witnessCount = _npcs.Values.Count(n =>
                n.IsAlive && n.Id != victimNpcId && n.Id != killerEntityId
                && n.CurrentLocationId == killerLocationId);
            if (witnessCount > 0)
                return (false, "witnessed");

            return (true, null);
        }

        /// <summary>
        /// Method กลางสำหรับ eliminate (ทั้ง AI killer และ player ผ่าน weapon card):
        /// ตรวจเงื่อนไขผ่าน CanEliminate ก่อน แล้วค่อย mutate state + spawn clues + publish
        /// NpcEliminatedMessage (event เดียวกันสำหรับทุก killer — visual layer ไม่ต้องรู้ต้นตอ)
        /// </summary>
        public (bool success, string reason) TryEliminate(string killerEntityId, string victimNpcId, string killerLocationId)
        {
            var (can, reason) = CanEliminate(killerEntityId, victimNpcId, killerLocationId);
            if (!can) return (false, reason);

            var victim = _npcs[victimNpcId]; // CanEliminate รับประกัน key มีอยู่แล้ว
            victim.IsAlive = false;

            var clueIds = SpawnClues(killerEntityId, victim);
            _eliminatedPublisher.Publish(new NpcEliminatedMessage
            {
                VictimNpcId = victim.Id,
                LocationId = victim.CurrentLocationId,
                SpawnedClueCardIds = clueIds
            });
            return (true, null);
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

            // Phase 4: ลงมือผ่าน TryEliminate กลาง (กฎ witness/clue/publish อยู่ที่เดียวกับ player)
            var (success, _) = TryEliminate(killer.Id, victim.Id, killer.CurrentLocationId);
            if (success)
                killer.KillCooldownRemaining = 180f; // seconds
        }

        private List<string> SpawnClues(string killerEntityId, NpcState victim)
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
