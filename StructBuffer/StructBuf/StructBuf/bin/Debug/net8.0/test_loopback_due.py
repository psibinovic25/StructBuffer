# Python test skripta za arhitekturu: DUE
import serial
import time
from arduino_packets import Status

ser = serial.Serial('COM3', 115200, timeout=1)
time.sleep(2)

packet = Status()
print('Pokretanje binarnog test loop-a (10 iteracija)...')

for i in range(10):
    packet.id = i
    raw_data = packet.pack()
    ser.write(raw_data)

    expected_size = len(raw_data)
    received_bytes = ser.read(expected_size)

    if len(received_bytes) == expected_size:
        response_packet = Status()
        response_packet.unpack(received_bytes)
        print(f'[{i+1}] Uspešan Echo! ID: {response_packet.id}')
        print(f'      [MCU] Vreme deserijalizacije: {response_packet.dbg.deser_time} µs')
        print(f'      [MCU] Vreme serijalizacije:    {response_packet.dbg.ser_time} µs')
    else:
        print(f'[{i+1}] Greška: Nisu stigli svi bajtovi.')
    time.sleep(0.5)

ser.close()
