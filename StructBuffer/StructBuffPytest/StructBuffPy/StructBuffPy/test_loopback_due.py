# Python test skripta za arhitekturu: DUE
import serial
import time
from arduino_packets import Status

ser = serial.Serial('COM11', 115200, timeout=1)
time.sleep(2)

packet = Status()
print('Pokretanje binarnog test loop-a (10 iteracija)...')

for i in range(10):
    packet.id = i
    raw_data = packet.pack()
    
    ser.reset_input_buffer()
    t_start = time.perf_counter()
    ser.write(raw_data)

    expected_size = len(raw_data)
    time.sleep(0.05)
    received_bytes = ser.read(expected_size)
    t_end = time.perf_counter()

    if len(received_bytes) == expected_size:
        rtt_ms = (t_end - t_start) * 1000
        response_packet = Status()
        response_packet.unpack(received_bytes)
        print(f'[{i+1}] Uspešan Echo! ID: {1} | Roundtrip (RTT): {rtt_ms:.3f} ms | Veličina: {len(received_bytes)} B')
        print(f'      [MCU] Vreme deserijalizacije: {response_packet.dbg.deser_time} µs')
        print(f'      [MCU] Vreme serijalizacije:     {response_packet.dbg.ser_time} µs')
        print(f'      [RAM1] Free ram: {response_packet.dbg.maxDeserRam} µs')
        print(f'      [RAM2] Free ram:     {response_packet.dbg.maxSerRam} µs')  
    else:
        print(f'[{i+1}] Greška: Nisu stigli svi bajtovi.')
    time.sleep(0.5)

ser.close()