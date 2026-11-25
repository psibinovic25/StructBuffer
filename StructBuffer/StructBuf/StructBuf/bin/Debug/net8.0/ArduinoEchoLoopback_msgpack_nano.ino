// Automatski generisan Arduino MsgPack Loopback skeč za: NANO
#include <ArduinoJson.h>
#pragma pack(1)
struct Arr {
  unsigned int data[10];
  unsigned int dataLen;
};

struct Status {
  unsigned char state;
  signed char coin;
  unsigned char start;
  unsigned int time;
  unsigned long pos;
  int force;
  unsigned long score;
  Arr arr;
  Dbg dbg;
};

struct Dbg {
  unsigned long deser_time;
  unsigned long ser_time;
  int maxDeserRam;
  int maxSerRam;
};

#pragma pack()

Status data;
uint8_t outBuffer[400];
StaticJsonDocument<400> doc;

unsigned long deserDuration = 0;
unsigned long serDuration = 0;
int maxSerRamUsed = 0;

int getFreeRam() {
  extern int __heap_start, *__brkval;
  int v;
  return (int) &v - (__brkval == 0 ? (int) &__heap_start : (int) __brkval);
}

const size_t MAX_BUFFER_SIZE = 512;
uint8_t rxBuffer[MAX_BUFFER_SIZE];

size_t readSerialToBuffer(uint8_t* buffer, size_t maxCapacity) {
  if (Serial.available() == 0) return 0;
  delay(2);
  size_t bytesRead = 0;
  while (Serial.available() > 0 && bytesRead < maxCapacity) {
    buffer[bytesRead] = Serial.read();
    bytesRead++;
    delayMicroseconds(100);
  }
  if (bytesRead == maxCapacity && Serial.available() > 0) {
    while (Serial.available() > 0) Serial.read();
  }
  return bytesRead;
}

void setup() {
  Serial.begin(115200);
  while (!Serial);
}

void loop() {
  if (!Serial.available()) return;

  int initialRam = getFreeRam();
  int minRam = initialRam;

  size_t bytesRead = readSerialToBuffer(rxBuffer, MAX_BUFFER_SIZE);
  if (bytesRead == 0) return;

  unsigned long t1 = micros();
  DeserializationError error = deserializeMsgPack(doc, rxBuffer, bytesRead);
  deserDuration = micros() - t1;

  int currentRam = getFreeRam();
  if (currentRam < minRam) minRam = currentRam;
  int maxDeserRamUsed = getFreeRam();

  if (error) return;

  data.state = doc["state"] | 0;
  data.coin = doc["coin"] | 0;
  data.start = doc["start"] | 0;
  data.time = doc["time"] | 0;
  data.pos = doc["pos"] | 0;
  data.force = doc["force"] | 0;
  data.score = doc["score"] | 0;
  JsonArray arr_arr_data = doc["arr"]["data"];
  for (int i = 0; i < 10; i++) {
    data.arr.data[i] = arr_arr_data[i] | 0;
  }
  data.arr.dataLen = doc["arr"]["dataLen"] | 0;

  data.id++;
  doc.clear();
  doc["state"] = data.state;
  doc["coin"] = data.coin;
  doc["start"] = data.start;
  doc["time"] = data.time;
  doc["pos"] = data.pos;
  doc["force"] = data.force;
  doc["score"] = data.score;
  JsonArray arrOut_arr_data = doc["arr"]["data"].to<JsonArray>();
  for (int i = 0; i < 10; i++) {
    arrOut_arr_data.add(data.arr.data[i]);
  }
  doc["arr"]["dataLen"] = data.arr.dataLen;

  doc["dbg"]["deser_time"] = deserDuration;
  doc["dbg"]["maxDeserRam"] = maxDeserRamUsed;
  doc["dbg"]["ser_time"] = serDuration;
  doc["dbg"]["maxSerRam"] = maxSerRamUsed;

  initialRam = getFreeRam();
  minRam = initialRam;

  unsigned long t2 = micros();
  size_t payloadLen = serializeMsgPack(doc, outBuffer, sizeof(outBuffer));
  serDuration = micros() - t2;

  if (payloadLen == 0) return;

  currentRam = getFreeRam();
  if (currentRam < minRam) minRam = currentRam;
  maxSerRamUsed = getFreeRam();

  Serial.write(outBuffer, payloadLen);
}
