// Automatski generisan Arduino MsgPack Loopback skeč za: DUE
#include <ArduinoJson.h>
#pragma pack(1) 
struct Dbg {
  unsigned long deser_time;
  unsigned long ser_time;
  int maxDeserRam;
  int maxSerRam;
};

struct Status {
  int state;
  long pushForlen;
  int repeatFor;
  int idSeq;
  int repeatForDone;
  int SwingArm;
  long materialLen;
  int sawPos;
  int sawCylinder;
  int tool;
};

struct ServoStatus {
  int st;
  long curPos;
  long setPoint;
  long positingErr;
  long maxSpd;
  long maxAcc;
  long maxDecc;
};

struct MachineRsp {
  unsigned int cmdType;
  unsigned int id;
  unsigned long timeStamp;
  Status mSt;
  ServoStatus mSrv;
  long ScannerPoints[10];
  Dbg dbg;
};

#pragma pack() 

MachineRsp data;
uint8_t outBuffer[400];
StaticJsonDocument<400> doc;


unsigned long deserDuration;
unsigned long serDuration;
int maxSerRamUsed; 

extern "C" char *sbrk(int incr);
int getFreeRam() {
  char top;
  return &top - reinterpret_cast<char*>(sbrk(0));
}


const size_t MAX_BUFFER_SIZE = 512;
uint8_t rxBuffer[MAX_BUFFER_SIZE];

// Metoda koja prikuplja podatke iz Serial bafera
// Prima pokazivač na bafer i njegov kapacitet, vraća broj pročitanih bajtova
size_t readSerialToBuffer(uint8_t* buffer, size_t maxCapacity) {
  if (Serial.available() == 0) {
    return 0;
  }

  // Mala pauza za stabilizaciju pristizanja paketa
  delay(2); 

  size_t bytesRead = 0;
  while (Serial.available() > 0 && bytesRead < maxCapacity) {
    buffer[bytesRead] = Serial.read();
    bytesRead++;
    delayMicroseconds(100); 
  }

  // Ako je poruka veća od predviđenog bafera, očisti ostatak da ne kvari sledeće čitanje
  if (bytesRead == maxCapacity && Serial.available() > 0) {
    while (Serial.available() > 0) {
      Serial.read();
    }
  }

  return bytesRead;
}

void setup() {
  Serial.begin(115200);
  while (!Serial);

  //Serial.print(getFreeRam());

}

void loop() {
  if (!Serial.available())
    return;

  int initialRam = getFreeRam();
  int minRam = initialRam;


  size_t bytesRead = readSerialToBuffer(rxBuffer, MAX_BUFFER_SIZE);
  if (bytesRead == 0) {
    return; // Nema podataka
  }


  
  // 1. Merenje vremena deserijalizacije
  unsigned long t1 = micros();
  DeserializationError error = deserializeMsgPack(doc, rxBuffer, bytesRead);
  deserDuration = micros() - t1;

  int currentRam = getFreeRam();
  if (currentRam < minRam) minRam = currentRam;
  int maxDeserRamUsed = getFreeRam();   //initialRam - minRam;

  if (error)
    return;

  data.cmdType = doc["cmdType"] | 0;
  data.id = doc["id"] | 0;
  data.timeStamp = doc["timeStamp"] | 0;
  data.mSt.state = doc["mSt"]["state"] | 0;
  data.mSt.pushForlen = doc["mSt"]["pushForlen"] | 0;
  data.mSt.repeatFor = doc["mSt"]["repeatFor"] | 0;
  data.mSt.idSeq = doc["mSt"]["idSeq"] | 0;
  data.mSt.repeatForDone = doc["mSt"]["repeatForDone"] | 0;
  data.mSt.SwingArm = doc["mSt"]["SwingArm"] | 0;
  data.mSt.materialLen = doc["mSt"]["materialLen"] | 0;
  data.mSt.sawPos = doc["mSt"]["sawPos"] | 0;
  data.mSt.sawCylinder = doc["mSt"]["sawCylinder"] | 0;
  data.mSt.tool = doc["mSt"]["tool"] | 0;
  data.mSrv.st = doc["mSrv"]["st"] | 0;
  data.mSrv.curPos = doc["mSrv"]["curPos"] | 0;
  data.mSrv.setPoint = doc["mSrv"]["setPoint"] | 0;
  data.mSrv.positingErr = doc["mSrv"]["positingErr"] | 0;
  data.mSrv.maxSpd = doc["mSrv"]["maxSpd"] | 0;
  data.mSrv.maxAcc = doc["mSrv"]["maxAcc"] | 0;
  data.mSrv.maxDecc = doc["mSrv"]["maxDecc"] | 0;
  JsonArray arr_ScannerPoints = doc["ScannerPoints"];
  for (int i = 0; i < 10; i++) {
    data.ScannerPoints[i] = arr_ScannerPoints[i] | 0;
  }

  data.id++;
  doc.clear();
  doc["cmdType"] = data.cmdType;
  doc["id"] = data.id;
  doc["timeStamp"] = data.timeStamp;
  doc["mSt"]["state"] = data.mSt.state;
  doc["mSt"]["pushForlen"] = data.mSt.pushForlen;
  doc["mSt"]["repeatFor"] = data.mSt.repeatFor;
  doc["mSt"]["idSeq"] = data.mSt.idSeq;
  doc["mSt"]["repeatForDone"] = data.mSt.repeatForDone;
  doc["mSt"]["SwingArm"] = data.mSt.SwingArm;
  doc["mSt"]["materialLen"] = data.mSt.materialLen;
  doc["mSt"]["sawPos"] = data.mSt.sawPos;
  doc["mSt"]["sawCylinder"] = data.mSt.sawCylinder;
  doc["mSt"]["tool"] = data.mSt.tool;
  doc["mSrv"]["st"] = data.mSrv.st;
  doc["mSrv"]["curPos"] = data.mSrv.curPos;
  doc["mSrv"]["setPoint"] = data.mSrv.setPoint;
  doc["mSrv"]["positingErr"] = data.mSrv.positingErr;
  doc["mSrv"]["maxSpd"] = data.mSrv.maxSpd;
  doc["mSrv"]["maxAcc"] = data.mSrv.maxAcc;
  doc["mSrv"]["maxDecc"] = data.mSrv.maxDecc;
  JsonArray arrOut_ScannerPoints = doc.createNestedArray("ScannerPoints");
  for (int i = 0; i < 10; i++) {
    arrOut_ScannerPoints.add(data.ScannerPoints[i]);
  }

  doc["dbg"]["deser_time"] = deserDuration;
  doc["dbg"]["maxDeserRam"] = maxDeserRamUsed;

  initialRam = getFreeRam();
  minRam = initialRam;

  // sajem prethodno vremem 
  doc["dbg"]["ser_time"] = serDuration;
  doc["dbg"]["maxSerRam"] = maxSerRamUsed;


  // 2. Čisto merenje SAMO serijalizacije u memorijski bafer (izolovano od UART prenosa)
  unsigned long t2 = micros();
  size_t payloadLen = serializeMsgPack(doc, outBuffer, sizeof(outBuffer));
  serDuration = micros() - t2;

  if (payloadLen == 0) {
    return;
  }

  currentRam = getFreeRam();
  if (currentRam < minRam) minRam = currentRam;
  maxSerRamUsed = getFreeRam();  //initialRam - minRam;

  // 3. Slanje već izmerenog MsgPack bajt-strama direktno na UART
  Serial.write(outBuffer, payloadLen);

 
}
