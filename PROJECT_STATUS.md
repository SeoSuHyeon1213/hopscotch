# 땅따먹기 — 프로젝트 현황 보고서

> 작성일: 2026-05-18 / 최종 갱신: 2026-05-25 (건물 덩굴 잠식 오버레이 추가 / BuildingCrackEffect 신규 구현 / OnIdle 매 프레임 호출 구조 수정 / idleAmount Time.deltaTime 적용)  
> 학교/학번/이름: 유원대(졸업) / 22042017 / 서수현  
> 엔진: Unity 6000.3.10f1 | Arduino Uno

---

## 작품 개요

**주제**: 지배와 잠식 — 인간 개입이 부르는 두 가지 재앙  
**컨셉**: 빨간 버튼을 누르면 인간이 자연을 파괴하고, 손을 떼면 자연이 인간 문명을 서서히 잠식하는 인터랙티브 미디어 아트 설치 작품  
**데이터 출처**: 국립중앙과학관 자연사 자원정보(NARIS)

---

## 전체 구현도: 약 82%

---

## 시스템 아키텍처

```
물리 버튼 → Arduino (TAP/HOLD/IDLE 판별)
         → USB Serial (COM3)
         → Scenecontroll.cs (Unity 수신)
         → TerritoryManager.cs (신호 라우팅)
         ├→ ZoneController.cs (영역 확장/축소)
         │    └→ AnimalManager.cs (동물 AI 연동)
         ├→ GlitchController.cs (TAP/HOLD 글리치 이펙트)
         ├→ IdleVineEffect.cs (IDLE 덩굴·이끼 파티클 + 건물 색상 잠식)
         ├→ BuildingCrackEffect.cs (IDLE 지속 시 건물 균열·붕괴 파티클)
         └→ 사운드/LED 피드백 (미구현)
```

---

## 스크립트 현황

### `ButtonReader.ino` — Arduino 버튼 판별
| 신호 | 조건 | 상태 |
|------|------|------|
| TAP | 0.5초 미만 짧게 누르고 뗌 (`TAP_THRESHOLD = 500ms`) | ✅ 완료 |
| HOLD | 1초 이상 길게 누름 (`HOLD_THRESHOLD = 1000ms`) | ✅ 완료 |
| IDLE | 2초 이상 무입력 (`IDLE_TIMEOUT = 2000ms`) | ✅ 완료 |

> 0.5초 ~ 1초 구간은 의도적으로 신호 없음 처리 (TAP·HOLD 어느 쪽도 해당 안 됨)

- ✅ **[업데이트 2026-05-23]** 소프트웨어 디바운싱 추가 (`DEBOUNCE_DELAY = 50ms`) — 핀 노이즈로 인한 TAP 오발 방지, 50ms 이상 안정 유지 시에만 상태 확정. 기존 `delay(10)` 제거 후 시간 기반 방식으로 교체
- ✅ **[업데이트 2026-05-23]** LED 상시 점등 추가 (`LED_PIN = 13`) — `setup()`에서 아두이노 시작 즉시 `HIGH`로 점등. `loop()`에서 Unity로부터 `LED_ON` / `LED_OFF` 명령 수신 시 제어 가능

> ⚠️ **`ProjectMediaKingdom.cpp` 사용 금지** — `Assets/mediakingdom/` 내 구 프로토타입 파일. 버튼 눌림 조건이 `HIGH && HIGH`(방치 상태)로 잘못 작성되어 버튼 미연결 시에도 0.5초마다 TAP이 무한 전송됨. `ButtonReader.ino`만 업로드할 것

### `Scenecontroll.cs` — Serial 수신 및 신호 라우팅
- ✅ COM3 비동기 Serial 수신 (`ReadTimeout = 2000ms`, 백그라운드 데몬 스레드)
- ✅ thread-safe 최신 메시지 보관 (`messageLock` — 프레임 단위로 Update에서 소비 후 초기화)
- ✅ TAP / HOLD / IDLE 이외 수신 문자열 자동 무시 (노이즈 필터링)
- ✅ **TerritoryManager 신호 연결 완료** (2026-05-18 작업)
- ✅ **[업데이트]** 포트 자동 탐색 — Inspector 지정 포트(COM3) 실패 시 감지된 나머지 포트를 순차적으로 시도해 자동 연결, 성공 시 `portName` 갱신 (`TryConnect` 헬퍼)
- ✅ **[업데이트]** TerritoryManager 자동 연결 — Inspector 슬롯이 비어 있으면 `Start()`에서 `FindObjectOfType<TerritoryManager>()`로 씬 내 자동 탐색 (`??=` 연산자 활용), 씬에도 없으면 에러 로그 출력
- ✅ **[업데이트]** `OnDestroy` 안전 종료 — `isRunning = false` → 포트 닫기 → `readThread.Join(500ms)` 순서로 정리 (에디터 종료 시 스레드 누수 방지)
- ✅ **[업데이트 2026-05-23]** Unity 자체 IDLE 타이머 — 게임 플레이 시작부터 TAP/HOLD 없이 `IdleInterval(2초)` 경과 시 `OnIdle()` 자동 발동. Arduino 미연결 상태에서도 자연 회복 작동.
- ✅ **[버그수정 2026-05-25]** `OnIdle()` 매 프레임 호출 구조로 변경 — 기존 `lastIdleFiredTime` 기반 2초 간격 1회 발동 방식은 `IdleVineEffect`/`BuildingCrackEffect`의 `Time.deltaTime` 누적 로직과 충돌해 이펙트가 쌓이지 않는 버그 수정. `lastIdleFiredTime` 제거, `isIdleActive` 상태 플래그 추가. IDLE 조건 충족 시 매 프레임 `OnIdle()` 호출, 진입 시 1회만 로그 출력

```csharp
// 연결된 신호 흐름
TAP  → territoryManager.OnTap()  + lastActionTime 리셋 + isIdleActive = false
HOLD → territoryManager.OnHold() + lastActionTime 리셋 + isIdleActive = false
IDLE 조건 충족 (Arduino OR 2초 타이머) → 매 프레임 territoryManager.OnIdle()
```

> **Inspector 설정 권장**: Scenecontroll 컴포넌트의 `Territory Manager` 슬롯에 TerritoryManager 게임오브젝트를 드래그 연결 (미연결 시 자동 탐색으로 폴백)

### `TerritoryManager.cs` — 영역 제어 로직
| 메서드 | 동작 | 상태 |
|--------|------|------|
| `OnTap()` | humanZone +0.1 / natureZone -0.1 | ✅ 완료 |
| `OnHold()` | humanZone +0.05 / natureZone -0.05 | ✅ 완료 |
| `OnIdle()` | natureZone +`idleAmount × Time.deltaTime` / humanZone -`idleAmount × Time.deltaTime` | ✅ 완료 |

- ✅ **[업데이트 2026-05-23]** `idleAmount` 0.08 → 0.104 (자연 회복 속도 30% 향상)
- ✅ **[버그수정 2026-05-25]** `OnIdle()`의 영역 변화량에 `Time.deltaTime` 적용 — `OnIdle()`이 매 프레임 호출되면서 `Expand/Shrink`가 초당 수백 회 적용되던 문제 수정. `idleAmount`의 단위가 **"호출당"** → **"초당"** 으로 변경됨. Inspector 값 조정 필요 시 `0.05` 수준 권장 (기존 체감 속도 유지 기준)

> ⚠️ Inspector에서 `TerritoryManager` → `Idle Amount` 값을 조정해 자연 회복 속도 튜닝 가능 (`0.104` = 초당 0.104 확장, 너무 빠르면 `0.05` 권장)

### `ZoneController.cs` — 플레인 스케일 및 오브젝트 스폰
- ✅ ~~Z축~~ **X축** 스케일 0.1 ~ 2.0 범위 Lerp 전환, hardMaxX 5.0까지 확장 가능 (2026-05-22 축 변경 / 2026-05-23 범위 갱신)
- ✅ 플레인 범위 내 나무/건물 동적 스폰 (최대 20개)
- ✅ 플레인 축소 시 오브젝트 ScaleOut 제거
- ✅ 동물 이동 범위/속도 실시간 연동
- ✅ **[업데이트]** 위치 고정 오브젝트(`fixedObjects`) 경계 페이드 — 플레인 경계에서 `fixedFadeRange` 범위 안으로 들어오면 ScaleOut, 회복되면 ScaleIn으로 자연스럽게 등장
- ✅ **[업데이트]** 동적 스폰 오브젝트가 전멸한 상태에서 Expand 시 `fixedObjects`를 템플릿으로 삼아 랜덤 위치에 복사본을 스폰 (`SpawnRandomCopies`)
- ✅ **[업데이트]** 경계선 고정 스케일 — `fixedEdgeDirection` 값으로 좌우 존이 공유하는 경계선을 고정한 채 반대쪽 방향으로만 확장/축소 (humanZone: `1`, natureZone: `-1`, 독립: `0`)
- ✅ **[업데이트 2026-05-23]** `Expand()` 상한 제거 — 침범하는 존이 기존 `maxX(2.0)` 초과 성장 가능, 상대 존 영역까지 잠식 허용
- ✅ **[업데이트 2026-05-23]** `hardMaxX` 필드 추가 (기본값 5.0) — 침범 포함 절대 최대 크기. 0으로 설정 시 무제한. Inspector에서 존별로 독립 조정 가능
- ✅ **[업데이트 2026-05-23]** `minX` 기본값 0.5 → 0.1 — 잠식 당하는 존이 거의 소멸할 때까지 축소 허용
- ✅ **[업데이트 2026-05-23]** `Shrink()` 상한 클램프 제거 — `maxX` 이상으로 확장된 존도 자연스럽게 감소 가능
- ✅ **[업데이트 2026-05-23]** `SpawnRandomCopies()` 스폰 위치 수정 — `currentX` → `targetX` 기준으로 계산, 넓어진 새 영역 안에 오브젝트 스폰
- ✅ **[업데이트 2026-05-23]** `SpawnOne()` null 체크 추가 — `prefabs`가 null일 때 NullReferenceException 방지
- ✅ **[코드 동기화]** `SpawnInExpandedStrip()` 코루틴 구현 — 확장 시 `spawnStepX`(기본 0.2)만큼 커질 때마다 새 영역(띠)에 `spawnPerStep`(기본 2)개씩 순차 스폰. `fixedEdgeDirection` 방향에 따라 확장 측에만 위치 결정. `prefabs` 미설정 시 `fixedObjects` 복사본으로 대체
- ✅ **[코드 동기화]** `spawnStepX` / `spawnPerStep` Inspector 파라미터 추가 — 확장 단계별 스폰 간격 및 개수를 독립 조정 가능
- ✅ **[코드 동기화]** `lastSpawnedAtX` 내부 상태 추적 — 축소 시 기준점 리셋, 재확장 때 새 영역부터 다시 스폰
- ✅ **[코드 동기화]** `HasActiveObjects()` 헬퍼 추가 — `fixedObjects` 및 `spawnedObjects` 전부 비어있을 때만 `Expand()` 내 `SpawnRandomCopies()` 호출 (중복 스폰 방지)
- ✅ **[업데이트 2026-05-23]** `scaleOutSpeed` Inspector 파라미터 추가 (기본값 6f) — spawned 오브젝트 제거 시 크기 축소 속도 조정 가능. 기존 `ScaleOut()` 하드코딩 3f에서 교체
- ✅ **[업데이트 2026-05-23]** `RemoveExcess()` 병렬 실행으로 변경 — 기존 순차(한 개씩 대기) 방식에서 전체 동시 ScaleOut으로 교체, 여러 오브젝트가 즉시 동시에 사라짐
- ✅ **[업데이트 2026-05-23]** `UpdateFixedObjects()` 경계 계산 `targetX` 기준으로 변경 — 기존 `currentX`(애니메이션 지연) 대신 `targetX`로 즉시 반응. `fixedEdgeDirection != 0` 시 목표 중심 위치도 함께 계산
- ✅ **[업데이트 2026-05-23]** `EffectiveMaxObjects(float x)` 헬퍼 추가 — 플레인 넓이에 비례해 허용 오브젝트 수를 동적 계산 (`maxObjects × (x - minX) / (maxX - minX)`). `Shrink()` keepCount, `SpawnInExpandedStrip()` 용량 체크, `SpawnSequence()` 용량 체크를 모두 이 값으로 교체 → 플레인이 `maxX` 초과 확장될수록 스폰 밀도 일정하게 유지
- ✅ **[버그수정 2026-05-24]** `CullOutOfBoundsSpawned()` 추가 — `Shrink()` 시 `RemoveExcess()`는 리스트 끝부터 순서 제거라 경계 밖 오브젝트가 앞쪽에 있으면 살아남는 문제 수정. `Update()` 매 프레임 `spawnedObjects` 위치를 `targetX` 경계와 비교해 밖으로 나간 오브젝트를 즉시 `ScaleOutAndDestroy`

### `AnimalManager.cs` — 동물 AI
- ✅ 영역 내 랜덤 경로 이동 (MoveTowards + Slerp 회전)
- ✅ 경계 자동 클램핑 (플레인 축소 시 밀려남 방지)
- ✅ 플레인 비율에 따른 동적 속도/이동범위 조정
- ✅ `[DefaultExecutionOrder(10)]` 어트리뷰트 적용 — `ZoneController.UpdateAnimalRange()`의 `SetBounds()` 호출 이후에 `AnimalManager.Update()`가 실행되도록 순서 보장
- ✅ `ClampPosition()` 메서드 — 플레인이 축소될 때 동물의 **현재 위치**를 매 프레임 경계 안으로 즉시 클램프 (기존 목표 지점 재조정은 `SetBounds()` 내에서 처리)

### `GlitchController.cs` — 픽셀 글리치 이펙트 *(2026-05-19 추가)*
- ✅ TAP → Digital Glitch 스파이크 (intensity 0.9 순간 피크 후 감쇠)
- ✅ HOLD → Analog Glitch 빌드업 (scanLineJitter / colorDrift / horizontalShake)
- ✅ IDLE → `TerritoryManager`에서 `OnIdle()`을 호출하지만 빈 메서드로 구현되어 있으며, Update 감쇠가 실제 복원을 처리함 *(기존 문서의 "별도 호출 불필요" 서술은 부정확 — 호출은 됨)*
- URPGlitch 패키지 활용, Volume Profile에 DigitalGlitchVolume + AnalogGlitchVolume 필요

> **Inspector 설정 필요**: `TerritoryManager`의 `Glitch Controller` 슬롯 → GlitchVolume 게임오브젝트 연결  
> **에디터 메뉴**: `Tools > MediaKingdom > Setup Effects`로 렌더러 Feature 자동 등록

### `IdleVineEffect.cs` — 덩굴·이끼 파티클 + 건물 색상 잠식 *(2026-05-19 추가 / 2026-05-25 건물 오버레이 추가)*
- ✅ IDLE 지속 시간에 비례한 파티클 방출량 빌드업 (최대 emissionRate 18)
- ✅ TAP / HOLD 발생 시 파티클 즉시 억제
- ✅ IDLE 중 크기 펄스 (살짝 흔들리는 연출)
- ✅ **[코드 동기화]** Inspector 파라미터 — `enableSizePulse`(펄스 on/off), `pulseAmount`(진폭 0.15), `pulseFrequency`(주기 0.7Hz) 독립 조정 가능
- ✅ **[업데이트 2026-05-25]** 건물 덩굴 색상 오버레이 — `humanZone.fixedObjects`에서 건물 Renderer를 수집해 `MaterialPropertyBlock`으로 `idleIntensity`에 따라 원본 색상 → 이끼 초록(`vineColor`)으로 Lerp. URP Lit(`_BaseColor`) / Legacy(`_Color`) 셰이더 자동 감지
- ✅ **[업데이트 2026-05-25]** `Start()`에서 `CollectBuildingRenderers()` 실행 — 수집 성공 시 콘솔에 건물 수 로그 출력, 실패 시 경고 출력
- NovaShader 파티클 머티리얼 할당 필요

> **Inspector 설정 필요**:  
> - `TerritoryManager`의 `Vine Effect` 슬롯 → VineMossEffect 게임오브젝트 연결  
> - `IdleVineEffect`의 `Human Zone` 슬롯 → HumanZone ZoneController 연결  
> **에디터 메뉴**: `Tools > MediaKingdom > Create VineMoss ParticleSystem in Scene`으로 오브젝트 자동 생성

### `BuildingCrackEffect.cs` — 건물 균열·붕괴 파티클 *(2026-05-25 추가)*
- ✅ `humanZone.fixedObjects` 건물별 독립 데미지(0~1) 추적 (배열 기반, GC 없음)
- ✅ `OnIdle()` 매 프레임 호출 → `damageSpeed(0.06/s)`로 데미지 누적
- ✅ `OnTapOrHold()` 호출 → `repairSpeed(0.20/s)`로 데미지 감소, 임계값 미만 시 균열 파티클 자동 제거
- ✅ `crackThreshold(0.35)` 도달 시 균열 파티클 건물에 자식으로 부착해 지속 재생
- ✅ `collapseThreshold(0.75)` 도달 시 `collapseInterval(2.5초)`마다 붕괴 버스트 파티클 스폰 후 4초 뒤 자동 파괴
- ✅ `OnDestroy()` 정리 — 씬 종료 시 활성 균열 파티클 인스턴스 전부 제거
- ⚠️ **파티클 프리팹 임시 연결 중** — 현재 `vfx_Lightning_01/02` (번개 이펙트) 임시 사용. 균열·붕괴에 맞는 프리팹으로 교체 필요
- 권장 무료 에셋: [Particle Pack](https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325) (Dust/Explosion 계열) 또는 [Free Quick Effects Vol.1](https://assetstore.unity.com/packages/vfx/particles/free-quick-effects-vol-1-304424) (URP 네이티브)

> **Inspector 설정 필요**:  
> - `BuildingCrackEffect`를 TerritoryManager 오브젝트에 Add Component  
> - `TerritoryManager`의 `Crack Effect` 슬롯 → 위 오브젝트(자기 자신) 드래그 연결 ✅ 연결 완료  
> - `BuildingCrackEffect`의 `Human Zone` 슬롯 → HumanZone ZoneController ✅ 연결 완료  
> - `Crack Particle Prefab` → 균열 파티클 프리팹 (임시: vfx_Lightning_01)  
> - `Collapse Particle Prefab` → 붕괴 파티클 프리팹 (임시: vfx_Lightning_02)

### `Editor/EffectSetup.cs` — 에디터 헬퍼 *(2026-05-19 추가)*
- ✅ `Tools > MediaKingdom > Setup Effects` — PC/Mobile 렌더러에 DigitalGlitch·AnalogGlitch Feature 자동 등록
- ✅ `Tools > MediaKingdom > Create GlitchController in Scene` — GlitchVolume 오브젝트 씬 자동 생성
- ✅ `Tools > MediaKingdom > Create VineMoss ParticleSystem in Scene` — 파티클 오브젝트 씬 자동 생성

> **참조 에셋 경로** (Setup Effects 실행 전 존재 여부 확인):  
> - `Assets/Settings/PC_Renderer.asset`  
> - `Assets/Settings/Mobile_Renderer.asset`  
> - `Assets/Settings/GlitchVolumeProfile.asset`

### `Test.cs` — 초기 프로토타입 (비활성화 완료)
- 단순 버튼 ON/OFF → X축 스케일 테스트용 코드
- **✅ 원본 코드 전부 삭제됨** — 현재 파일 내용은 `//file for claude code test so touching this file is meaningless` 한 줄만 존재. 클래스가 없으므로 컴파일 대상 아님, COM 포트 충돌 위험 해소
- 현재 `Scenecontroll.cs`가 역할을 완전히 대체 → 파일 삭제 또는 현 상태 유지 가능

---

## 에셋 현황

### 자연 영역 프리팹
| 에셋 | 종류 | 상태 |
|------|------|------|
| 토종 고슴도치 (Erinaceus amurensis) | 동물 | ✅ 프리팹 있음 |
| 시베리아 호랑이 (Panthera tigris altaica) | 동물 | ✅ 프리팹 있음 |
| 대륙 사슴 (Cervus nippon hortulorum) | 동물 | ✅ 프리팹 있음 |
| 양봉 꿀벌 (Apis mellifera) | 동물 | ✅ 프리팹 있음 |
| 비버 | 동물 | ✅ 프리팹 있음 |
| 코끼리 | 동물 | ✅ 프리팹 있음 |
| 소나무, 풀, 통나무 | 식물 | ✅ 프리팹 있음 (11종) |
| 자연 3D 모델 | 나무·암석 등 | ✅ 658개 |

### 인간 영역 프리팹
| 에셋 | 종류 | 상태 |
|------|------|------|
| 일반 단독 주택 | 건물 | ✅ 프리팹 있음 |
| 고층 빌딩 | 건물 | ✅ 프리팹 있음 |
| 건물 3D 모델 | 다양한 빌딩 | ✅ 84개 |

### 설치된 Third-party 패키지
| 경로 | 용도 | 사용 여부 |
|------|------|------|
| `Assets/ThirdParty/URPGlitch/` | AnalogGlitch + DigitalGlitch Post-processing | ✅ `GlitchController.cs`에서 사용 |
| `Assets/ThirdParty/NovaShader/` | 파티클 셰이더 | ✅ `IdleVineEffect` 머티리얼 할당 필요 |
| `Assets/CurissVRCTools/SimpleCapture/` | 에디터 스크린샷 캡처 도구 | ❌ 프로젝트에서 미사용 (삭제 가능) |
| `Assets/ParticlePack/` 또는 `Assets/FreeQuickEffects/` | 건물 균열·붕괴 파티클 프리팹 | ⚠️ 미임포트 — `BuildingCrackEffect` 프리팹 슬롯용, 임시 vfx_Lightning 사용 중 |

---

## 미구현 항목 (잔여 30%)

| 우선순위 | 항목 | 상세 | 상태 |
|----------|------|------|------|
| **2순위** | ~~픽셀 글리치 이펙트~~ | TAP/HOLD 시 Post-processing 글리치 | ✅ 완료 (GlitchController.cs) |
| **2순위** | ~~덩굴·이끼 확산 비주얼~~ | IDLE 시 파티클 빌드업 | ✅ 완료 (IdleVineEffect.cs) |
| **2순위** | 건물 균열·붕괴 연출 | IDLE 지속 시 건물 파괴 애니메이션 | ⚠️ 부분 구현 (스크립트 완성·연결 완료 / 파티클 프리팹 임시 사용 중) |
| **2순위** | 경계 충돌 파티클 | 두 영역 경계선 파티클 이펙트 | ❌ 미구현 |
| **3순위** | 사운드 시스템 | 드릴음·기계음 (개입) / 바람·벌레소리 (방치) | ❌ 미구현 |
| **3순위** | 경계 혼합 사운드 | 두 사운드가 영역 비율에 따라 혼합 | ❌ 미구현 |
| **4순위** | LED 피드백 | Unity → Arduino 역송신 (자연 우세 시 꺼짐, 인간 우세 시 점멸) | ⚠️ 부분 구현 (Arduino 수신 완료 / Unity 역송신 미구현) |

---

## 다음 작업 계획

### Step 1 (완료) — 신호 연결
- [x] `Scenecontroll` → `TerritoryManager` TAP/HOLD/IDLE 연결

### Step 2 (부분 완료) — 시각 이펙트
- [x] TAP/HOLD Post-processing 글리치 → `GlitchController.cs` 구현 완료
- [x] IDLE 덩굴·이끼 파티클 → `IdleVineEffect.cs` 구현 완료
- [~] 건물 균열·붕괴 연출 — `BuildingCrackEffect.cs` 구현·연결 완료, **파티클 프리팹 교체 필요** (현재 임시 vfx_Lightning 사용)
- [ ] 경계 충돌 파티클 — 두 영역 경계선 파티클 이펙트

### Step 3 — 사운드
- [ ] AudioSource 컴포넌트 추가
- [ ] 인간 개입 / 자연 회복 사운드 클립 추가
- [ ] 영역 비율에 따른 AudioMixer 볼륨 혼합

### Step 4 — LED 피드백
- [x] Arduino `ButtonReader.ino`에 LED 수신 처리 추가 — 시작 즉시 점등, `LED_ON`/`LED_OFF` 명령 수신 대기
- [ ] `ZoneController`에서 영역 비율 계산
- [ ] `Scenecontroll`에서 `serialPort.WriteLine(ratio)` 역송신
- [ ] Arduino에서 수신 후 PWM LED 밝기 조절

---

## 전시 설치 체크리스트

- [ ] Inspector에서 `Scenecontroll` → `Territory Manager` 슬롯 연결
- [ ] Inspector에서 `TerritoryManager` → `Glitch Controller` 슬롯 연결 (GlitchVolume 오브젝트)
- [ ] Inspector에서 `TerritoryManager` → `Vine Effect` 슬롯 연결 (VineMossEffect 오브젝트)
- [ ] Inspector에서 `IdleVineEffect` → `Human Zone` 슬롯 연결 (HumanZone ZoneController)
- [x] Inspector에서 `TerritoryManager` → `Crack Effect` 슬롯 연결 (BuildingCrackEffect 컴포넌트)
- [x] Inspector에서 `BuildingCrackEffect` → `Human Zone` 슬롯 연결 (HumanZone ZoneController)
- [ ] `BuildingCrackEffect` → `Crack Particle Prefab` / `Collapse Particle Prefab` 교체 (Particle Pack 또는 Free Quick Effects 임포트 후)
- [ ] Inspector에서 `TerritoryManager` → `Idle Amount` 값 **0.104** 로 변경
- [ ] `Tools > MediaKingdom > Setup Effects` 실행하여 렌더러 Feature 등록
- [ ] VineMossEffect 파티클에 NovaShader 머티리얼 할당
- [ ] `ZoneController` (자연/인간) 각각 prefabs 배열 할당 확인
- [ ] Inspector에서 HumanZone `ZoneController` → `Min X = 0.1`, `Hard Max X = 5.0` 확인
- [ ] Inspector에서 NatureZone `ZoneController` → `Min X = 0.1`, `Hard Max X = 5.0` 확인
- [ ] `ZoneController` humanZone `fixedEdgeDirection = 1`, natureZone `fixedEdgeDirection = -1` 설정
- [ ] Arduino에 `ButtonReader.ino` 업로드 확인 (`ProjectMediaKingdom.cpp` 업로드 금지)
- [ ] Arduino COM 포트 번호 확인 (현재 `COM4` — Inspector `portName` 값 일치 여부 확인)
- [ ] 전시용 노트북 COM 포트 일치 여부 확인
- [x] ~~씬에서 `Test.cs` 컴포넌트 제거~~ (파일 전체 주석 처리로 자동 해소)
- [ ] 30분 이상 연속 안정성 테스트
- [ ] 암막 환경 설치 (자연 잠식 연출 극대화)
