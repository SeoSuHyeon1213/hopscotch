using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ESC 키를 누르면 애플리케이션을 종료합니다.
/// New Input System(com.unity.inputsystem) 전용.
/// 빈 GameObject에 컴포넌트로 추가하세요.
/// </summary>
public class AppQuit : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Application.Quit();
    }
}
