# Automatski generisan Python fajl za arhitekturu: NANO
import struct

class Arr:
    def __init__(self):
        self.data = [32767] * 10
        self.dataLen = 32767

class Status:
    def __init__(self):
        self.state = 127
        self.coin = 0
        self.start = 127
        self.time = 32767
        self.pos = 2147483647
        self.force = 0
        self.score = 2147483647
        self.arr = Arr()
        self.dbg = Dbg()

    def unpack(self, raw_bytes):
        unpacked_data = struct.unpack('<BbBHIhIHHHHHHHHHHHIIhh', raw_bytes)
        idx = 0
        self.state = unpacked_data[idx]
        idx += 1
        self.coin = unpacked_data[idx]
        idx += 1
        self.start = unpacked_data[idx]
        idx += 1
        self.time = unpacked_data[idx]
        idx += 1
        self.pos = unpacked_data[idx]
        idx += 1
        self.force = unpacked_data[idx]
        idx += 1
        self.score = unpacked_data[idx]
        idx += 1
        self.arr.data = list(unpacked_data[idx : idx + 10])
        idx += 10
        self.arr.dataLen = unpacked_data[idx]
        idx += 1
        self.dbg.deser_time = unpacked_data[idx]
        idx += 1
        self.dbg.ser_time = unpacked_data[idx]
        idx += 1
        self.dbg.maxDeserRam = unpacked_data[idx]
        idx += 1
        self.dbg.maxSerRam = unpacked_data[idx]
        idx += 1
        return self

    def pack(self):
        return struct.pack('<BbBHIhIHHHHHHHHHHHIIhh',
            self.state,
            self.coin,
            self.start,
            self.time,
            self.pos,
            self.force,
            self.score,
            self.arr.data[0],
            self.arr.data[1],
            self.arr.data[2],
            self.arr.data[3],
            self.arr.data[4],
            self.arr.data[5],
            self.arr.data[6],
            self.arr.data[7],
            self.arr.data[8],
            self.arr.data[9],
            self.arr.dataLen,
            self.dbg.deser_time,
            self.dbg.ser_time,
            self.dbg.maxDeserRam,
            self.dbg.maxSerRam,
        )

class Dbg:
    def __init__(self):
        self.deser_time = 2147483647
        self.ser_time = 2147483647
        self.maxDeserRam = 0
        self.maxSerRam = 0

