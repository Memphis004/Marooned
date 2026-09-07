using System;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Marooned.Systems
{
    /// <summary>
    /// Lab B Phase 6 (Test C fix) — MCP request handlers ถูก invoke บน background
    /// thread ของ MessagePipe TCP receive loop แต่ chain ที่ handler เรียกใช้
    /// (เช่น publish CardInventoryChangedMessage → CardHandPresenter re-render,
    /// publish PlayerLocationChangedMessage → ChibiSpawnerView spawn chibi)
    /// ต้องแตะ Unity API ซึ่งทำได้เฉพาะ main thread — ไม่งั้นล้มด้วย
    /// "get_gameObject can only be called from the main thread"
    /// (จับได้จาก RemoteRequestException ฝั่ง bridge ตอนเทส explore_location)
    ///
    /// dispatcher นี้มาร์ชั่นงานไป main thread ผ่าน pump MonoBehaviour ที่ drain
    /// คิวใน Update — handler await ผลแล้วส่ง response กลับผ่าน TCP ตามปกติ
    /// (exception บน main thread จะถูกส่งกลับเป็น RemoteError ให้ bridge เห็นรายละเอียด)
    /// </summary>
    public class McpMainThreadDispatcher : IInitializable, IDisposable
    {
        private readonly ConcurrentQueue<Action> _queue = new();
        private McpMainThreadPump _pump;

        public void Initialize()
        {
            // host pump บน GameObject แยก (ไม่ผูก scene object) + อยู่รอดข้าม scene load
            var host = new GameObject("McpMainThreadPump");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _pump = host.AddComponent<McpMainThreadPump>();
            _pump.dispatcher = this;
        }

        public void Dispose()
        {
            if (_pump != null) UnityEngine.Object.Destroy(_pump.gameObject);
            _pump = null;
        }

        /// <summary>รัน work บน main thread (fire-and-forget)</summary>
        public void Post(Action work) => _queue.Enqueue(work);

        /// <summary>รัน work บน main thread แล้ว await ผล — ใช้ใน MCP handlers</summary>
        public UniTask<T> EnqueueAsync<T>(Func<T> work)
        {
            var tcs = new UniTaskCompletionSource<T>();
            _queue.Enqueue(() =>
            {
                try { tcs.TrySetResult(work()); }
                catch (Exception ex) { tcs.TrySetException(ex); }
            });
            return tcs.Task;
        }

        internal void Drain()
        {
            while (_queue.TryDequeue(out var work))
            {
                try { work(); }
                catch (Exception ex)
                {
                    // safety net — งานที่ wrap ด้วย EnqueueAsync จะ catch เองแล้ว
                    // ส่ง exception กลับ bridge; อันนี้กันงานแบบ fire-and-forget
                    Debug.LogError("[McpMainThreadDispatcher] work threw: " + ex);
                }
            }
        }
    }

    /// <summary>Pump ที่ drain คิวทุก Update — สร้างโดย dispatcher เอง</summary>
    public class McpMainThreadPump : MonoBehaviour
    {
        internal McpMainThreadDispatcher dispatcher;

        private void Update() => dispatcher?.Drain();
    }
}
