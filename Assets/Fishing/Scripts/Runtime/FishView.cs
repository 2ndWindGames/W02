using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 물고기의 표현 — 회전, 뱅킹, 그림자, 꼬리 지연, 셰이더 파라미터. 기획서 §6.
    ///
    /// 스파인·2D Bone·프레임 애니메이션 전부 불필요하다.
    /// 파동은 셰이더가, 선회감은 트랜스폼이 만든다.
    /// </summary>
    [RequireComponent(typeof(Fish))]
    public class FishView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _body;
        [Tooltip("대물만 사용. 비어 있으면 통짜 1파트.")]
        [SerializeField] Transform _tailPivot;
        [SerializeField] SpriteRenderer _tail;
        [Tooltip("탑다운에서는 필수. 없으면 물고기가 수면 위 스티커로 보인다 — §6-4")]
        [SerializeField] SpriteRenderer _shadow;

        static readonly int AmpId   = Shader.PropertyToID("_Amp");
        static readonly int FreqId  = Shader.PropertyToID("_Freq");
        static readonly int BoostId = Shader.PropertyToID("_SpeedBoost");

        Fish _fish;
        MaterialPropertyBlock _mpb;
        float _tailHeading;
        float _bank = 1f;
        Rect _water;

        void Awake()
        {
            _fish = GetComponent<Fish>();
            _mpb = new MaterialPropertyBlock();
        }

        void OnEnable() { _bank = 1f; _tailHeading = _fish.Heading; }

        /// <summary>스폰 시 1회. 스프라이트와 수면 정보를 받는다.</summary>
        public void Setup(FishSpeciesSO sp, Rect water)
        {
            _water = water;
            if (_body != null) _body.sprite = sp.bodySprite;
            bool twoPart = sp.tailSprite != null;
            if (_tail != null)
            {
                _tail.sprite = sp.tailSprite;
                _tail.gameObject.SetActive(twoPart);
            }
            if (_tailPivot != null) _tailPivot.gameObject.SetActive(twoPart);
            if (_shadow != null && _body != null) _shadow.sprite = _body.sprite;
            _bank = 1f;
            _tailHeading = _fish.Heading;
        }

        void LateUpdate()
        {
            var sp = _fish.Species;
            if (sp == null) return;
            float dt = FishingTuning.Dt;

            // ── 회전 ─────────────────────────────────────────────
            // Fish 가 이미 선회율 제한으로 Heading 을 굴렸다. 여기서는 그리기만 한다.
            transform.rotation = Quaternion.Euler(0f, 0f, _fish.Heading * Mathf.Rad2Deg);

            // ── 뱅킹 — 선회할 때 몸을 기울인다 ────────────────────
            float target = _fish.State == FishState.Interested
                ? 1f                                       // 관심 상태는 뱅킹 없음 (직진 신호를 흐리지 않게)
                : 1f - Mathf.Min(0.24f, Mathf.Abs(_fish.TurnRateNow) * 0.10f);
            _bank = Mathf.Lerp(_bank, target, Mathf.Min(1f, dt * 6f));

            float scale = _fish.SizeScale;
            transform.localScale = new Vector3(scale, scale * _bank, 1f);

            // ── 꼬리 지연 — 대물만. 선회할 때 꼬리가 뒤에서 끌려온다 (§6-3) ──
            if (_tailPivot != null)
            {
                float lag = Mathf.Max(sp.tailLag, 0.016f);
                _tailHeading = Mathf.LerpAngle(_tailHeading * Mathf.Rad2Deg,
                                               _fish.Heading * Mathf.Rad2Deg,
                                               Mathf.Min(1f, dt / lag)) * Mathf.Deg2Rad;
                float swing = Mathf.Sin(Time.time * sp.waveFreq * 5.2f) * 0.35f * sp.waveAmp;
                float delta = Mathf.DeltaAngle(_fish.Heading * Mathf.Rad2Deg, _tailHeading * Mathf.Rad2Deg);
                _tailPivot.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Clamp(delta, -40f, 40f) + swing * Mathf.Rad2Deg);
            }

            // ── 그림자 — 구역이 아래일수록 멀고 흐리게. 이게 곧 깊이다 (§6-4) ──
            if (_shadow != null)
            {
                float depth01 = _water.height > 0f
                    ? Mathf.Clamp01(1f - Mathf.InverseLerp(_water.yMin, _water.yMax, _fish.Pos.y))
                    : 0.5f;
                float off = Mathf.Lerp(0.05f, 0.22f, depth01);
                _shadow.transform.position = new Vector3(_fish.Pos.x + off * 0.8f,
                                                         _fish.Pos.y - off * 1.3f,
                                                         _shadow.transform.position.z);
                _shadow.transform.rotation = transform.rotation;
                // ⚠️ localScale 에 부모 스케일을 곱하면 안 된다. 자식이라 이미 상속받는다.
                _shadow.transform.localScale = Vector3.one * 1.05f;
                var c = _shadow.color;
                c.a = Mathf.Lerp(0.30f, 0.16f, depth01);
                _shadow.color = c;
            }

            // ── 파동 셰이더 파라미터 ──────────────────────────────
            if (_body != null)
            {
                float ampMul = 1f, freqMul = 1f;
                switch (_fish.State)
                {
                    case FishState.Bite:   ampMul = 1.8f; freqMul = 3.2f;  break;   // 물고 파닥임
                    case FishState.Notice: ampMul = 0.3f; freqMul = 0.22f; break;   // 발견 = 얼어붙음
                    case FishState.Interested: ampMul = 1.3f; freqMul = 1.8f; break; // 서둘러 온다
                }
                float speedBoost = 0.9f + Mathf.Min(1.4f, _fish.SpeedNow * 0.5f);

                _body.GetPropertyBlock(_mpb);
                _mpb.SetFloat(AmpId, sp.waveAmp * ampMul * FishingTuning.WaveAmpScale);
                _mpb.SetFloat(FreqId, sp.waveFreq * freqMul * 5.2f);
                _mpb.SetFloat(BoostId, speedBoost);
                _body.SetPropertyBlock(_mpb);
            }
        }
    }
}
