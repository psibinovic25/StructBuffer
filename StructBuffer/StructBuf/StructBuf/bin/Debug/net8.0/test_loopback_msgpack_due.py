# Python MsgPack Loopback Benchmark Test za arhitekturu: DUE
import serial
import time
import msgpack

ser = serial.Serial('COM3', 115200, timeout=2)
time.sleep(2)

data_payload = {
    'state': 127,
    'coin': 0,
    'start': 127,
    'time': 2147483647,
    'pos': 2147483647,
    'force': 0,
    'score': 2147483647,
    'arr': {
        'data': 2147483647,
        'dataLen': 2147483647,
    },
}
print('Pokretanje MsgPack test loop-a (10 iteracija)...')

for i in range(10):
    data_payload['id'] = i
    packed_data = msgpack.packb(data_payload)

    ser.reset_input_buffer()
    t_start = time.perf_counter()
    ser.write(packed_data)

    time.sleep(0.1)
    received_raw = ser.read(ser.in_waiting or 256)
    t_end = time.perf_counter()

    if received_raw:
        try:
            unpacked_response = msgpack.unpackb(received_raw)
            rtt_ms = (t_end - t_start) * 1000
            print(f'[{i+1}] MsgPack RTT: {rtt_ms:.3f} ms | Veličina: {len(received_raw)} B')
            if 'dbg' in unpacked_response:
                dbg = unpacked_response['dbg']
                print(f'      [MCU] Vreme deserijalizacije: {dbg.get("deser_time", 0)} µs')
                print(f'      [MCU] Vreme serijalizacije:    {dbg.get("ser_time", 0)} µs')
        except Exception as e:
            print(f'[{i+1}] Greška pri raspakivanju: {e} | Primljeno bajtova: {len(received_raw)}')
    else:
        print(f'[{i+1}] Greška: Nema odziva sa Arduina.')
    time.sleep(0.5)

ser.close()
