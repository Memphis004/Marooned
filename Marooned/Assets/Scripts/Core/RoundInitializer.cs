using System.Collections.Generic;
using System.Linq;
using Marooned.Systems;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    /// <summary>
    /// ปิดช่องว่างจุดที่ 1: ไม่มีใครเรียก NpcDirectorSystem.SetupRound()
    /// ทำให้ Npcs ว่างเปล่า และ MCP Tools อย่าง get_visible_npcs / call_meeting
    /// คืนค่าว่างเสมอ
    ///
    /// ติดตั้งบน GameObject ลูกของ GameLifetimeScope (เช่น GameManager) เพื่อให้
    /// Resolve ระบบจาก container ผ่าน code ได้เลย ไม่ต้องลาก reference ใน Inspector
    ///
    /// หมายเหตุ: roster ชื่อ NPC ยังเป็น mock (ยังไม่มีตาราง Luban NpcDef) —
    /// เมื่อมีตารางแล้วให้ย้ายไปอ่านจาก LubanDataService แทนค่า hardcode ตรงนี้
    /// </summary>
    public class RoundInitializer : MonoBehaviour
    {
        [Header("Round Setup (default ตามอัตราส่วน 5 NPC -> 1 Killer)")]
        [SerializeField] private int npcCount = 5;
        [SerializeField] private int killerCount = 1;

        private NpcDirectorSystem _npcDirector;
        private GameStateProvider _stateProvider;
        private LubanDataService _data;

        private void Start()
        {
            // VContainer build container ใน LifetimeScope.Awake — resolve ตอน Start
            // เพื่อกัน execution-order race กับตัว scope เอง
            var scope = GetComponentInParent<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogError("[RoundInitializer] หา GameLifetimeScope ไม่เจอ — ให้ติดสคริปต์นี้บน GameObject ลูกของ GameLifetimeScope");
                enabled = false;
                return;
            }

            _npcDirector = scope.Container.Resolve<NpcDirectorSystem>();
            _stateProvider = scope.Container.Resolve<GameStateProvider>();
            _data = scope.Container.Resolve<LubanDataService>();

            // กัน SetupRound ซ้ำ (เช่น ถ้ามีระบบอื่นเริ่มรอบไปก่อนแล้ว)
            if (_npcDirector.Npcs.Count > 0)
            {
                Debug.Log($"[RoundInitializer] มี NPC อยู่แล้ว {_npcDirector.Npcs.Count} ตัว — ข้ามการ SetupRound");
                return;
            }

            SetupRound();
        }

        /// <summary>สร้างรอบใหม่: จัด Role (Killer/Innocent) แล้ววาง NPC ลง location เริ่มต้น</summary>
        private void SetupRound()
        {
            var npcIds = new List<string>();
            for (int i = 1; i <= npcCount; i++)
                npcIds.Add($"npc_{i:D2}");

            _npcDirector.SetupRound(npcIds, killerCount);

            AssignStartingLocations();

            Debug.Log($"[RoundInitializer] SetupRound เสร็จ: {npcCount} NPC ({killerCount} killer)");
        }

        /// <summary>
        /// SetupRound() ของ NpcDirectorSystem ยังไม่กำหนด CurrentLocationId — แต่
        /// DeductionSystem.GetObservableNpcsAt() กรองด้วย location ดังนั้นต้องวาง
        /// NPC ลง location ก่อน ไม่งั้น get_visible_npcs ยังว่างอยู่
        /// (Lab B: ใช้ NpcDirectorSystem.MoveNpc แทนการ set ตรง เพื่อให้
        /// publish NpcLocationChangedMessage ทุกครั้ง — Visual layer spawn chibi ตาม event)
        /// </summary>
        private void AssignStartingLocations()
        {
            var playerLocation = _stateProvider.GetPlayer().CurrentLocationId;

            foreach (var npc in _npcDirector.Npcs.Values)
            {
                // ห้าม set CurrentLocationId ตรง — ให้ MoveNpc เป็นคน set + publish
                // (ทุกตัวอยู่กับผู้เล่นเลย เทสง่าย)
                // Lab C Phase 2 (Position Seeding Rule): MoveNpc ยัง seed PositionX/Y
                // = LocationDef.WorldX/Y ของปลายทางให้ด้วย — จุดเริ่มของ NPC จึงถูกต้อง
                // ตั้งแต่เฟรมแรก ไม่ warp ไป (0,0)
                _npcDirector.MoveNpc(npc.Id, playerLocation);
            }
        }
    }
}
