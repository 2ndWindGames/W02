# 낚시 게임 — 문서 인덱스

> 최종 갱신: 2026-09-02 (Water Optics tuning pass)
> 상태: **코어 루프 확정 · unified water composite/visible surface optics 튜닝 검증 · v25 데이터/이동 1차 이식 완료 · 물 브랜치 머지 후 상호작용 통합 대기** (§12 로드맵 참조)
>
> **v0.4 요약** — 운동 법칙 3 + 변형자 1로 재정리 / 사행 주기 2배 / 선회를 반경에서 뽑음(ω = v/R) /
> 접근 방식 조립 신설 / 복어 → 오징어 / 튜닝 값은 `const` 가 아니라 SO 로 /
> **리소스를 3D 메시로 확정** (아트가이드 v2.0). 2D 스프라이트는 같은 모델에서 구운 예비안

---

## 파일

| 파일 | 내용 | 언제 보나 |
|---|---|---|
| **01_기획서_v0.4.md** | 기획의 단일 원본. 설계 원칙 · 코어 루프 · 상태 머신 · 어종 4종 실수치 · 데이터 모델 · Unity 구조 · MVP 범위 | 구현 전, 그리고 규칙이 헷갈릴 때마다 |
| **02_아트리소스가이드_v2.0.md** | **3D 메시 규격** + 휨 셰이더 계약 + 2D 예비 규격 | 리소스 만들 때 |
| Art/fishgen.py | 어종 생성 원본. Blender 헤드리스. 여기 40줄이 어종 정의 전부 | 형태를 고칠 때 |
| bite-bench.html | **입질 벤치** — 입질·회수·점수. §7 수치가 그대로 들어가 있고 플레이·튜닝 가능 | 수치 감 잡을 때, 규칙 바꿔볼 때 |
| turn-bench.html | **선회 벤치** — 절차적 3D 메시 + 정점 휨. 선회·접근 방식의 정답. 브라우저로 바로 열림 | 이동·형태를 판단할 때 |
| 06_v25_이관상태_v0.1.md | v25 HTML → Unity 이관 범위·브랜치 경계·검증 상태 | v25 이관 작업을 이어갈 때 |

---

## 한 장 요약

**장르** 모바일 방치형 낚시. **필수 조작은 찌 배치 하나뿐** — 손을 놓아도 자동으로 잡힌다.
**시점** 탑다운. 물고기도 위에서 본 모습(dorsal view).
**세션** 90초 타이머 라운드 → 결과 → 낚시터 이동.

### 게임이 성립하는 네 가지 규칙

1. **찌를 옮기면 비용이 발생한다** — 재착수 = 놀람 + 관심 전원 리셋.
   이게 없으면 찌를 물고기 위로 끌고 다니게 되고 궤도·습성·예측이 전부 무의미해진다.
2. **잡는 것 자체가 비용이다** — 잡으면 찌를 걷어올려 큰 물장구, 놓아주면 조용하다.
   → **"연어를 잡을수록 참치이 멀어진다"**. 자동 회수라 이게 저절로 일어난다:
   번잡한 자리는 계속 물장구가 일어 참치이 안 오고, 조용한 자리는 참치이 들어온다.
3. **착수는 유인이지 경보가 아니다** — 코앞은 도망가도 중거리는 다가온다.
   유일한 조작에 대한 응답이 "전부 도망"이면 첫 박자가 죽는다. 단, **참치은 유인 계수 0**이라
   "작은 놈은 물장구에 몰리고 참치은 조용해져야 온다"가 규칙으로 선다.
4. **참치 접근 시간(6~11초) > 낚는 주기(약 7초)** — 이 부등식이 깨지면 2번 규칙이 있어도 참치이 그냥 통과한다.

### 유일한 의사결정 — 어디에 놓을 것인가

**찌 위치만 바꿔도 (무입력 상태에서) 점수가 1.9배, 참치 획득이 8배 갈립니다.**
마릿수는 어디 놓든 비슷한데 *무엇이 잡히는지*가 달라집니다.

| 90초 | 마릿수 | 점수 | 참치 |
|---|---|---|---|
| 완전 무입력 방치 | 10.9 | 40.9 | 0.8 |
| 자동 + 즉시 탭 | 12.9 | 40.5 | 0.6 |
| 선별(연어·멸치 놓음) | 1.9 | 36.7 | 1.1 |

세 방식의 기대 점수가 거의 같습니다. **지배 전략이 없고 구성만 달라집니다** —
탭하면 많이, 손 놓으면 참치 확률 높게, 선별하면 참치만. 아무도 의무감으로 화면을 두드리지 않습니다.

---

## 지금까지 확정된 것

| 항목 | 결정 |
|---|---|
| 시점 | 탑다운 / dorsal view |
| **코어 조작** | **찌 배치 중심.** 자동 회수 기본, 탭은 즉시 회수(양↑ 질↓) |
| **착수 반응** | 근접은 도망, **중거리는 다가옴**(유인). 참치만 유인 0 — 조용해야 온다 |
| **획득 연출** | 물보라와 함께 수면 위로 솟아 바구니로 비행. 집계는 도착 시 오름 |
| **성장/과금** | **자동 회수는 무료 기본.** 선별 회수(어종별 놓아주기)가 첫 해금이자 최대 전환점 |
| 세션 | 타이머 라운드 90초 |
| 찌 이동 | 재착수 = 놀람 + 관심 전원 리셋 |
| 도착했는데 자리 참 | 대기 없이 흥미 상실 후 궤도 복귀 |
| 관심 신호 | 아이콘 없음. 발견 동작 + 직진 + 속도 + 꼬리 비트 |
| 입질 신호 | 남은 시간 링 (자동 회수라 느낌표는 아마 불필요) |
| 보상 | 점수 + 도감 (후속: 납품 · 물고기 소모형 강화) |
| 사이즈(cm) | 필드만, MVP 미표시 |
| 리소스 | 참치만 2파트(몸통+꼬리), 나머지 통짜 |
| 그림자 | 필수 (탑다운에서는 선택 아님) |
| 애니메이션 | 정점 파동 셰이더 1개 + 트랜스폼. **스파인·2D Bone 불필요** |
| 이동 모델 | **선회율 제한 + 머리 방향 전진** (미사일 방식). 위치·회전 분리 금지 |

## 화면 프레젠테이션 A/B 비교

같은 canonical 씬에서 `FishingV2Session.PresentationVariant`만 바꿔 두 방향을 비교한다. 별도 Unity
루트나 사본 씬을 만들지 않는다. 에디터 메뉴의 `Fishing V2/Presentation/Use A - Calm Observation`
또는 `Use B - Casual Fishing`을 선택한 뒤 Play Mode를 재시작한다.

| | A · 수중 관찰 | B · 캐주얼 낚시 |
|---|---|---|
| 물 | 어두운 외곽, 유기적인 Large/Mid caustic, 중앙 관찰 영역 | 더 높은 Mid caustic 대비, 밝은 청록, 강한 중앙 시선 유도 |
| 물고기 | 짧은 꼬리 variation, 압축된 오징어 다리, 낮은 진폭 | 기준 꼬리, 현재형 촉완, 높은 실루엣 대비 |
| HUD | 시간·점수 중심의 최소 정보 | 어종 포획 수를 포함한 상세 정보 |
| 목적 | 조용히 관찰하는 낚시 연못 | 클릭 반응이 분명한 캐주얼 낚시 |

> **렌더 좌표 계약:** HTML 벤치와 동일하게 카메라는 `+Z`에서 `-Z`를 바라본다. 물은 `z=-0.20`,
> 물고기 몸통은 시각 수심에 따라 `z=0.38(surface) ~ 0.12(bottom)`, 그림자 바닥 평면은
> `z=-0.06`, 눈 원반은 메시의 `+Z` 면에 둔다. 카메라를 `-Z`로 뒤집으면 눈이 몸통 안쪽으로
> 가려지므로 깊이 값을 함께 바꾸지 않는다.

## 수중 표현 구현 기준

`FishingV2Session`은 underwater layer를 별도 카메라로 1회 렌더해 투명 RenderTexture를 만들고,
메인 카메라의 `WaterSurfaceV2` composite 쿼드가 그 RT 전체를 한 번 샘플한다. 바닥 재질 source는
`Assets/FishingV2/Art/BottomSubstrateOrganic.png`이며, 프로젝트 Editor 메뉴의
`Fishing V2/Generate Organic Bottom Substrate`로 재생성할 수 있다.

1. **Underwater Render Layer** — layer 30에 바닥 쿼드, fish, projected shadow, rock/vegetation, bobber를 둔다.
2. **Underwater RenderTexture** — `FishingV2_UnderwaterSceneRT`에 바닥·물고기·그림자·환경을 같은 시점으로 렌더한다.
3. **Water Optics Composite** — 메인 `WaterSurfaceV2`가 저주파 optical displacement로 RT 전체를 샘플한다.
4. **Water Absorption** — composite 단계에서 RT 전체에 절제된 depth/edge tint와 shared light를 적용한다.
5. **Projected Floor Caustic** — bottom layer의 substrate에만 맺히고, 최종 composite에서 fish/shadow와 함께 굴절된다.
6. **Water Color Integration** — clear cyan-teal, 중층 dark teal, 외곽/deep blue-navy 팔레트를 유지한다.
7. **Continuous Bottom Modulation** — `BottomFloorValue`/`BottomFloorGrain`의 저주파 명암·breakup 보정.
8. **Large/Mid Caustic** — broad floor-light가 주성분이고 organic breakup은 보조 성분으로 제한한다.
9. **Surface Micro Movement** — 인지 역치 아래의 고주파 움직임.
10. **Visible Surface Highlight** — shared height gradient/normal response로 넓은 수면 하이라이트를 만들고, RT scene 전체에는 낮은 강도로만 적용한다.

UI는 `FishingV2Session.OnGUI`의 메인 화면 경로에 남아 RenderTexture composite의 영향을 받지 않는다.

중앙은 단순한 백색 spotlight가 아니라 채도·fog를 조금 줄이고 대비를 올린 `Clear Observation
Zone`으로 처리한다. 외곽은 청록 흡수와 fog가 증가한다.

물고기는 이동 시뮬레이션의 X/Y를 그대로 유지하면서 종 데이터의 `VisualDepth` 범위로 시각 수심을
샘플링한다. 이 값은 실제 렌더 Z, 깊이 tint/채도/밝기, 얕은 쪽 top light, 그리고 그림자 계산에
동시에 사용된다. 그림자는 고정 화면 오프셋이 아니라 `LightDirection`과 `ShadowBottomZ`를 이용해
바닥 평면으로 투영하고, 얕은 개체는 멀고 부드럽고 낮은 알파, 깊은 개체는 가깝고 선명하고 높은
알파가 되도록 한다.

세션 Inspector의 `Presentation Overrides`를 켜면 다음 값을 별도 코드 수정 없이 조절할 수 있다.

- Clear Zone Strength / Radius
- Clear Color Strength / Depth Color Strength / Bottom Color Bleed
- Optical Distortion Strength / Scale / Speed / Caustic Floor Bias
- Fish / Shadow / Bobber Optical Strength
- Bottom Visibility / Grain Strength / Variation Scale
- Bottom Texture Scale / Blend / Contrast / Secondary Sample Strength
- Large Caustic Strength / Scale / Speed
- Mid Caustic Strength / Scale / Speed
- Micro Surface Strength
- Edge Fog Strength / Radius
- Fish Depth Tint / Desaturation / Brightness Drop / Contrast
- Shadow Depth Influence / Softness

구현 상세와 현재 기준값은 `Docs/05_수중표현_구현기준_v0.1.md`에 기록한다.

## 아직 안 정한 것

1. **화면 방향 — 가로 vs 세로** ← 유일하게 남은 큰 결정. 02 §12-1/12-2로 컨셉 뽑아 비교
2. 화면 고정 vs 스크롤 (고정 추천)
3. 세션 중 낚시터 이동 (종료 후에만 추천)
4. 도감 표시 단위 (MVP는 어종만)

---

## 다음 할 일

**유니티 구현은 지금 시작해도 됩니다.** 순서는 기획서 §12-2 참조 — 요약하면:

1. **입질 벤치를 손 놓고 90초 지켜본다** — 무입력이 지루하지 않은지가 이 방향의 진짜 관문
2. 수면 + 궤도 + 물고기 4종을 **플레이스홀더 도형으로** — 아트를 기다리지 말 것
3. **수중 표현** — bottom/fish/shadow/environment를 하나의 underwater RenderTexture로 묶는 optical composite를 검증하고, 이후 최종 아트 텍스처 교체 시 동일 계약을 유지한다.
4. 찌 + 상태머신 + 자동 회수 + 유인 → 획득 연출
5. 실제 스프라이트 교체 → 사운드 → UI/세이브 → 낚시터 2곳

병행: 02 §12-1/12-2로 **가로·세로 컨셉** 뽑아 방향 결정, 어종 4종 개별 생성

---

## 왔던 길 (같은 실수 반복 방지)

이 프로젝트에서 실제로 틀렸다가 고친 것들. 기획서 §11-1에 구현 함정이 표로 더 있다.

| 틀렸던 것 | 왜 틀렸나 |
|---|---|
| 물고기를 **측면 뷰**로 규격화 | 탑다운에서 360도 회전이 안 된다. 8자 궤도가 성립 못 함 |
| "수심대(DepthBand)" | 탑다운에서 화면 세로축은 깊이가 아니라 거리 → "구역(Zone)"으로 정정 |
| 입질에도 아이콘 금지 | 관심은 *정보*지만 입질은 **0.6~1.2초 데드라인이 붙은 입력 요구**. 다른 종류의 신호다 |
| 대기열(줄 서기) | 플레이어 판단에 아무것도 안 보태고 화면만 어지럽힌다 |
| **위치와 회전을 따로 굴림** | 속도가 0에 가까울 때 제자리에서 빙글 돈다. 물고기는 미사일처럼 **머리 방향으로만** 나아가야 한다 |
| **정지 이미지로 관심 큐 검증** | 신호가 자세가 아니라 움직임이라 한 프레임에 안 담긴다 |
| **종이 위에서 밸런스 계산** | 호기심 확률이 '개체당 초당'인데 개체 수를 안 넣었다. 실측치가 2배 이상 차이 |
| **코어를 탭 위에 얹음** | "멍하니 봐도 재밌다"가 목표인데 손을 놓으면 아무것도 안 잡혔다. 자동 회수를 기본으로 내리고 양·질 트레이드오프를 **찌 배치로 이전** |
| **착수 반응이 도망뿐** | 유일한 조작에 대한 유일한 응답이 거절이었다. 거리로 나눠 **근접=도망 / 중거리=유인**으로 |
| **잡히면 그냥 사라짐** | 조작이 거의 없는 게임에서 획득은 **유일한 보상 프레임**이다. 솟아올라 바구니로 날아가는 연출 필수 |

마지막 두 줄이 핵심 교훈이다 — **움직임과 밸런스는 돌려봐야 안다.**
