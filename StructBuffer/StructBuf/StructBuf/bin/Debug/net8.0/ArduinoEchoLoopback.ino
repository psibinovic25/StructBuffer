// Automatski generisan Arduino Echo Loopback skeč

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
};

#pragma pack()

Status incomingData;

void setup() {
  Serial.begin(9600);
}

void loop() {
  // Provera da li je stigao ceo paket za strukturu
  if (Serial.available() >= sizeof(Status)) {
    Serial.readBytes((char*)&incomingData, sizeof(Status));

    // ECHO LOOPBACK: Šaljemo istu strukturu nazad na PC
    Serial.write((uint8_t*)&incomingData, sizeof(Status));
  }
}
