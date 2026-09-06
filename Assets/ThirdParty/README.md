# ThirdParty Assets

ProjectMediaKingdom의 IDLE / TAP·HOLD 비주얼 이펙트용 외부 에셋 모음입니다.

---

## 1. URPGlitch — TAP/HOLD 픽셀 글리치 Post-Processing

**출처**: https://github.com/saimarei/URPGlitch (mao-test-h 원본 fork, Unity 6 호환)

**내용**:
- `AnalogGlitch.shader` — VHS 스캔라인·색 수차 아날로그 글리치
- `DigitalGlitch.shader` — 블록 파편화 디지털 글리치
- `DigitalGlitchCompat.shader` — 구형 GPU 호환 버전

### Unity 임포트 방법 (권장: Package Manager Git URL)

1. Unity 메뉴 → **Window > Package Manager**
2. 좌측 상단 `+` → **Add package from git URL** 선택
3. 아래 URL 입력 후 Add:
   ```
   https://github.com/saimarei/URPGlitch.git
   ```

또는 `Packages/manifest.json`에 직접 추가:
```json
"com.saimarei.urp-glitch": "https://github.com/saimarei/URPGlitch.git"
```

### URP Renderer Feature 등록

1. **Project Settings > Graphics > URP Asset > Renderer** 열기
2. `Add Renderer Feature` → `DigitalGlitchRendererFeature` 또는 `AnalogGlitchRendererFeature` 추가
3. Camera에 붙은 **Volume** 컴포넌트에서 `Override`로 강도 조절

### TAP/HOLD 연동 예시 (C#)

```csharp
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Volume 참조 후 글리치 강도 제어
volume.profile.TryGet(out DigitalGlitch glitch);
glitch.intensity.value = Mathf.Lerp(0f, 1f, holdTime);
```

---

## 2. NovaShader — IDLE 덩굴·이끼 파티클 셰이더

**출처**: https://github.com/CyberAgentGameEntertainment/NovaShader (CyberAgent, MIT)

**내용**:
- `Runtime/` — URP 파티클 멀티 셰이더 런타임 코어
- `Editor/` — 머티리얼 인스펙터 확장

### Unity 임포트 방법 (권장: Package Manager Git URL)

1. Unity 메뉴 → **Window > Package Manager**
2. 좌측 상단 `+` → **Add package from git URL** 선택
3. 아래 URL 입력 후 Add:
   ```
   https://github.com/CyberAgentGameEntertainment/NovaShader.git?path=Assets/Nova
   ```

또는 `Packages/manifest.json`에 직접 추가:
```json
"com.cyberagent.nova": "https://github.com/CyberAgentGameEntertainment/NovaShader.git?path=Assets/Nova"
```

### 덩굴·이끼 파티클 세팅 가이드

1. **Particle System** 오브젝트 생성
2. 머티리얼 생성 → Shader를 `Nova/Particles/UberLit` 또는 `Nova/Particles/UberUnlit`으로 변경
3. **Texture** 탭에서 이끼·잎사귀 텍스처 할당
4. 권장 Particle System 세팅:

   | 항목 | IDLE 이끼·덩굴 추천값 |
   |------|----------------------|
   | Start Speed | 0.05 ~ 0.2 |
   | Start Size | 0.1 ~ 0.5 |
   | Start Lifetime | 3 ~ 8 |
   | Emission Rate | 2 ~ 8 /sec |
   | Shape | Sphere / Box (오브젝트 표면) |
   | Color over Lifetime | 초록→진초록 페이드 |
   | Renderer → Render Mode | Billboard |

5. **NovaShader Flow Map** 기능을 활성화하면 덩굴이 흔들리는 유기적 움직임 연출 가능

---

## 임시 폴더 정리

Unity가 프로젝트를 열고 있는 동안 아래 폴더가 잠겨 삭제되지 않았습니다:
```
Assets/ThirdParty/_tmp_nova
```
Unity를 닫은 뒤 수동으로 삭제해 주세요.

---

## 라이선스

| 에셋 | 라이선스 |
|------|---------|
| URPGlitch (saimarei) | MIT |
| NovaShader (CyberAgent) | MIT |
