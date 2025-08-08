#!/usr/bin/env python3
"""
Minimal Lua 5.1 chunk constants dumper.
Reads .alb/.luac and prints number/string/boolean constants per prototype (recursively).
This is not a decompiler; it helps locate hardcoded values in bytecode.
"""
import struct
import sys
from pathlib import Path


class Reader:
    def __init__(self, data: bytes, endian: str, sz_int: int, sz_size_t: int, sz_num: int, num_is_int: bool):
        self.data = data
        self.p = 0
        self.endian = endian
        self.sz_int = sz_int
        self.sz_size_t = sz_size_t
        self.sz_num = sz_num
        self.num_is_int = num_is_int

    def read(self, n: int) -> bytes:
        b = self.data[self.p:self.p+n]
        if len(b) != n:
            raise EOFError("Unexpected end of data")
        self.p += n
        return b

    def read_byte(self) -> int:
        return self.read(1)[0]

    def read_int(self) -> int:
        fmt = {1: 'B', 2: 'H', 4: 'I', 8: 'Q'}[self.sz_int]
        return struct.unpack(self.endian + fmt, self.read(self.sz_int))[0]

    def read_size_t(self) -> int:
        fmt = {1: 'B', 2: 'H', 4: 'I', 8: 'Q'}[self.sz_size_t]
        return struct.unpack(self.endian + fmt, self.read(self.sz_size_t))[0]

    def read_number(self):
        if self.num_is_int:
            fmt = {1: 'b', 2: 'h', 4: 'i', 8: 'q'}[self.sz_num]
            return struct.unpack(self.endian + fmt, self.read(self.sz_num))[0]
        else:
            fmt = {4: 'f', 8: 'd'}[self.sz_num]
            return struct.unpack(self.endian + fmt, self.read(self.sz_num))[0]

    def read_string(self) -> str:
        size = self.read_size_t()
        if size == 0:
            return ''
        raw = self.read(size)
        # In Lua 5.1 chunks, string size includes the trailing null terminator
        s = raw[:-1]
        try:
            return s.decode('utf-8', errors='ignore')
        except Exception:
            return s.decode('latin1', errors='ignore')


def parse_function(r: Reader, depth: int, out_lines: list):
    # source name
    src = r.read_string()
    line_defined = r.read_int()
    last_line_defined = r.read_int()
    nups = r.read_byte()
    numparams = r.read_byte()
    is_vararg = r.read_byte()
    maxstacksize = r.read_byte()

    indent = '  ' * depth
    out_lines.append(f"{indent}Function src='{src}' lines={line_defined}-{last_line_defined} nups={nups} params={numparams} vararg={is_vararg} maxstack={maxstacksize}")

    # code
    code_len = r.read_int()
    r.read(code_len * 4)  # instructions

    # constants
    k_len = r.read_int()
    if k_len:
        out_lines.append(f"{indent}  Constants ({k_len}):")
    for i in range(k_len):
        t = r.read_byte()
        if t == 0:  # nil
            out_lines.append(f"{indent}    [{i}] nil")
        elif t == 1:  # boolean
            b = r.read_byte()
            out_lines.append(f"{indent}    [{i}] bool {bool(b)}")
        elif t == 3:  # number
            num = r.read_number()
            out_lines.append(f"{indent}    [{i}] num {num}")
        elif t == 4:  # string
            s = r.read_string()
            out_lines.append(f"{indent}    [{i}] str '{s}'")
        else:
            out_lines.append(f"{indent}    [{i}] unknown_type {t}")

    # protos
    p_len = r.read_int()
    for j in range(p_len):
        parse_function(r, depth + 1, out_lines)

    # skip debug
    lineinfo_len = r.read_int()
    r.read(lineinfo_len * r.sz_int)
    locvars_len = r.read_int()
    for _ in range(locvars_len):
        _ = r.read_string()  # varname
        _ = r.read_int()     # startpc
        _ = r.read_int()     # endpc
    upvals_len = r.read_int()
    for _ in range(upvals_len):
        _ = r.read_string()


def dump_constants(path: Path):
    data = path.read_bytes()
    if not data.startswith(b"\x1bLua"):
        raise SystemExit(f"Not a Lua chunk: {path}")
    # header
    # 1B 4C 75 61 51 00 endianness int size, size_t size, instruction size, number size, num_is_int
    # signature (4) + version (1) + format (1)
    version = data[4]
    if version != 0x51:
        print(f"Warning: Lua version {version:02x} (expected 0x51)")
    format_byte = data[5]
    endian_flag = data[6]
    endian = '<' if endian_flag == 1 else '>'
    sz_int = data[7]
    sz_size_t = data[8]
    sz_inst = data[9]
    sz_num = data[10]
    num_is_int = data[11] != 0
    r = Reader(data[12:], endian, sz_int, sz_size_t, sz_num, num_is_int)
    # main function
    out = []
    parse_function(r, 0, out)
    print(f"File: {path}")
    print("\n".join(out))


def main():
    if len(sys.argv) < 2:
        print("Usage: lua51_constdump.py <file.alb> [more.alb...]")
        return 2
    for arg in sys.argv[1:]:
        dump_constants(Path(arg))


if __name__ == '__main__':
    sys.exit(main())

