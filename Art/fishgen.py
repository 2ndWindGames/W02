"""
절차적 물고기 생성 — Blender 4.0 headless
파라미터만으로 어종을 만든다. 스컬프팅도, 텍스처도, 리깅도 없다.

  blender -b --python fishgen.py -- --out /home/claude/blender/out
"""
import bpy, bmesh, math, sys, os, hashlib
from mathutils import Vector

# ─────────────────────────────────────────────────────────────────
# 어종 파라미터 — 여기만 고치면 새 어종이 나온다
# ─────────────────────────────────────────────────────────────────
def lerp(a, b, t): return a + (b - a) * t

def interp(u, pts):
    """(u, v) 제어점 리스트를 선형보간"""
    for i in range(len(pts) - 1):
        u0, v0 = pts[i]; u1, v1 = pts[i + 1]
        if u <= u1:
            t = 0 if u1 == u0 else (u - u0) / (u1 - u0)
            return lerp(v0, v1, max(0.0, min(1.0, t)))
    return pts[-1][1]

# 일반적인 물고기 등쪽 윤곽 (u=0 주둥이 → u=1 미병)
GEN_W = [(0,.00),(.04,.20),(.12,.48),(.24,.78),(.38,1.00),(.54,.92),(.68,.70),(.80,.44),(.90,.24),(1,.11)]
GEN_H = [(0,.00),(.04,.26),(.12,.56),(.24,.84),(.38,1.00),(.54,.95),(.68,.76),(.80,.50),(.90,.28),(1,.13)]

SPECIES = {
 # v0.6 — 실제 어종 5종. 색을 **강하게 갈라야** 탑다운에서 구분된다.
 #   멸치 은백 / 연어 주홍 / 만새기 금녹 / 오징어 크림 / 참치 진남색
 # 크기 과장도 넓혔다: 0.42 → 2.55 (6.1배).
 # turn-bench.html 의 SPECIES 와 값이 같아야 한다.

 'anchovy': dict(
    name='멸치', L=0.42, wmax=0.048, hmax=0.056,
    wprof=[(0,0),(.05,.28),(.14,.60),(.30,.93),(.44,1),(.60,.88),(.76,.60),(.88,.30),(1,.09)],
    hprof=[(0,0),(.05,.34),(.14,.66),(.30,.95),(.44,1),(.60,.92),(.76,.66),(.88,.36),(1,.11)],
    tail=dict(len=0.22, spread=0.16, notch=0.62, cant=0.03),
    pect=dict(u0=0.30, u1=0.42, len=0.062, sweep=0.44),
    dorsal=dict(u0=0.30, u1=0.52, h=0.09),
    base=(0.46,0.74,0.82), edge=(0.99,0.99,0.98), fin=(0.72,0.88,0.92),
    stripes=dict(n=1, u0=0.30, u1=0.56, amt=0.30, w=0.60),
    eye=dict(u=0.150, spread=0.56, r=0.024),
    waveAmp=0.70, finRipple=0.007, finRate=2.6),

 'salmon': dict(
    name='연어', L=1.15, wmax=0.162, hmax=0.145,
    wprof=[(0,0),(.04,.30),(.12,.62),(.26,.88),(.40,1),(.56,.96),(.72,.76),(.86,.42),(1,.13)],
    hprof=[(0,0),(.04,.36),(.12,.68),(.26,.92),(.40,1),(.56,.97),(.72,.80),(.86,.46),(1,.15)],
    tail=dict(len=0.23, spread=0.19, notch=0.26, cant=0.05),
    pect=dict(u0=0.29, u1=0.43, len=0.092, sweep=0.42),
    dorsal=dict(u0=0.30, u1=0.56, h=0.11),
    base=(0.74,0.30,0.20), edge=(0.99,0.78,0.60), fin=(0.66,0.32,0.26),
    stripes=dict(n=0, u0=0, u1=0, amt=0, w=0), spots=True,
    eye=dict(u=0.165, spread=0.57, r=0.027),
    waveAmp=1.0, finRipple=0.011, finRate=1.9),

 'mahi': dict(
    # 앞이 뭉툭하게 높고 뒤로 급히 가늘어진다. 등 전체를 덮는 등지느러미가 식별자.
    name='만새기', L=1.55, wmax=0.150, hmax=0.218,
    wprof=[(0,0),(.03,.44),(.10,.76),(.20,.96),(.34,1),(.50,.86),(.66,.60),(.82,.32),(1,.10)],
    hprof=[(0,0),(.03,.62),(.10,.92),(.20,1),(.34,.99),(.50,.86),(.66,.62),(.82,.34),(1,.11)],
    tail=dict(len=0.26, spread=0.20, notch=0.58, cant=0.03),
    pect=dict(u0=0.24, u1=0.36, len=0.080, sweep=0.40),
    dorsal=dict(u0=0.07, u1=0.84, h=0.13),
    base=(0.14,0.56,0.36), edge=(0.99,0.88,0.20), fin=(0.16,0.52,0.66),
    stripes=dict(n=0, u0=0, u1=0, amt=0, w=0), spots=True,
    eye=dict(u=0.120, spread=0.54, r=0.026),
    waveAmp=1.45, finRipple=0.012, finRate=2.0),

 'squid': dict(
    # Hover-Dash 는 제트 추진이다 — 물고기가 아니라 오징어의 움직임.
    # 몸통 끝(외투강)이 앞서고 다리가 끌려간다. 그래서 +X 가 뾰족하다.
    name='오징어', L=1.05, wmax=0.130, hmax=0.108,
    wprof=[(0,0),(.07,.28),(.20,.58),(.38,.86),(.56,1.0),(.72,.99),(.88,.93),(1,.82)],
    hprof=[(0,0),(.07,.30),(.20,.58),(.38,.84),(.56,1.0),(.72,.97),(.88,.89),(1,.76)],
    arms=dict(n=8, len=0.40, spread=0.40, w=0.026, seg=7),
    tail=dict(len=0.16, spread=0.16, notch=0.15, cant=0.05),
    pect=dict(u0=0.10, u1=0.56, len=0.150, sweep=0.05, seg=12),
    dorsal=dict(u0=0.32, u1=0.72, h=0.018),
    base=(0.80,0.66,0.62), edge=(0.99,0.96,0.92), fin=(0.74,0.58,0.56),
    stripes=dict(n=0, u0=0, u1=0, amt=0, w=0), spots=True,
    eye=dict(u=0.84, spread=0.70, r=0.038, ring=1.24),
    # 외투막(aU 0~1.0)은 강체다. 관절은 다리 밑동(aU 0.95)에만 있다.
    waveAmp=0.55, waveLag=3.4, waveExp=1.0, waveHinge=0.95, arcBody=0.22,
    finRipple=0.020, finWave=11.0, finRate=4.2),

 'tuna': dict(
    # 럭비공 — 앞뒤가 두툼하고 미병에서 갑자기 가늘어진다.
    name='참치', L=2.55, wmax=0.352, hmax=0.296,
    wprof=[(0,0),(.03,.30),(.09,.62),(.20,.90),(.34,1),(.48,.99),(.64,.82),(.80,.42),(.90,.20),(1,.07)],
    hprof=[(0,0),(.03,.36),(.09,.68),(.20,.93),(.34,1),(.48,1),(.64,.86),(.80,.46),(.90,.22),(1,.08)],
    tail=dict(len=0.26, spread=0.21, notch=0.66, cant=0.03),
    pect=dict(u0=0.26, u1=0.40, len=0.112, sweep=0.42),
    dorsal=dict(u0=0.28, u1=0.50, h=0.13),
    finlets=dict(n=4, u0=0.66, u1=0.88, len=0.020),
    base=(0.08,0.17,0.42), edge=(0.78,0.83,0.89), fin=(0.14,0.22,0.40),
    finletCol=(0.85,0.68,0.20),
    stripes=dict(n=0, u0=0, u1=0, amt=0, w=0),
    eye=dict(u=0.140, spread=0.55, r=0.024),
    waveAmp=1.75, finRipple=0.013, finRate=1.5),
}

PIVOT = 0.36            # 좌우 흔들림의 회전 중심 — 셰이더와 같아야 한다
RINGS, SEG = 40, 24     # 몸통 해상도. 이 정도면 삼각형 ~1100개


# ─────────────────────────────────────────────────────────────────
# 메시 생성
# ─────────────────────────────────────────────────────────────────
def build_fish(key, sp, bend=None):
    """bend = (amp, phase, turn) — 셰이더가 할 휨을 여기서 미리 적용해 검증한다"""
    verts, faces, cols = [], [], []
    # (aU, aSeed, aRip) — verts 와 1:1. glTF 는 임의 속성을 안 실어주므로
    # 이 셋을 UV 레이어에 실어 보낸다. 없으면 유니티에서 휨 셰이더가 아예 못 돈다.
    chan = []
    L, wmax, hmax = sp['L'], sp['wmax'], sp['hmax']

    def bend_y(u):
        if not bend: return 0.0
        amp, phase, turn = bend
        # 셰이더에 넣을 것과 같은 수식:
        #   유영 파동 + 선회 굽힘 (선회는 몸 전체가 호를 그린다)
        # 위상 지연(lag)이 클수록 '파동이 몸을 타고 지나간다'.
        # hinge 는 파동이 시작되는 지점 — 0 이면 주둥이부터(물고기),
        # 오징어는 0.95(다리 밑동)라 외투막이 강체로 남는다.
        # ★ turn-bench.html 의 GLSL bendAt() 과 같은 식이어야 한다.
        lag = sp.get('waveLag', 2.4)
        # ★ PIVOT 지점의 변위를 빼서 몸 중심을 고정한다. 안 빼면 주둥이가 축이 되어
        #   몸 전체가 좌우로 까닥인다. 빼면 머리가 꼬리와 반대로 살짝 도는데
        #   그게 실제 물고기의 움직임(head yaw)이다. 셰이더의 PIVOT 과 같은 값.
        def sw(x):
            bb = max(x - sp.get('waveHinge', 0.0), 0.0)
            return amp * (bb ** sp.get('waveExp', 1.6)) * math.sin(phase - x * lag)
        swim = sw(u) - sw(PIVOT)
        # ★ u > 1 (꼬리·다리) 구간은 완만하게만 더 끌린다. 이걸 빼먹으면
        #   촉완처럼 u 가 2에 가까운 정점이 2.5배 과하게 휘어서
        #   셰이더(3D)와 생성기(스프라이트)가 서로 다른 모양이 된다.
        # arcBody = 몸통이 선회에 참여하는 정도. 오징어 외투막은 강체라 거의 0.
        ab   = sp.get('arcBody', 1.0)
        arc  = turn * (ab * min(u, 1.0) ** 1.9 + max(u - 1.0, 0.0) * 0.5)
        return (swim + arc) * L

    # 몸통 링
    ring_start = len(verts)
    for i in range(RINGS + 1):
        u = i / RINGS
        w = interp(u, sp['wprof']) * wmax
        h = interp(u, sp['hprof']) * hmax
        x = u * L * 0.80
        y0 = bend_y(u)
        for j in range(SEG):
            a = j / SEG * math.tau
            y = math.cos(a) * w + y0
            z = math.sin(a) * h
            verts.append((x, y, z)); chan.append((u, 0.0, 0.0))
            # 색 — 등(위)은 진하고 옆구리는 밝게. 위에서 볼 때 둥글어 보이게 하는 값
            t = max(0.0, math.sin(a))                # 1 = 등마루, 0 = 옆구리
            shade = t ** 0.42
            c = [lerp(sp['edge'][k], sp['base'][k], shade) for k in range(3)]
            st = sp['stripes']
            if st['n'] and st['u0'] <= u <= st['u1']:
                p = (u - st['u0']) / (st['u1'] - st['u0']) * st['n']
                band = abs((p % 1.0) - 0.5) * 2.0
                if band > 1.0 - st['w']:
                    k2 = st['amt'] * shade
                    c = [c[m] * (1 - k2) for m in range(3)]
            if sp.get('spots'):
                hsh = int(hashlib.md5(f"{i//2}_{j//2}".encode()).hexdigest()[:4], 16) / 65535
                if hsh > 0.62 and t > 0.35:
                    c = [c[m] * 0.55 for m in range(3)]
            cols.append(c)

    for i in range(RINGS):
        for j in range(SEG):
            a = ring_start + i * SEG + j
            b = ring_start + i * SEG + (j + 1) % SEG
            c = ring_start + (i + 1) * SEG + (j + 1) % SEG
            d = ring_start + (i + 1) * SEG + j
            faces.append((a, b, c, d))

    # 주둥이/미병 마감
    for idx, ring in ((0, 0), (1, RINGS)):
        cx = 0.0 if ring == 0 else L * 0.80
        center = len(verts)
        verts.append((cx + (-0.02 * L if ring == 0 else 0.0), bend_y(ring / RINGS), 0.0))
        cols.append(sp['base']); chan.append((ring / RINGS, 0.0, 0.0))
        for j in range(SEG):
            a = ring_start + ring * SEG + j
            b = ring_start + ring * SEG + (j + 1) % SEG
            faces.append((center, b, a) if ring == 0 else (center, a, b))

    def quad(p, col, ch=None):
        # ch = 정점별 (aU, aSeed, aRip). 생략하면 전부 0.
        s = len(verts)
        for i, v in enumerate(p):
            verts.append(v); cols.append(col)
            chan.append(ch[i] if ch else (0.0, 0.0, 0.0))
        faces.append(tuple(range(s, s + len(p))))

    # 다리 다발 — 오징어. 몸통 뒤로 여러 가닥이 끌려간다.
    if sp.get('arms'):
        am = sp['arms']; xb = L * 0.80 - L*0.015
        pw = interp(1.0, sp['wprof']) * wmax
        for i in range(am['n']):
            t = (i / (am['n'] - 1)) * 2 - 1
            ang = t * am['spread']
            lng = 1.85 if i in (0, am['n'] - 1) else 1.0      # 촉완 2개는 길다
            club = lng > 1                                    # 촉완 끝의 흡반뭉치
            ln = am['len'] * L * lng * (0.74 + 0.26 * (1 - abs(t) * 0.8))
            # ★ 다리마다 u 를 어긋낸다. 바깥 다리일수록 지렛대가 길어 더 끌린다.
            #   turn-bench.html 의 uEnd 식과 같아야 한다.
            u_end = 1 + am['len'] * lng * 1.15 * (0.82 + 0.30 * abs(t))
            # ★ 분절. 밑동-끝 쿼드 한 장이면 **휠 수가 없고 꺾이기만** 한다 →
            #   8가닥이 갈퀴로 보인다. 분절이 있어야 파동이 다리를 타고 흐른다.
            seg = am.get('seg', 7) + (4 if club else 0)   # 촉완은 길어서 분절이 더 필요
            hw0 = am['w'] * L * (0.72 if club else 1.0)
            base_y = t * pw * 0.70
            # ★ 다리마다 다른 위상. turn-bench.html 의 hash01(a*7+3, 11) 과 같은 역할.
            seed = ((i * 7 + 3) * 1103515245 + 12345) % 2147483648 / 2147483648
            prev = None; pu = 1.0
            for k in range(seg + 1):
                fr = k / seg
                hw = hw0 * (1 - 0.72 * fr * fr)
                if club:
                    hw *= 1 + 2.1 * math.exp(-((fr - 0.88) / 0.11) ** 2)
                u = 1 + (u_end - 1) * fr
                x = xb + math.cos(ang) * ln * fr
                y = base_y + math.sin(ang) * ln * fr + bend_y(u)
                cur = ((x, y - hw, -0.004 * fr), (x, y + hw, -0.004 * fr))
                if prev is not None:
                    quad([prev[0], prev[1], cur[1], cur[0]], sp['fin'],
                         [(pu, seed, 0.0), (pu, seed, 0.0), (u, seed, 0.0), (u, seed, 0.0)])
                prev = cur; pu = u

    # 꼬리지느러미 — 실제 물고기는 수직이라 위에서 보면 선이다.
    # 게임 가독성을 위해 수평으로 눕혀서 부채꼴로 만든다 (의도적 거짓말)
    tl = sp['tail']; xb = L * 0.80; xe = xb + tl['len'] * L
    yb = bend_y(1.0); sprd = tl['spread'] * L; z = tl['cant'] * L
    pw = interp(1.0, sp['wprof']) * wmax * 0.95       # 미병 폭 = 꼬리 밑동
    xb -= L * 0.02                                     # 살짝 겹쳐서 이음매를 감춘다
    if not sp.get('arms'):
        quad([(xb, yb + pw, 0), (xe, yb + sprd, z),
              (xe - tl['notch'] * tl['len'] * L, yb, 0),
              (xe, yb - sprd, z), (xb, yb - pw, 0)], sp['fin'])

    # 눈 — 링 정점에 색을 칠하면 해상도에 눌려 각진다. 작은 원반을 표면에 얹는다.
    # 탑다운에서 눈은 인식의 절반이지만, 크면 즉시 징그러워진다. 작고 옆으로.
    ey = sp.get('eye')
    if ey:
        for s2 in (1, -1):
            a_e = math.pi / 2 - s2 * ey['spread']          # 등마루에서 옆으로 내린 각
            ue = ey['u']
            w_e = interp(ue, sp['wprof']) * wmax
            h_e = interp(ue, sp['hprof']) * hmax
            px = ue * L * 0.80
            py = math.cos(a_e) * w_e + bend_y(ue)
            pz = h_e + 0.012 * L          # 국소 최고점 위 — 아래로 파묻히면 링이 잘린다
            # ⚠️ 원반을 표면 법선에 맞춰 세우면 위에서 볼 때 몸통 실루엣에 잘려 초승달이 된다.
            #    탑다운이므로 원반을 '위로 눕힌다' — 꼬리지느러미와 같은 의도적 거짓말.
            n  = Vector((0.0, 0.0, 1.0))
            t1 = Vector((1.0, 0.0, 0.0))
            t2 = Vector((0.0, 1.0, 0.0))
            base_p = Vector((px, py, pz))
            # 홍채는 흰색이 아니라 옆구리 색을 밝힌 것. 대비가 높으면 즉시 인형 눈이 된다.
            iris = tuple(min(1.0, c * 1.06 + 0.10) for c in sp['edge'])
            # 동공 → 홍채링 → 하이라이트 순으로 얹는다.
            # 하이라이트가 없으면 점이 '눈'이 아니라 '반점'으로 읽힌다. 다만 아주 작게.
            base_r = min(ey['r'] * L, w_e * 0.62)
            layers = ((ey.get('ring', 1.30), ey.get('iris', iris), 0.0),
                      (1.0, ey.get('pupil', (0.09, 0.10, 0.12)), 0.0),
                      (0.34, (0.93, 0.95, 0.96), 0.42))          # 하이라이트: 바깥쪽으로 치우침
            for r_mul, col, off in layers:
                rad = base_r * r_mul
                lift = n * (0.0022 * L + 0.0011 * L * (1.0 - r_mul))
                shift = t2 * (-s2 * base_r * off) + t1 * (base_r * off * 0.35)
                ctr = base_p + lift + shift
                cen = len(verts)
                # 눈은 몸통에 얹힌 원반이므로 눈이 있는 u 를 그대로 받는다.
                # 0 을 주면 몸이 휠 때 눈만 제자리에 남아 떨어져 나간다.
                eu = sp['eye']['u']
                verts.append(tuple(ctr)); cols.append(col); chan.append((eu, 0.0, 0.0))
                N2 = 12
                for i in range(N2):
                    th = i / N2 * math.tau
                    p = ctr + (t1 * math.cos(th) + t2 * math.sin(th)) * rad
                    verts.append(tuple(p)); cols.append(col); chan.append((eu, 0.0, 0.0))
                for i in range(N2):
                    faces.append((cen, cen + 1 + i, cen + 1 + (i + 1) % N2))
                    faces.append((cen, cen + 1 + (i + 1) % N2, cen + 1 + i))

    # 가슴지느러미 / 외투막 지느러미 — 좌우 대칭, 뒤로 젖힘
    # ⚠️ 밑동을 '점 하나'로 두면 몸통에서 나오는 부분이 서브픽셀로 얇아져
    #    떨어져 붙은 것처럼 보인다. 밑동은 몸통을 따라가는 **선분**이어야 한다.
    # ★ 삼각형 한 장이면 물결칠 수가 없다. 오징어 외투막 지느러미는 길게 붙은
    #   능선이고 실제로는 끊임없이 파도친다 — 밑동을 따라 분절한다.
    #   turn-bench.html 의 분절식과 같아야 한다.
    pc = sp['pect']; NF = pc.get('seg', 3)
    for sgn in (1, -1):
        prev = None; pu = pc['u0']; pprof = 0.0
        for i in range(NF + 1):
            fr = i / NF
            u = pc['u0'] + (pc['u1'] - pc['u0']) * fr
            prof = math.sin(math.pi * fr)             # 가운데가 가장 넓다
            wR = interp(u, sp['wprof']) * wmax * 0.80  # 밑동은 몸 안쪽
            wE = interp(u, sp['wprof']) * wmax + pc['len'] * L * prof
            by = bend_y(u)
            # 지느러미 젓기/물결 — 셰이더의 uRip 항과 같은 식.
            # ripW 큼(오징어 11) = 파동이 막을 타고 흐른다,
            # ripW 0(물고기)     = 막 전체가 한 몸으로 앞뒤로 젓는다.
            rx = 0.0
            if bend:
                rip = sp.get('finRipple', 0.0)
                rx = rip * prof * math.sin(bend[1] * sp.get('finRate', 2.2) - u * sp.get('finWave', 0.0)) * L
            cur = ((u * L * 0.80 + rx * 0.15, by + sgn * wR, 0.0),
                   (u * L * 0.80 + pc['len'] * L * pc['sweep'] * prof + rx,
                    by + sgn * wE, -0.015))
            if prev is not None:
                quad([prev[0], prev[1], cur[1], cur[0]], sp['fin'],
                     [(pu, 0.0, 0.0), (pu, 0.0, pprof), (u, 0.0, prof), (u, 0.0, 0.0)])
            prev = cur; pu = u; pprof = prof

    # 토막지느러미(finlet) — 고등어·참치의 뒤쪽에 늘어선 작은 삼각형들.
    # 위에서 볼 때 톱니처럼 늘어선 실루엣이라 어종 식별에 크게 기여한다.
    if sp.get('finlets'):
        fl = sp['finlets']; fc = sp.get('finletCol', sp['fin'])
        for i in range(fl['n']):
            u  = fl['u0'] + (fl['u1'] - fl['u0']) * (i / (fl['n'] - 1))
            w  = interp(u, sp['wprof']) * wmax
            ln = fl['len'] * L * (1 - 0.35 * (i / (fl['n'] - 1)))
            by = bend_y(u)
            # ⚠️ 뾰족한 삼각형이면 가시가 줄지어 난 것처럼 보인다.
            #    낮고 넓은 부채꼴이어야 '작은 지느러미'로 읽힌다.
            for sg in (1, -1):
                quad([(u*L*0.80 - ln*0.35, by + sg*w*0.86,          0.002),
                      (u*L*0.80 + ln*0.30, by + sg*(w + ln*0.30),   0.000),
                      (u*L*0.80 + ln*1.15, by + sg*(w + ln*0.22),   0.000),
                      (u*L*0.80 + ln*1.55, by + sg*w*0.80,          0.002)], fc,
                     [(u,0.0,0.0)]*4)

    # 등지느러미 — 얇은 판은 위에서 '선' 하나로 보여 흉터처럼 읽힌다.
    # 낮고 폭 있는 능선(텐트)으로 만들어야 지느러미로 읽힌다.
    dr = sp['dorsal']; N = 8
    ridge = []
    for i in range(N + 1):
        u = lerp(dr['u0'], dr['u1'], i / N)
        x = u * L * 0.80
        hb = interp(u, sp['hprof']) * hmax
        bump = math.sin(math.pi * (i / N)) ** 0.7
        ridge.append((x, bend_y(u), hb * 0.92 + dr['h'] * L * bump,
                      interp(u, sp['wprof']) * wmax * 0.30 * bump + 0.004))
    for i in range(N):
        (x0, y0, z0, b0), (x1, y1, z1, b1) = ridge[i], ridge[i + 1]
        zb0 = interp(lerp(dr['u0'], dr['u1'], i / N), sp['hprof']) * hmax * 0.86
        zb1 = interp(lerp(dr['u0'], dr['u1'], (i+1) / N), sp['hprof']) * hmax * 0.86
        for sgn in (1, -1):
            quad([(x0, y0, z0), (x1, y1, z1),
                  (x1, y1 + sgn * b1, zb1), (x0, y0 + sgn * b0, zb0)], sp['fin'])

    # 머리를 +X 로 (Z축 180° 정회전 — 반사가 아니라 감기·법선 그대로).
    # 유니티에서 rotation.z = heading 이 +X 를 진행 방향에 맞추므로 이 축이 곧 '앞'이다.
    verts = [(-v[0], -v[1], v[2]) for v in verts]

    assert len(chan) == len(verts), \
        f"{key}: 셰이더 채널이 정점과 안 맞는다 ({len(chan)} vs {len(verts)}) — " \
        "verts.append 하는 곳마다 chan.append 도 해야 한다"

    mesh = bpy.data.meshes.new(key)
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    for p in mesh.polygons: p.use_smooth = True

    ca = mesh.color_attributes.new(name="Col", type='FLOAT_COLOR', domain='CORNER')
    for li, loop in enumerate(mesh.loops):
        c = cols[loop.vertex_index]
        ca.data[li].color = (c[0], c[1], c[2], 1.0)

    # ★ 셰이더 채널을 UV 로 굽는다. glTF/유니티가 확실히 실어주는 통로가 UV 뿐이다.
    #   UV0 = (aU, aSeed) · UV1 = (aRip, 0)
    uv0 = mesh.uv_layers.new(name="Chan0")
    uv1 = mesh.uv_layers.new(name="Chan1")
    for li, loop in enumerate(mesh.loops):
        u_, sd_, rp_ = chan[loop.vertex_index]
        uv0.data[li].uv = (u_, sd_)
        uv1.data[li].uv = (rp_, 0.0)

    obj = bpy.data.objects.new(key, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


# ─────────────────────────────────────────────────────────────────
# 툰 머티리얼 — 정점 컬러 × 2단 램프. 텍스처 없음
# ─────────────────────────────────────────────────────────────────
def toon_material():
    mat = bpy.data.materials.new("Toon"); mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    vc  = nt.nodes.new('ShaderNodeVertexColor'); vc.layer_name = "Col"
    dif = nt.nodes.new('ShaderNodeBsdfDiffuse')
    s2r = nt.nodes.new('ShaderNodeShaderToRGB')
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (0.66, 0.69, 0.76, 1)
    ramp.color_ramp.elements[1].position = 0.40
    ramp.color_ramp.elements[1].color = (0.87, 0.89, 0.93, 1)
    e = ramp.color_ramp.elements.new(0.76); e.color = (1.00, 1.00, 0.98, 1)
    mul = nt.nodes.new('ShaderNodeMixRGB'); mul.blend_type = 'MULTIPLY'; mul.inputs[0].default_value = 1.0
    emi = nt.nodes.new('ShaderNodeEmission')
    nt.links.new(dif.outputs[0], s2r.inputs[0])
    nt.links.new(s2r.outputs[0], ramp.inputs[0])
    nt.links.new(vc.outputs[0], mul.inputs[1])
    nt.links.new(ramp.outputs[0], mul.inputs[2])
    nt.links.new(mul.outputs[0], emi.inputs[0])
    nt.links.new(emi.outputs[0], out.inputs[0])
    return mat


def outline_material():
    mat = bpy.data.materials.new("Outline"); mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    emi = nt.nodes.new('ShaderNodeEmission')
    emi.inputs[0].default_value = (0.06, 0.07, 0.10, 1)
    nt.links.new(emi.outputs[0], out.inputs[0])
    mat.use_backface_culling = True
    return mat


OUTLINE = True
def add_outline(obj, mat_out, thickness=0.012):
    return None   # 역외피 대신 Freestyle 사용
    if not OUTLINE: return None
    dup = obj.copy(); dup.data = obj.data.copy()
    bpy.context.collection.objects.link(dup)
    dup.data.materials.clear(); dup.data.materials.append(mat_out)
    m = dup.modifiers.new("Solid", 'SOLIDIFY')
    m.thickness = thickness; m.offset = 1.0; m.use_flip_normals = True
    m.use_rim = False
    return dup


# ─────────────────────────────────────────────────────────────────
def setup_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.film_transparent = True
    sc.eevee.taa_render_samples = 64
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.view_settings.view_transform = 'Standard'
    if OUTLINE:
        sc.render.use_freestyle = True
        vl = sc.view_layers[0]; vl.use_freestyle = True
        fs = vl.freestyle_settings
        fs.as_render_pass = False
        ls = vl.freestyle_settings.linesets[0] if vl.freestyle_settings.linesets else \
             vl.freestyle_settings.linesets.new("Out")
        ls.select_silhouette = True; ls.select_border = True
        ls.select_crease = False; ls.select_edge_mark = False
        if ls.linestyle is None:
            ls.linestyle = bpy.data.linestyles.new("LS")
        ls.linestyle.thickness = 3.2
        ls.linestyle.color = (0.06, 0.08, 0.12)

    cam_d = bpy.data.cameras.new("Cam"); cam_d.type = 'ORTHO'
    cam = bpy.data.objects.new("Cam", cam_d)
    bpy.context.collection.objects.link(cam)
    cam.location = (0, 0, 6); cam.rotation_euler = (0, 0, 0)   # 정수직 하향
    sc.camera = cam

    sun_d = bpy.data.lights.new("Sun", 'SUN'); sun_d.energy = 3.2
    sun = bpy.data.objects.new("Sun", sun_d)
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(28), math.radians(-16), 0)
    bpy.context.collection.objects.link(
        bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", 'SUN')))
    f = bpy.data.objects["Fill"]; f.data.energy = 1.1
    f.rotation_euler = (math.radians(-38), math.radians(30), 0)
    return cam


def fit(objs, aspect, pad=0.25):
    """보이는 오브젝트의 실제 경계로 카메라를 맞춘다 (배치 규칙이 바뀌어도 안 깨지게).
       bound_box 는 depsgraph 갱신 전이면 낡은 값이라 반드시 update() 먼저."""
    bpy.context.view_layer.update()
    xs, ys = [], []
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            xs.append(w.x); ys.append(w.y)
    cx, cy = (min(xs)+max(xs))/2, (min(ys)+max(ys))/2
    return (cx, cy), max(max(xs)-min(xs), (max(ys)-min(ys))*aspect) + pad*2


def render(path, cam, ortho, center=(0, 0), res=(1024, 512)):
    sc = bpy.context.scene
    cam.data.ortho_scale = ortho
    cam.location = (center[0], center[1], 6)
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[argv.index("--out") + 1] if "--out" in argv else "/home/claude/blender/out"
    mode = argv[argv.index("--mode") + 1] if "--mode" in argv else "sheet"
    globals()["OUTLINE"] = ("--no-outline" not in argv)
    os.makedirs(out, exist_ok=True)

    cam = setup_scene()
    mat, mout = toon_material(), outline_material()

    if mode == "sheet":
        xs, placed = 0.0, []
        for key in ('anchovy', 'salmon', 'mahi', 'squid', 'tuna'):
            sp = SPECIES[key]
            o = build_fish(key, sp)
            o.data.materials.append(mat)
            group = [o]
            ol = add_outline(o, mout)
            if ol: group.append(ol)
            for ob in group: ob.location = (xs, 0, 0)
            placed.append((key, sp, group, xs))
            xs += sp['L'] * 1.35 + 0.55
        c, w = fit([o for _, _, g, _ in placed for o in g], 1600/460, 0.3)
        render(f"{out}/sheet.png", cam, w, center=c, res=(1600, 460))

        for key, sp, group, x0 in placed:
            for k2, s2, g2, _ in placed:
                for ob in g2: ob.hide_render = (k2 != key)
            c, _ = fit(group, 1.0, 0)
            render(f"{out}/{key}.png", cam, sp['L'] * 1.45, center=c, res=(512, 512))
        for _, _, g, _ in placed:
            for ob in g: ob.hide_render = False

    elif mode == "sprites":
        # 2D 예비안 — 3D 와 같은 모델에서 굽는다. 둘이 어긋날 일이 없다.
        # 정점 휨을 못 쓰는 대신 유영 파동 한 주기를 N 프레임으로 굽는다.
        PPU, N = 128, 8
        for key in ('anchovy', 'salmon', 'mahi', 'squid', 'tuna'):
            sp = SPECIES[key]
            frames = []
            # 모든 프레임을 담을 공통 셀 크기를 먼저 구한다
            bx0 = by0 = 1e9; bx1 = by1 = -1e9
            for i in range(N):
                for o in list(bpy.context.collection.objects):
                    if o.type == 'MESH': bpy.data.objects.remove(o, do_unlink=True)
                o = build_fish(key, sp, bend=(0.16 * sp.get("waveAmp", 1.0) * 0.40 * 1.7,   # 2D 는 이 파동이 유일한 움직임이라 더 크게
                                              i / N * math.tau, 0.0))
                o.data.materials.append(mat)
                bpy.context.view_layer.update()
                for c in o.bound_box:
                    w = o.matrix_world @ Vector(c)
                    bx0 = min(bx0, w.x); bx1 = max(bx1, w.x)
                    by0 = min(by0, w.y); by1 = max(by1, w.y)
            cw = (bx1 - bx0) * 1.16; ch = (by1 - by0) * 1.35
            side = max(cw, ch)
            px = int(side * PPU / 4 + 1) * 4
            cx, cy = (bx0 + bx1) / 2, (by0 + by1) / 2
            for i in range(N):
                for o in list(bpy.context.collection.objects):
                    if o.type == 'MESH': bpy.data.objects.remove(o, do_unlink=True)
                o = build_fish(key, sp, bend=(0.16 * sp.get("waveAmp", 1.0) * 0.40 * 1.7,   # 2D 는 이 파동이 유일한 움직임이라 더 크게
                                              i / N * math.tau, 0.0))
                o.data.materials.append(mat)
                render(f"{out}/spr_{key}_{i}.png", cam, side, center=(cx, cy), res=(px, px))
            print(f"SPRITE {key} {px}px x{N}")

    elif mode == "bend":
        key = argv[argv.index("--sp") + 1] if "--sp" in argv else 'tuna'
        poses = [(0.00, 0.0,  0.00, "straight"),
                 (0.16, 1.1,  0.00, "swim"),
                 (0.10, 2.2,  0.30, "turn"),
                 (0.10, 0.4,  0.62, "hardturn")]
        sp = SPECIES[key]; xs = 0.0
        for amp, ph, turn, label in poses:
            o = build_fish(f"{key}_{label}", sp,
                           bend=(amp * sp.get('waveAmp', 1.0) * 0.40 / 0.16 * 0.16, ph, turn))
            o.data.materials.append(mat)
            group = [o]
            ol = add_outline(o, mout)
            if ol: group.append(ol)
            for ob in group: ob.location = (xs, 0, 0)
            xs += sp['L'] * 1.15 + 0.10
        c, w = fit([o for o in bpy.context.collection.objects if o.type == 'MESH'], 1600/700, 0.3)
        render(f"{out}/bend_{key}.png", cam, w, center=c, res=(1600, 700))

    print("RENDER DONE ->", out)
