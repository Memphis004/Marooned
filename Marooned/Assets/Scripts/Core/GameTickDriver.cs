using Marooned.Systems;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    /// <summary>
    /// ปิดช่องว่างจุดที่ 2: ระบบหลังบ้านไม่มี Driver เรียก Tick() เกมจึงหยุดนิ่ง
    ///
    /// เป็น MonoBehaviour ที่เรียก Tick ของระบบ gameplay ทุกเฟรมใน Update():
    ///   - SurvivalStatSystem.Tick(float deltaSeconds)
    ///   - NpcDirectorSystem.Tick(float deltaSeconds)  (killer AI / กำหนดเวลา NPC — ตั้ง target)
    ///   - NpcMovementSystem.Tick(float deltaSeconds)  (เดิน NPC เข้าหา target — ต้องมาหลัง Director เสมอ)
    ///   - WorldEventSystem.Tick(float deltaSeconds, string currentLocationTag)
    ///
    /// โปรเจกต์ยังไม่มี interface แบบ ITickable เอง จึงเรียกตรงตาม signature จริง
    /// ระบบทั้งหมดถูก Resolve จาก VContainer (ไม่ลากใส่ Inspector)
    /// ติดตั้งบน GameObject ลูกของ GameLifetimeScope (เช่น GameManager)
    /// </summary>
    public class GameTickDriver : MonoBehaviour
    {
        private SurvivalStatSystem _survival;
        private NpcDirectorSystem _npcDirector;
        private NpcMovementSystem _npcMovement;
        private NpcZoneTransitionSystem _npcZoneTransition;
        private WorldEventSystem _worldEvents;
        private GameStateProvider _stateProvider;
        private PlayerInputService _playerInput;
        private PlayerMovementSystem _playerMovement;
        private ItemPickupSystem _itemPickup;
        private NodeHarvestSystem _nodeHarvest;

        private bool _resolved;

        private void Awake()
        {
            // GameLifetimeScope (parent) build container ใน Awake ซึ่งรันก่อน Awake
            // ของ GameObject ลูก — ลอง resolve เลย ถ้ายังไม่สำเร็จจะลองอีกครั้งใน Start
            TryResolveSystems();
        }

        private void Start()
        {
            if (!_resolved) TryResolveSystems();
        }

        private bool TryResolveSystems()
        {
            var scope = GetComponentInParent<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogError("[GameTickDriver] หา GameLifetimeScope ไม่เจอ — ให้ติดสคริปต์นี้บน GameObject ลูกของ GameLifetimeScope");
                enabled = false;
                return false;
            }

            _survival = scope.Container.Resolve<SurvivalStatSystem>();
            _npcDirector = scope.Container.Resolve<NpcDirectorSystem>();
            _npcMovement = scope.Container.Resolve<NpcMovementSystem>();
            _npcZoneTransition = scope.Container.Resolve<NpcZoneTransitionSystem>();
            _worldEvents = scope.Container.Resolve<WorldEventSystem>();
            _stateProvider = scope.Container.Resolve<GameStateProvider>();
            _playerInput = scope.Container.Resolve<PlayerInputService>();
            _playerMovement = scope.Container.Resolve<PlayerMovementSystem>();
            _itemPickup = scope.Container.Resolve<ItemPickupSystem>();
            _nodeHarvest = scope.Container.Resolve<NodeHarvestSystem>();
            _resolved = true;
            Debug.Log("[GameTickDriver] Resolve ระบบครบแล้ว — เริ่ม Tick ทุกเฟรม");
            return true;
        }

        private void Update()
        {
            if (!_resolved) return;

            float deltaSeconds = Time.deltaTime;

            // 1) input ก่อน (ให้ movement/pickup อ่านค่าเฟรมนี้)
            _playerInput.Tick();

            // ลำดับ: stat ผู้เล่น -> เดิน -> เก็บของ -> พฤติกรรม NPC -> world event
            _survival.Tick(deltaSeconds);
            _playerMovement.Tick(deltaSeconds);
            _itemPickup.Tick(deltaSeconds);
            // Lab C Phase 1: เก็บเกี่ยว node — อยู่หลัง pickup เพื่อให้กด E ครั้งเดียว
            // เก็บไอเท็มพื้นก่อน (ถ้ามี) แล้วจึงโดน node (กันของทั้งสองระบบโดยกดเดียว)
            _nodeHarvest.Tick(deltaSeconds);

            // Lab C Phase 2: ⚠️ Tick order — NpcDirectorSystem (AI ตั้ง target) ก่อน
            // NpcMovementSystem (เดิน) ในเฟรมเดียวกัน target ที่ AI ตั้งในเฟรมนี้ต้อง
            // ถูกเดินทันที ไม่ดีเลย์ 1 เฟรม
            _npcDirector.Tick(deltaSeconds);
            _npcMovement.Tick(deltaSeconds);
            // Hybrid Transition Points (Part 2): หลัง Movement เสมอ — Movement เดิน
            // NPC ถึงจุดเชื่อม แล้วระบบนี้เปลี่ยน phase/ข้ามโซนทันทีในเฟรมเดียว
            _npcZoneTransition.Tick(deltaSeconds);

            // WorldEventSystem.Tick ต้องการ location tag ปัจจุบัน — ใช้ id ของ
            // location ผู้เล่นเป็น tag ไปก่อน (mock events ไม่ได้กำหนด RequiredLocationTags)
            _worldEvents.Tick(deltaSeconds, _stateProvider.GetPlayer().CurrentLocationId);
        }
    }
}
