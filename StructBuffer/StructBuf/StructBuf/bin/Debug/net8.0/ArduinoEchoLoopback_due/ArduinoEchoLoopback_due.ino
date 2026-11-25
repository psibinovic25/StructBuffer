// Automatski generisan Arduino Binary Echo Loopback skeč za: DUE

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

MachineRsp incomingData;


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
  if (Serial.available() >= sizeof(MachineRsp)) {
    int initialRam = getFreeRam();
    int minRam = initialRam;

   
    unsigned long t1 = micros();
    Serial.readBytes((char*)&incomingData, sizeof(MachineRsp));
    deserDuration = micros() - t1;

    int currentRam = getFreeRam();
    if (currentRam < minRam) minRam = currentRam;
    maxDeserRamUsed =  getFreeRam();//initialRam - minRam;

    incomingData.id++;

    initialRam = getFreeRam();
    minRam = initialRam;


    incomingData.dbg.deser_time = deserDuration;
    incomingData.dbg.ser_time = serDuration;
    incomingData.dbg.maxDeserRam = maxDeserRamUsed;
    incomingData.dbg.maxSerRam = maxSerRamUsed;


    unsigned long t2 = micros();
    Serial.write((uint8_t*)&incomingData, sizeof(MachineRsp));
    serDuration = micros() - t2;

    currentRam = getFreeRam();
    if (currentRam < minRam) minRam = currentRam;
    maxSerRamUsed = getFreeRam();//initialRam - minRam;

    
  }
}
