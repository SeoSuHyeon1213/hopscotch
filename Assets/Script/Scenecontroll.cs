using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;

public class Scenecontroll : MonoBehaviour
{
    public string portName = "COM3";
    public int baudRate = 9600;

    [Header("연결")]
    public TerritoryManager territoryManager;

    private SerialPort serialPort;
    private Thread readThread;
    private string latestMessage = "";
    private readonly object messageLock = new object();
    private bool isRunning = false;

    // 자연 회복 타이머 (시작부터 즉시 발동)
    private float lastActionTime;
    private const float IdleInterval = 2f;
    private bool isIdleActive;

    void Start()
    {
        lastActionTime = Time.time;

        territoryManager ??= FindObjectOfType<TerritoryManager>();

        //if (territoryManager == null)
            //Debug.LogError("[Serial] TerritoryManager를 씬에서 찾을 수 없습니다.");

        string[] available = SerialPort.GetPortNames();
        //foreach (string p in available)
            //Debug.Log($"[Serial] 감지된 포트: {p}");

        // 1순위: Inspector에 지정된 포트(기본 COM3)
        if (!string.IsNullOrEmpty(portName) && TryConnect(portName))
            return;

        // 2순위: 나머지 포트 순차 탐색
        //Debug.LogWarning($"[Serial] {portName} 연결 실패 — 다른 포트 자동 탐색 중...");
        foreach (string p in available)
        {
            if (p == portName) continue;
            if (TryConnect(p))
            {
                portName = p;
                return;
            }
        }

        Debug.LogError("[Serial] 연결 가능한 Arduino 포트를 찾을 수 없습니다.");
    }

    bool TryConnect(string port)
    {
        try
        {
            serialPort = new SerialPort(port, baudRate);
            serialPort.ReadTimeout = 2000;
            serialPort.Open();
            isRunning = true;
            readThread = new Thread(ReadSerial);
            readThread.IsBackground = true;
            readThread.Start();
            //Debug.Log($"[Serial] {port} 연결 성공");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Serial] {port} 연결 실패: {e.Message}");
            if (serialPort != null)
            {
                try { serialPort.Close(); } catch { }
                serialPort = null;
            }
            return false;
        }
    }

    void ReadSerial()
    {
        while (isRunning)
        {
            try
            {
                string line = serialPort.ReadLine().Trim();
                if (line == "TAP" || line == "HOLD" || line == "IDLE")
                {
                    lock (messageLock)
                    {
                        latestMessage = line;
                    }
                }
            }
            catch (TimeoutException) { }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogWarning($"[Serial] 읽기 오류: {e.Message}");
            }
        }
    }

    void Update()
    {
        string msg;
        lock (messageLock)
        {
            msg = latestMessage;
            latestMessage = "";
        }

        if (territoryManager == null) return;

        // TAP / HOLD: 즉시 처리 + IDLE 상태 해제
        if (msg == "TAP")
        {
            lastActionTime = Time.time;
            isIdleActive   = false;
            //Debug.Log("[Serial] TAP - 인간 침범");
            territoryManager.OnTap();
        }
        else if (msg == "HOLD")
        {
            lastActionTime = Time.time;
            isIdleActive   = false;
            //Debug.Log("[Serial] HOLD - 지속 점령");
            territoryManager.OnHold();
        }

        // IDLE: Arduino 신호 OR Unity 타이머 — 조건 충족 시 매 프레임 호출
        bool idleFromArduino = msg == "IDLE";
        bool idleFromTimer   = Time.time - lastActionTime >= IdleInterval;

        if (idleFromArduino || idleFromTimer)
        {
            if (!isIdleActive)
                //Debug.Log("[Serial] IDLE - 자연 회복 시작");
            isIdleActive = true;
            territoryManager.OnIdle();
        }
        else
        {
            isIdleActive = false;
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        if (serialPort != null && serialPort.IsOpen)
            serialPort.Close();
        if (readThread != null && readThread.IsAlive)
            readThread.Join(500);
    }
}