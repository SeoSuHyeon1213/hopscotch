const int BUTTON_PIN = 2;
const int LED_PIN    = 13;

// 신호 판별 파라미터
const unsigned long TAP_THRESHOLD  = 500;   // 0.5초 미만 뗌 = TAP
const unsigned long HOLD_THRESHOLD = 1000;  // 1초 이상 누름 = HOLD
const unsigned long IDLE_TIMEOUT   = 2000;  // 2초 무반응 = IDLE
const unsigned long DEBOUNCE_DELAY = 50;    // 50ms 이상 같은 값 유지 시 확정

// 디바운스 상태
bool rawState      = HIGH;  // digitalRead 직접 값
bool stableState   = HIGH;  // 디바운스 후 확정 상태
unsigned long debounceStart = 0;

// 타이밍
unsigned long pressStart    = 0;
unsigned long lastEventTime = 0;
bool idleSent = true;  // 시작 직후 IDLE 오발 방지

void setup() {
  Serial.begin(9600);
  pinMode(BUTTON_PIN, INPUT_PULLUP);
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, HIGH);  // 연결 즉시 LED ON
  lastEventTime = millis();
  debounceStart = millis();
}

void loop() {
  // Unity에서 LED 명령 수신
  if (Serial.available() > 0) {
    String cmd = Serial.readStringUntil('\n');
    cmd.trim();
    if (cmd == "LED_ON")  digitalWrite(LED_PIN, HIGH);
    if (cmd == "LED_OFF") digitalWrite(LED_PIN, LOW);
  }

  unsigned long now = millis();
  bool currentRaw = digitalRead(BUTTON_PIN);

  // raw 값이 바뀌면 디바운스 타이머 리셋
  if (currentRaw != rawState) {
    rawState      = currentRaw;
    debounceStart = now;
  }

  // DEBOUNCE_DELAY ms 이상 안정 유지 시 확정 상태 갱신
  bool prevStable = stableState;
  if ((now - debounceStart) >= DEBOUNCE_DELAY) {
    stableState = rawState;
  }

  // 버튼 눌림 확정 (HIGH → LOW)
  if (prevStable == HIGH && stableState == LOW) {
    pressStart    = now;
    lastEventTime = now;
    idleSent      = false;
  }

  // 버튼 떼임 확정 (LOW → HIGH)
  if (prevStable == LOW && stableState == HIGH) {
    unsigned long pressDuration = now - pressStart;
    lastEventTime = now;

    if (pressDuration >= HOLD_THRESHOLD) {
      Serial.println("HOLD");
    } else if (pressDuration < TAP_THRESHOLD) {
      Serial.println("TAP");
    }
    // TAP_THRESHOLD ~ HOLD_THRESHOLD 구간(0.5초~1초)은 신호 없음
  }

  // IDLE 판정: 마지막 이벤트 이후 2초 경과
  if (!idleSent && (now - lastEventTime) > IDLE_TIMEOUT) {
    Serial.println("IDLE");
    idleSent = true;
  }
}
