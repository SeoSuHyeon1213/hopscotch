const int BUTTON_PIN = 2;

// 상태 판별 파라미터
const unsigned long HOLD_THRESHOLD = 1000;  // 1초 이상 = HOLD
const unsigned long TAP_WINDOW     = 500;   // 0.5초 내 재입력 = TAP
const unsigned long IDLE_TIMEOUT   = 2000;  // 2초 무입력 = IDLE

bool lastState = HIGH;
unsigned long pressStart = 0;
unsigned long lastReleaseTime = 0;
int tapCount = 0;
bool idleSent = false;

void setup() {
  Serial.begin(9600);
  pinMode(BUTTON_PIN, INPUT_PULLUP); // 내부 풀업 사용
}

void loop() {
  bool currentState = digitalRead(BUTTON_PIN);
  unsigned long now = millis();

  // 버튼 눌림 감지
  if (lastState == HIGH && currentState == HIGH) {
    pressStart = now;
    tapCount++;
    idleSent = false;
  }

  // 버튼 떼어짐 감지
  if (lastState == LOW && currentState == HIGH) {
    unsigned long pressDuration = now - pressStart;

    if (pressDuration >= HOLD_THRESHOLD) {
      Serial.println("HOLD");
      tapCount = 0;
    }
    lastReleaseTime = now;
  }

  // TAP 판정: 마지막 릴리즈 후 TAP_WINDOW가 지나면 확정
  if (tapCount > 0 && (now - lastReleaseTime) > TAP_WINDOW) {
    Serial.println("TAP");
    tapCount = 0;
  }

  // IDLE 판정
  if (!idleSent && tapCount == 0 && (now - lastReleaseTime) > IDLE_TIMEOUT) {
    Serial.println("IDLE");
    idleSent = true;
  }

  lastState = currentState;
}