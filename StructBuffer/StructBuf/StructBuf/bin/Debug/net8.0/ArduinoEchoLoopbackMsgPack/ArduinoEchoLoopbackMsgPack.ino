#include <ArduinoJson.h>

// Definišemo strukturu Status
struct Arr
{
	unsigned int data[10];
  unsigned int dataLen;
};



/*comm*/
struct Status
{
	unsigned char state;
	signed char coin;
	unsigned char start;
	unsigned int time;
	unsigned long pos;
	int force;
	unsigned long score;
	Arr arr;
	
};

Status data;

void setup() {
  Serial.begin(9600);
}

void loop() {
  // Proveravamo da li ima podataka na serijskom portu
  if (Serial.available() > 0) {
    unsigned long startTime = micros();

    // Čitamo sve pristigle bajtove u MsgPack formatu
    // Koristimo StaticJsonDocument (prilagodite veličinu bafera po potrebi, npr. 200 bajtova)
    StaticJsonDocument<64> doc;
    DeserializationError error = deserializeMsgPack(doc, Serial);

    if (!error) {
      // Čupamo podatke iz MsgPack mape u našu strukturu
      data.state = doc["state"] | 0;
      data.coin = doc["coin"] | 0;
      data.start = doc["start"] | 0;
      data.time = doc["time"] | 0;
      data.pos = doc["pos"] | 0;
      data.force = doc["force"] | 0;
      data.score = doc["score"] | 0;

      // Obrada podataka (vaš primer sa uvećanjem Coina)
      data.coin++;

      // Merenje vremena obrade u mikrosekundama
      unsigned long duration = micros() - startTime;
      data.time = (unsigned int)duration;

      // Pakujemo nazad u MsgPack format
      doc["state"] = data.state;
      doc["coin"] = data.coin;
      doc["start"] = data.start;
      doc["time"] = data.time;
      doc["pos"] = data.pos;
      doc["force"] = data.force;
      doc["score"] = data.score;



      // Šaljemo serijski nazad ka PC-ju u binarnom MsgPack obliku
      serializeMsgPack(doc, Serial);
    }
  }
}