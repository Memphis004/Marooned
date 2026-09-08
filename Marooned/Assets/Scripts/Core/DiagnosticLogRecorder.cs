using System;
using System.Collections.Concurrent;
using System.Linq;
using UnityEngine;

namespace Marooned.Core
{
    /// <summary>
    /// Lab C test evidence helper — buffers Debug.Log lines ของ play session
    /// เพื่อให้ BiomeScatterTestRunner (Editor) ดึงไปเขียนเป็นหลักฐานได้
    /// (Console ของ Unity เคลียร์ทุก play และ copy มือลำบาก — อันนี้ทำให้อัตโนมัติ)
    /// on/off ได้ (Enabled) และเก็บไม่เกิน MaxLines กัน memory บวม
    /// </summary>
    public static class DiagnosticLogRecorder
    {
        private const int MaxLines = 4000;
        private static readonly ConcurrentQueue<string> Lines = new();

        /// <summary>true = เก็บ log เข้าคิว (เปิดอัตโนมัติตั้งแต่เข้า play mode)</summary>
        public static bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Hook()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!Enabled) return;
            Lines.Enqueue($"{DateTime.Now:HH:mm:ss.fff} [{type.ToString().ToUpperInvariant()}] {condition}");
            while (Lines.Count > MaxLines && Lines.TryDequeue(out _)) { }
        }

        /// <summary>ดึง log ทั้งหมดที่เก็บไว้ (snapshot) — คิวไม่ถูกเคลียร์</summary>
        public static string[] Snapshot() => Lines.ToArray();

        /// <summary>กรองเฉพาะ log ของระบบที่เทส (tag ใด ๆ ใน List)</summary>
        public static string[] FilterByTags(params string[] tags)
        {
            return Lines.Where(l => tags.Any(t => l.Contains(t))).ToArray();
        }
    }
}
