import serial
import time
import msgpack
from arduino_packets import Status

# Povezivanje na Arduino
ser = serial.Serial('COM3', 9600, timeout=1)
time.sleep(2) # Čekanje da se Arduino resetuje

packet = Status()
packet.coin = 1
packet.force = 1000

while True:
    data_dict = {
        "state": packet.state,
        "coin": packet.coin,
        "start": packet.start,
        "time": packet.time,
        "pos": packet.pos,
        "force": packet.force,
        "score": packet.score
    }
    
    # 1. Pakujemo u MsgPack i šaljemo
    raw_data = msgpack.packb(data_dict)
    ser.write(raw_data)
    print(f'Poslato {len(raw_data)} bajtova ka Arduinu.')

    # 2. Čitamo povratni echo koristeći msgpack Unpacker (sam prepoznaje granice strukture)
    unpacker = msgpack.Unpacker()
    response_dict = None
    
    start_time = time.time()
    while time.time() - start_time < 1.0:  # Timeout od 1 sekunde
        # Čitamo bajt po bajt ili u čunkovima sa serijskog porta
        chunk = ser.read(ser.in_waiting or 1)
        if chunk:
            unpacker.feed(chunk)
            try:
                # Unpacker će automatski izvući ceo objekat čim stignu svi bajtovi
                response_dict = next(unpacker)
                break
            except StopIteration:
                pass # Još uvek nisu stigli svi bajtovi, nastavljamo da punimo unpacker

    if response_dict:
        response_packet = Status()
        response_packet.state = response_dict.get("state", 0)
        response_packet.coin = response_dict.get("coin", 0)
        response_packet.start = response_dict.get("start", 0)
        response_packet.time = response_dict.get("time", 0)
        response_packet.pos = response_dict.get("pos", 0)
        response_packet.force = response_dict.get("force", 0)
        response_packet.score = response_dict.get("score", 0)

        print('Uspešno primljen echo sa Arduina!')
        packet = response_packet
        print(response_packet.__dict__)
        print(f"Vreme obrade na Arduinu: {packet.time} mikrosekundi\n")
    else:
        print('Greška: Isteklo vreme za čitanje ili nekompletna poruka.')
        
    time.sleep(0.1)

ser.close()