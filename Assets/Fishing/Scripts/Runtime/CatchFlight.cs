using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 획득 연출 — 기획서 §6-5.
    ///
    /// 조작이 거의 없는 게임에서 잡히는 순간이 유일한 결과 표시다. 그냥 사라지면 보상이 없다.
    /// ① 수면 위로 솟아오름 ② 바구니로 포물선 비행 ③ 입고
    ///
    /// 탑다운에서 "위로 올라간다"는 그림자로 보여준다.
    /// 물고기는 올라가고 그림자는 수면에 남으므로, 둘 사이가 벌어지면 그게 곧 높이다.
    /// </summary>
    public class CatchFlight : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _flyerPrefab;
        [SerializeField] SpriteRenderer _shadowPrefab;
        [Tooltip("어종별 도착 지점(바구니 칸). speciesId 순서와 맞출 것")]
        [SerializeField] Transform[] _basketSlots;
        [SerializeField] ParticleSystem _splashPrefab;

        class Flight
        {
            public SpriteRenderer body, shadow;
            public Vector2 from, to;
            public float t, heading, scale;
            public Action onArrive, onDone;
        }

        readonly List<Flight> _active = new List<Flight>();

        public void Launch(Fish fish, Action onArrive, Action onDone)
        {
            int slot = Mathf.Clamp(SlotIndexOf(fish.Species), 0, Mathf.Max(0, _basketSlots.Length - 1));
            Vector2 target = (_basketSlots != null && _basketSlots.Length > 0)
                ? (Vector2)_basketSlots[slot].position
                : fish.Pos + Vector2.right * 8f;

            var f = new Flight
            {
                body    = Instantiate(_flyerPrefab, fish.Pos, Quaternion.identity, transform),
                shadow  = _shadowPrefab ? Instantiate(_shadowPrefab, fish.Pos, Quaternion.identity, transform) : null,
                from    = fish.Pos,
                to      = target,
                heading = fish.Heading,
                scale   = fish.SizeScale,
                onArrive = onArrive,
                onDone   = onDone,
            };
            f.body.sprite = fish.Species.bodySprite;
            _active.Add(f);

            if (_splashPrefab) Instantiate(_splashPrefab, fish.Pos, Quaternion.identity, transform);
        }

        int SlotIndexOf(FishSpeciesSO sp)
        {
            // 프로젝트에 맞게 교체 — 지금은 인스펙터 배열 순서를 그대로 쓴다는 전제
            return sp ? Mathf.Abs(sp.speciesId.GetHashCode()) % Mathf.Max(1, _basketSlots.Length) : 0;
        }

        void Update()
        {
            float dt = FishingTuning.Dt;
            const float T1 = FishingTuning.CatchLiftDuration;
            const float T2 = FishingTuning.CatchFlyDuration;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var f = _active[i];
                f.t += dt;

                float lift, fly;
                Vector2 pos;

                if (f.t < T1)
                {
                    // ① 솟아오름
                    float u = Mathf.SmoothStep(0f, 1f, f.t / T1);
                    lift = u; fly = 0f;
                    pos = f.from + Vector2.up * (u * 0.35f);
                }
                else
                {
                    // ② 포물선 비행
                    float u = Mathf.Clamp01((f.t - T1) / T2);
                    float e = Mathf.SmoothStep(0f, 1f, u);
                    lift = 1f; fly = u;
                    Vector2 start = f.from + Vector2.up * 0.35f;
                    pos = Vector2.Lerp(start, f.to, e)
                        + Vector2.up * (Mathf.Sin(u * Mathf.PI) * FishingTuning.CatchArcHeight);

                    if (u >= 1f)
                    {
                        // ③ 입고 — 집계는 여기서 오른다. 그래야 비행이 결과를 실어 나르는 게 된다.
                        f.onArrive?.Invoke();
                        f.onDone?.Invoke();
                        if (f.body) Destroy(f.body.gameObject);
                        if (f.shadow) Destroy(f.shadow.gameObject);
                        _active.RemoveAt(i);
                        continue;
                    }
                }

                float s = (1f + lift * 0.20f) * (1f - fly * 0.66f) * f.scale;
                float alpha = 1f - Mathf.Max(0f, (fly - 0.75f) / 0.25f);

                f.body.transform.position = pos;
                f.body.transform.rotation = Quaternion.Euler(0f, 0f,
                    (f.heading - fly * 0.5f) * Mathf.Rad2Deg);
                f.body.transform.localScale = Vector3.one * s;
                var c = f.body.color; c.a = alpha; f.body.color = c;

                if (f.shadow)
                {
                    // 그림자는 수면에 남는다 → 솟아오를수록 아래로 멀어지고 흐려진다
                    float sep = 0.10f + lift * 0.45f + fly * 0.5f;
                    f.shadow.transform.position = new Vector3(pos.x + sep * 0.5f, pos.y - sep, 0f);
                    f.shadow.transform.rotation = f.body.transform.rotation;
                    f.shadow.transform.localScale = Vector3.one * s;
                    var sc = f.shadow.color;
                    sc.a = Mathf.Lerp(0.28f, 0.02f, fly) * alpha;
                    f.shadow.color = sc;
                }
            }
        }
    }
}
