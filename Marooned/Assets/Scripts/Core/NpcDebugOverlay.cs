#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Marooned.Core;
using Marooned.Shared;
using Marooned.Systems;
using Marooned.Systems.AI;
using UnityEngine;
using VContainer;

namespace Marooned.EditorTools
{
    /// <summary>
    /// Lab C Phase 2.5B — Debug overlay สำหรับดู ground truth ของ NPC แบบเรียลไทม์
    /// (Editor-only — Test D: build target อื่นต้องไม่มี error)
    ///
    ///  • กด F12 ใน Game view เพื่อ toggle (Legacy Input — ProjectSettings
    ///    activeInputHandler=2 เหมือน PlayerInputService)
    ///  • Resolve ระบบจาก GameLifetimeScope.Container แบบ lazy (ครั้งแรกที่เปิด overlay)
    ///    — ไม่แตะ DI ตอน Awake ให้ play ที่ยังไม่มี NPC ผ่านได้
    ///  • แสดง: Id | Role | Loc | Pos | Act | Hunger | AI
    ///     - Hunger อ่านจาก npc.Survival.Hunger ตรง ๆ (NpcSurvivalSystem เป็นคน tick
    ///       อยู่แล้ว — overlay ไม่ implement decay เอง เพียงแค่ mirror ground truth)
    ///     - AI column: innocent → InnocentUtilityAI.GetCurrentActionId
    ///                  killer   → KillerPlanner.GetCurrentPhase
    ///     - แถม transition phase สั้น ๆ เมื่อกำลังข้ามโซน (WALK/EXIT/ENTER)
    ///
    ///  Information Hiding: หน้าที่ overlay เป็น DEBUG ใน Editor เท่านั้น — แสดง
    ///  ground truth โดยตรง (รวมค่าที่ผู้เล่นต้องไม่เห็น) จึงต้องถูกคอมไพล์ออกจาก
    ///  build จริงเสมอ (#if UNITY_EDITOR) และห้ามใช้ค่าจากที่นี่ไปตัดสินใจแทนระบบ
    ///
    ///  Setup: เพิ่ม component นี้บน GameObject เดียวกับ GameLifetimeScope
    ///  ใน SampleScene (ถ้าลืม จะ fallback หาด้วย FindFirstObjectByType)
    /// </summary>
    public class NpcDebugOverlay : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.F12;

        /// <summary>สถานะเปิด/ปิดปัจจุบัน (เทสอ่านได้ — F12/Toggle เรียกสลับ)</summary>
        public bool Visible { get; private set; }

        GameLifetimeScope _scope;
        NpcDirectorSystem _director;
        InnocentUtilityAI _innocentAi;
        KillerPlanner _killerPlanner;
        string _resolveError;

        GUIStyle _style;
        string _guiText = "";

        void Awake()
        {
            _scope = GetComponent<GameLifetimeScope>();
            if (_scope == null)
                _scope = FindFirstObjectByType<GameLifetimeScope>();
        }

        void Update()
        {
            if (Input.GetKeyDown(ToggleKey) || Input.GetKeyDown(KeyCode.F12))
                Toggle();

            if (!Visible) { _guiText = ""; return; }

            if (!TryResolve()) { _guiText = $"[NpcDebugOverlay] resolve ไม่สำเร็จ: {_resolveError}"; return; }

            _guiText = BuildText();
        }

        /// <summary>สลับ overlay (F12 เรียก; เทสเรียกตรง ๆ ได้ — deterministic)</summary>
        public void Toggle() => Visible = !Visible;

        bool TryResolve()
        {
            if (_director != null) return true;
            if (_scope == null)
            {
                _scope = FindFirstObjectByType<GameLifetimeScope>();
                if (_scope == null) { _resolveError = "ไม่เจอ GameLifetimeScope ใน scene"; return false; }
            }
            try
            {
                _director = _scope.Container.Resolve<NpcDirectorSystem>();
                _innocentAi = _scope.Container.Resolve<InnocentUtilityAI>();
                _killerPlanner = _scope.Container.Resolve<KillerPlanner>();
                return true;
            }
            catch (System.Exception ex)
            {
                _resolveError = ex.Message;
                return false;
            }
        }

        string BuildText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("== NPC DEBUG (Editor-only — ground truth) ==");
            sb.AppendLine("Id        | Role     | Loc           | Pos          | Act       | Hunger | AI");
            foreach (var npc in _director.Npcs.Values)
            {
                if (!npc.IsAlive)
                {
                    sb.AppendLine($"{npc.Id,-9} | DEAD");
                    continue;
                }

                var ai = npc.Role == NpcRole.Killer
                    ? _killerPlanner.GetCurrentPhase(npc.Id).ToString()
                    : _innocentAi.GetCurrentActionId(npc.Id) ?? "(idle)";

                sb.AppendLine(
                    $"{npc.Id,-9} | {npc.Role,-8} | {npc.CurrentLocationId,-13} | " +
                    $"({npc.PositionX:0.0},{npc.PositionY:0.0}) | {npc.Activity,-9} | " +
                    $"{npc.Survival.Hunger,6:0} | {ai}{TransitionTag(npc)}");
            }
            return sb.ToString();
        }

        static string TransitionTag(NpcState npc) => npc.TransitionPhase switch
        {
            NpcTransitionPhase.WalkingToPoint => " ⇄WALK",
            NpcTransitionPhase.Exiting => " ⇄EXIT",
            NpcTransitionPhase.Entering => " ⇄ENTER",
            _ => ""
        };

        void OnGUI()
        {
            if (!Visible || string.IsNullOrEmpty(_guiText)) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    richText = false,
                };
                _style.normal.textColor = Color.white;
            }

            var bg = new GUIStyle(GUI.skin.box);
            var content = new GUIContent(_guiText);
            var size = _style.CalcSize(content);
            var rect = new Rect(10f, 10f, size.x + 16f, size.y + 12f);

            GUI.Box(rect, GUIContent.none, bg);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, size.x, size.y), _guiText, _style);
        }
    }
}
#endif
