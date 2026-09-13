using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cysharp.Threading.Tasks;
using Marooned.Core;
using Marooned.Shared;
using Marooned.Systems;
using NUnit.Framework;
using VContainer;
using UnityEngine;
using UnityEngine.TestTools;

namespace Marooned.EditorTools
{
    /// <summary>
    /// Peek-before-consume fix (ItemPickupSystem) — PlayMode evidence:
    ///
    /// ก่อนแก้: ItemPickupSystem (tick ก่อน NodeHarvestSystem ใน GameTickDriver)
    /// เรียก ConsumeInteractPressed() ทันทีเมื่อ _worldItems.ActiveItems.Count > 0
    /// แม้ผู้เล่นจะไม่ได้ยืนใกล้ไอเท็มบนพื้น → ปุ่ม E ถูก "กินทิ้ง" ฟรี
    /// NodeHarvestSystem ได้ false จึงเก็บ node (เช่น bush_berry) ไม่ได้เลย
    /// จนกว่าจะเก็บไอเท็มจนโซนว่าง (ActiveItems.Count == 0)
    ///
    /// หลังแก้: pickup "peek" ผ่าน IsInteractPressed ก่อน แล้วเช็ค
    /// TryGetNearest ในรัศมีจริง — ถ้าไม่มีไอเท็มใกล้ จะไม่ consume
    /// (ปล่อยปุ่มผ่านให้ NodeHarvestSystem ใช้ต่อในเฟรมเดียวกัน)
    ///
    ///  • Test A: ยืนใกล้ไอเท็ม + กด E → ปุ่มถูก consume + เก็บได้ตามปกติ
    ///    (regression guard ฝั่ง "กินเมื่อควรกิน")
    ///  • Test B: ยืนไกลไอเท็มทุกชิ้น + กด E → ปุ่ม "ไม่" ถูก consume —
    ///    IsInteractPressed คง true หลัง pickup tick และ ConsumeInteractPressed()
    ///    ยังคืน true ให้ผู้ใช้ถัดไป (NodeHarvestSystem)
    ///
    /// Deterministic: ตำแหน่ง loot ของโซนเป็นการสุ่ม จึง "ย้ายไอเท็มจริงทุกชิ้น
    /// ออกไกล" ก่อนทุกเคส (stash/restore) แล้ว seed ไอเท็มหลอกที่ตำแหน่งที่คุมได้
    /// ผ่าน _activeItems (reflection) — ไม่พึ่ง loot table ของโซนเลย
    /// </summary>
    public class ItemPickupPeekPlayModeTests
    {
        public const string EvidenceDir = "TestEvidence/itempickup-peek";

        static readonly BindingFlags PrivateFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly Vector2 FarOffset = new(50f, 50f); // offset จากผู้เล่น — ไกลกว่า PickupRadius หลายเท่า

        GameLifetimeScope _scope;
        GameStateProvider _stateProvider;
        PlayerInputService _input;
        ItemPickupSystem _pickup;
        WorldItemSystem _worldItems;
        PlayerSurvivalState _player;
        readonly StringBuilder _log = new();

        [UnitySetUp]
        public IEnumerator SetUp() => UniTask.ToCoroutine(async () =>
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene")
            {
                var load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SampleScene");
                while (load != null && !load.isDone) await UniTask.Yield();
            }

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                _scope = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>();
                if (_scope != null && Application.isPlaying)
                {
                    _stateProvider = _scope.Container.Resolve<GameStateProvider>();
                    if (_stateProvider != null) break;
                }
                await UniTask.Yield();
            }
            Assert.That(_scope != null, "GameLifetimeScope ไม่เจอใน play mode");

            _player = _stateProvider.GetPlayer();
            _input = _scope.Container.Resolve<PlayerInputService>();
            _pickup = _scope.Container.Resolve<ItemPickupSystem>();
            _worldItems = _scope.Container.Resolve<WorldItemSystem>();

            // deterministic: ล็อค input override + ค่าระบบเป็นค่าที่เทสคาดหวัง
            _input.EnableTestInputOverride();
            _pickup.Mode = PickupMode.InteractKey;
            _pickup.PickupRadius = 1.6f;
            _input.ConsumeInteractPressed(); // เคลียร์ edge-flag ค้างจากเทสก่อนหน้า
            await UniTask.Yield();
        });

        [UnityTest]
        public IEnumerator E_WhenItemInRange_IsConsumed_AndItemPicked() => UniTask.ToCoroutine(async () =>
        {
            _log.Clear();
            var playerPos = new Vector2(_player.PositionX, _player.PositionY);

            // ย้ายไอเท็มจริงของโซนออกไกล (ตำแหน่ง loot สุ่ม — ไม่ควรมาแย่ง nearest)
            var originals = StashRealItemsAway(playerPos + FarOffset);
            // seed: ไอเท็มหลอกวางในรัศมี (0.5 world unit จากผู้เล่น)
            var item = CreateLooseItem(playerPos + new Vector2(0.5f, 0f));

            try
            {
                _input.InjectTestInput(Vector2.zero, true); // กด E (edge-trigger)
                _pickup.Tick(0.016f); // tick pickup แบบ manual (ไม่มี yield ระหว่างนี้ — driver แทรก tick ไม่ได้)

                Assert.IsFalse(_input.IsInteractPressed,
                    "อยู่ในรัศมี → pickup ต้องกินปุ่ม (peek แล้ว commit)");
                Assert.IsFalse(_input.ConsumeInteractPressed(),
                    "กินไปแล้ว consume ซ้ำต้อง false (edge-trigger กดครั้งเดียว = ใช้ครั้งเดียว)");
                Assert.IsTrue(item.Picked, "ไอเท็มในรัศมีต้องโดนเก็บ");

                _log.AppendLine($"[A] item @ {item.Position}, player @ {playerPos}, radius {_pickup.PickupRadius} → consumed + picked → PASS");
                WriteEvidence("E_WhenItemInRange_IsConsumed_AndItemPicked", _log.ToString());
            }
            finally
            {
                CleanupLooseItem(item); // ถ้า assert fail ก่อนโดนเก็บ — ต้องเก็บกวาดเอง
                RestoreRealItems(originals);
            }
            await UniTask.Yield();
        });

        [UnityTest]
        public IEnumerator E_WhenNoItemInRange_IsNotConsumed_PassesThrough() => UniTask.ToCoroutine(async () =>
        {
            _log.Clear();
            var playerPos = new Vector2(_player.PositionX, _player.PositionY);

            // ย้ายไอเท็มจริงทุกชิ้นออกห่างเกินรัศมี (จดตำแหน่งเดิมไว้คืนหลังจบ)
            var originals = StashRealItemsAway(playerPos + FarOffset);
            // seed: ไอเท็มหลอก "ไกล" 1 ชิ้น — บังคับว่า ActiveItems.Count > 0
            // เพื่อให้ Tick ไปถึงบรรทัดเช็คระยะจริง (ไม่ early-return ตั้งแต่บรรทัดแรก)
            var farItem = CreateLooseItem(playerPos + FarOffset);

            try
            {
                _input.InjectTestInput(Vector2.zero, true); // กด E แต่ไม่มีไอเท็มใกล้
                _pickup.Tick(0.016f); // ← จุดที่เคยกินปุ่มทิ้งก่อนแก้

                Assert.IsTrue(_input.IsInteractPressed,
                    "peek fix: pickup ต้อง 'ไม่' กินปุ่มเมื่อไม่มีไอเท็มในรัศมี (IsInteractPressed คง true)");
                Assert.IsTrue(_input.ConsumeInteractPressed(),
                    "ปุ่มต้องรอดไปถึงผู้ใช้ถัดไป (NodeHarvestSystem tick ถัดมาในเฟรมเดียวกัน ต้องได้ true)");
                Assert.IsFalse(_input.ConsumeInteractPressed(),
                    "consume รอบสองต้อง false — edge-trigger ถูกใช้ไปแล้วครั้งเดียว");
                Assert.IsFalse(farItem.Picked, "ไอเท็มไกลต้องไม่โดนเก็บ");

                _log.AppendLine($"[B] {originals.Count} real + 1 far loose item moved away, player @ {playerPos}, radius {_pickup.PickupRadius} → press survived → PASS");
                WriteEvidence("E_WhenNoItemInRange_IsNotConsumed_PassesThrough", _log.ToString());
            }
            finally
            {
                CleanupLooseItem(farItem);
                RestoreRealItems(originals);
            }
            await UniTask.Yield();
        });

        // ---- helpers ----

        /// <summary>list _activeItems จริงของ WorldItemSystem (reflection)</summary>
        List<WorldItemSystem.ItemState> ActiveItemList()
        {
            var field = typeof(WorldItemSystem).GetField("_activeItems", PrivateFlags);
            Assert.That(field != null, "_activeItems field ไม่เจอ (WorldItemSystem ถูก refactor?)");
            return (List<WorldItemSystem.ItemState>)field.GetValue(_worldItems);
        }

        /// <summary>ย้ายไอเท็มจริงทุกชิ้น (state + GameObject) ออกไกล — คืน list ตำแหน่งเดิมเพื่อ restore</summary>
        List<(WorldItemSystem.ItemState Item, Vector2 Pos)> StashRealItemsAway(Vector2 farPoint)
        {
            var originals = new List<(WorldItemSystem.ItemState, Vector2)>();
            foreach (var it in _worldItems.ActiveItems)
            {
                originals.Add((it, it.Position));
                it.Position = farPoint;
                if (it.GameObject != null) it.GameObject.transform.position = farPoint;
            }
            return originals;
        }

        /// <summary>คืนตำแหน่งไอเท็มจริงของโซน — กัน pollution ไปเทสอื่น</summary>
        void RestoreRealItems(List<(WorldItemSystem.ItemState Item, Vector2 Pos)> originals)
        {
            foreach (var (it, pos) in originals)
            {
                it.Position = pos;
                if (it.GameObject != null) it.GameObject.transform.position = pos;
            }
        }

        /// <summary>สร้างไอเท็มหลอก 1 ชิ้นยัดเข้า _activeItems ของ WorldItemSystem จริง (มี GameObject ให้ TryGetNearest หาเจอ)</summary>
        WorldItemSystem.ItemState CreateLooseItem(Vector2 position)
        {
            var state = new WorldItemSystem.ItemState
            {
                CardId = "food_coconut", // มีใน CardDefs แน่นอน (อยู่ใน ZoneLootTable ของ beach)
                Position = position,
                Picked = false,
                GameObject = new GameObject("peek_test_item"),
            };
            state.GameObject.transform.position = position;
            ActiveItemList().Add(state);
            return state;
        }

        /// <summary>เก็บกวาดไอเท็มหลอกออกจาก _activeItems + destroy GameObject (กันค้างข้ามเทส)</summary>
        void CleanupLooseItem(WorldItemSystem.ItemState item)
        {
            if (item == null) return;
            ActiveItemList().Remove(item);
            if (item.GameObject != null)
            {
                UnityEngine.Object.Destroy(item.GameObject);
                item.GameObject = null;
            }
        }

        private void WriteEvidence(string test, string body)
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", EvidenceDir));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"{test}.txt"),
                $"=== {test} — {DateTime.Now:HH:mm:ss} ===\n{body}\nRESULT: PASS\n");
            Debug.Log($"[ItemPickupPeekPlayMode] {test} PASS — evidence: {EvidenceDir}/");
        }
    }
}
