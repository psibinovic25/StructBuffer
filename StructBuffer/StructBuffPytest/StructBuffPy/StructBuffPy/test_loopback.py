import serial
import time
from arduino_packets import Status

# Povezivanje na Arduino (izaberite odgovarajući COM port)
ser = serial.Serial('COM10', 9600, timeout=1)
time.sleep(2) # Čekanje da se Arduino resetuje

packet = Status()
# Ovde podesite vrednosti po želji...
packet.coin = 1;
packet.force = 1000;

# 1. Pakujemo u sirove bajtove i šaljemo ka Arduinu

while(True):
    raw_data = packet.pack()
    ser.write(raw_data)
    print(f'Poslato {len(raw_data)} bajtova ka Arduinu.')

    # 2. Čekamo echo povratak sa Arduina
    expected_size = len(raw_data)
    received_bytes = ser.read(expected_size)

    if len(received_bytes) == expected_size:
        response_packet = Status()
        response_packet.unpack(received_bytes)
        print('Uspešno primljen echo sa Arduina!')
        packet = response_packet
        print(response_packet.__dict__)
    else:
        print('Greška: Nisu stigli svi bajtovi.')
    time.sleep(0.1)
ser.close()
