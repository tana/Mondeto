#include <M5StickCPlus.h>

const int LED_PIN = 10;

void setup() {
  M5.begin();
  pinMode(LED_PIN, OUTPUT);
}

void loop() {
  while (Serial.available()) {
    int chr = Serial.read();
    if (chr == 'h') {
      digitalWrite(LED_PIN, HIGH);
    } else if (chr == 'l') {
      digitalWrite(LED_PIN, LOW);
    }
    Serial.println(chr);
  }
}
