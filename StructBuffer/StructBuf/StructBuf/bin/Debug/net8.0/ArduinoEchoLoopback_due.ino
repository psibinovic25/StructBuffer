// Automatski generisan Arduino Binary Echo Loopback skeč za: DUE

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

Status incomingData;

extern "C" char *sbrk(int incr);
int getFreeRam() {
  char top;
  return &top - reinterpret_cast<char*>(sbrk(0));
}

void setup() {
  Serial.begin(115200);
  while(!Serial);
}

int maxDeserRamUsed;
int maxSerRamUsed;
unsigned long deserDuration;
unsigned long serDuration;

void loop() {
  if (Serial.available() >= sizeof(Status)) {
    int initialRam = getFreeRam();
    int minRam = initialRam;

    unsigned long t1 = micros();
    Serial.readBytes((char*)&incomingData, sizeof(Status));
    deserDuration = micros() - t1;

    int currentRam = getFreeRam();
    if (currentRam < minRam) minRam = currentRam;
    maxDeserRamUsed = getFreeRam();

    incomingData.id++;

    incomingData.dbg.deser_time = deserDuration;
    incomingData.dbg.ser_time = serDuration;
    incomingData.dbg.maxDeserRam = maxDeserRamUsed;
    incomingData.dbg.maxSerRam = maxSerRamUsed;

    unsigned long t2 = micros();
    Serial.write((uint8_t*)&incomingData, sizeof(Status));
    serDuration = micros() - t2;

    currentRam = getFreeRam();
    if (currentRam < minRam) minRam = currentRam;
    maxSerRamUsed = getFreeRam();
  }
}
