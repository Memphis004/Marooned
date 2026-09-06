using System;
using System.Collections.Generic;
using Marooned.Core.Visual;
using Marooned.Shared;
using Marooned.Systems;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Marooned.Core
{
    /// <summary>
    /// Lab B Phase 3 — View ของตัวผู้เล่น: apply ตำแหน่ง/animation จาก
    /// PlayerMovementSystem แบบ **direct state read ทุกเฟรม** (ไม่ pub/sub —
    /// การเดินเป็น continuous state ตามบทเรียน Lab 13) และ subscribe
    /// ItemPickedUpMessage (discrete event) เพื่อเล่น anim "pick up"
    ///
    /// VContainer: resolve ผ่าน GetComponentInParent&lt;GameLifetimeScope&gt;() เท่านั้น
    /// SerializeField รับแค่ visualPrefab (asset ล้วนๆ)
    /// ติดตั้งบน GameLifetimeScope/GameManager/PlayerCharacter
    /// </summary>
    public class PlayerCharacterView : MonoBehaviour
    {
        [Header("Prefab ล้วนๆ — '001 Student 1 Character' (สงวนให้ player เท่านั้น)")]
        [SerializeField] private GameObject visualPrefab;

        private GameStateProvider _stateProvider;
        private IChibiVisual _visual;

        // ล็อค animation ชั่วคราวตอนเล่น pick up (กัน Update ทับด้วย idle/walk ทันที)
        private float _animLockUntil;
        private readonly List<IDisposable> _subscriptions = new();

        private void Start()
        {
            // VContainer: container build ใน LifetimeScope.Awake → resolve ใน Start
            var scope = GetComponentInParent<GameLifetimeScope>();
            if (scope == null)
            {
                Debug.LogError("[PlayerCharacterView] หา GameLifetimeScope ไม่เจอ — ให้ติดบน GameObject ลูกของ GameLifetimeScope");
                enabled = false;
                return;
            }
            _stateProvider = scope.Container.Resolve<GameStateProvider>();

            // MessagePipe: subscribe discrete event เก็บของ → trigger anim
            var pickupSubscriber = scope.Container.Resolve<ISubscriber<ItemPickedUpMessage>>();
            _subscriptions.Add(pickupSubscriber.Subscribe(_ => OnItemPickedUp()));

            // spawn visual จาก prefab (Student 1 มี GenericCuteVisualController ติดมาแล้ว)
            var visualGo = Instantiate(visualPrefab, transform);
            visualGo.transform.localPosition = Vector3.zero;
            _visual = visualGo.GetComponent<IChibiVisual>();
            if (_visual == null)
                Debug.LogError("[PlayerCharacterView] visual prefab ไม่มี component ที่ implement IChibiVisual");
        }

        /// <summary>ทุกเฟรม: อ่าน state ตรงจาก PlayerSurvivalState (ไม่ polling ระบบอื่น)</summary>
        private void Update()
        {
            if (_stateProvider == null) return;
            var player = _stateProvider.Player;

            transform.position = new Vector3(player.PositionX, player.PositionY, 0f);

            if (Time.time < _animLockUntil || _visual == null) return;

            _visual.SetFacing(player.FacingRight);
            _visual.Bind(player.Activity); // Idle→idle, Traveling→walk
        }

        private void OnItemPickedUp()
        {
            if (_visual == null) return;
            _visual.PlayPickup();
            _animLockUntil = Time.time + 0.8f; // กัน Update ทับ one-shot anim
        }

        private void OnDestroy()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
    }
}
