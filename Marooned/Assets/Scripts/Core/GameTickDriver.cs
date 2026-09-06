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
    ///   - NpcDirectorSystem.Tick(float deltaSeconds)  (killer AI / กำหนดเวลา NPC)
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
        private WorldEventSystem _worldEvents;
        private GameStateProvider _stateProvider;

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
            _worldEvents = scope.Container.Resolve<WorldEventSystem>();
            _stateProvider = scope.Container.Resolve<GameStateProvider>();
            _resolved = true;
            Debug.Log("[GameTickDriver] Resolve ระบบครบแล้ว — เริ่ม Tick ทุกเฟรม");
            return true;
        }

        private void Update()
        {
            if (!_resolved) return;

            float deltaSeconds = Time.deltaTime;

            // ลำดับ: stat ผู้เล่น -> พฤติกรรม NPC -> world event
            // (ลำดับนี้ยังไม่มีผลเชิงระบบตอนนี้ แต่ fix ไว้ให้ deterministic)
            _survival.Tick(deltaSeconds);
            _npcDirector.Tick(deltaSeconds);

            // WorldEventSystem.Tick ต้องการ location tag ปัจจุบัน — ใช้ id ของ
            // location ผู้เล่นเป็น tag ไปก่อน (mock events ไม่ได้กำหนด RequiredLocationTags)
            _worldEvents.Tick(deltaSeconds, _stateProvider.Player.CurrentLocationId);
        }
    }
}
