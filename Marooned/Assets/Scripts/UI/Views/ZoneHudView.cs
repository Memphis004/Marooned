using System;
using Marooned.Core;
using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Marooned.UI.Views
{
    /// <summary>
    /// Lab B Phase 6.1 — HUD บอกโซนปัจจุบัน (กึ่งกลางขอบบนจอ): passive MVP Lite
    /// view — resolve dependencies เองจาก GameLifetimeScope ใน Start() (pattern
    /// เดียวกับ ZoneTransitionTrigger/RoundInitializer) แล้ว subscribe
    /// PlayerLocationChangedMessage → อัปเดตข้อความเป็น
    /// LocationDefs[id].DisplayName (fallback เป็น id)
    ///
    /// แสดงค่าเริ่มต้นทันทีตอน Start (ไม่รอ message แรก)
    ///
    /// หมายเหตุ: Canvas ใน SampleScene เป็น scene root (ไม่ได้อยู่ใต้
    /// GameLifetimeScope) จึง fallback หา scope ด้วย FindAnyObjectByType ก่อน
    /// แล้วจึง fail — วาง view นี้ใต้ Canvas หรือใต้ scope ก็ทำงานได้ทั้งคู่
    /// </summary>
    public class ZoneHudView : MonoBehaviour
    {
        [SerializeField] private Text zoneText; // optional — auto-create ใต้ Canvas ถ้าไม่ผูก (pattern เดียวกับ CardHandView feedback)

        private GameStateProvider _stateProvider;
        private LubanDataService _data;
        private IDisposable _subscription;

        private void Start()
        {
            // VContainer: container build ใน LifetimeScope.Awake → resolve ใน Start
            var scope = GetComponentInParent<GameLifetimeScope>();
            if (scope == null) scope = FindAnyObjectByType<GameLifetimeScope>(); // Canvas เป็น scene root — ไม่มี scope ใน ancestry
            if (scope == null)
            {
                Debug.LogError("[ZoneHudView] หา GameLifetimeScope ไม่เจอทั้งใน hierarchy และใน scene — ปิดทำงาน", this);
                enabled = false;
                return;
            }

            _stateProvider = scope.Container.Resolve<GameStateProvider>();
            _data = scope.Container.Resolve<LubanDataService>();
            var subscriber = scope.Container.Resolve<ISubscriber<PlayerLocationChangedMessage>>();
            _subscription = subscriber.Subscribe(msg => UpdateZoneText(msg.NewLocationId));

            // แสดงค่าเริ่มต้นทันทีตอน Start (ไม่รอ message แรก)
            EnsureZoneText();
            UpdateZoneText(_stateProvider.GetPlayer().CurrentLocationId);
        }

        private void OnDestroy() => _subscription?.Dispose();

        private void UpdateZoneText(string locationId)
        {
            if (zoneText == null) return;
            if (_data != null
                && _data.LocationDefs.TryGetValue(locationId, out var def)
                && !string.IsNullOrEmpty(def.DisplayName))
            {
                zoneText.text = def.DisplayName;
            }
            else
            {
                zoneText.text = locationId; // fallback เป็น id เมื่อไม่มี def / ชื่อว่าง
            }
        }

        /// <summary>หา Text จาก Inspector ก่อน — ไม่มี then สร้างเองใต้ Canvas (built-in font, pattern เดียวกับ CardHandView.EnsureFeedbackText)</summary>
        private void EnsureZoneText()
        {
            if (zoneText != null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[ZoneHudView] หา Canvas ไม่เจอ — แสดงชื่อโซนทาง Console แทน", this);
                return;
            }

            var go = new GameObject("ZoneHudText", typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f); // กึ่งกลางแนวนอน ชิดขอบบน
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -20f);
            rt.sizeDelta = new Vector2(600f, 44f);

            zoneText = go.GetComponent<Text>();
            zoneText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Unity 6 built-in font
            zoneText.fontSize = 26;
            zoneText.alignment = TextAnchor.MiddleCenter;
            zoneText.color = Color.white;
            go.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.85f); // outline กันพื้นหลังสว่าง
        }
    }
}
