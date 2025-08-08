#!/usr/bin/env python3
"""
Patch Lua 5.1 chunk numeric constants for inventory grid in AA x2ui.
Heuristic: find prototypes whose constants include the string 'CreateInventoryItemSlots'
and replace numeric constants equal to old_cols and old_rows (defaults 13,10)
with new values.

Usage:
  lua51_patch_grid.py <file.alb> --cols 20 --rows 10 [--old-cols 13 --old-rows 10] [--dry-run]
"""
import argparse
import struct
from pathlib import Path


class Reader:
    def __init__(self, data, endian: str, sz_int: int, sz_size_t: int, sz_num: int, num_is_int: bool):
        self.data = data
        self.p = 0
        self.endian = endian
        self.sz_int = sz_int
        self.sz_size_t = sz_size_t
        self.sz_num = sz_num
        self.num_is_int = num_is_int

    def tell(self):
        return self.p

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
        s = raw[:-1]
        try:
            return s.decode('utf-8', errors='ignore')
        except Exception:
            return s.decode('latin1', errors='ignore')


def parse_and_patch(data: bytearray, cols_new: float, rows_new: float, cols_old: float, rows_old: float, dry_run: bool):
    if not data.startswith(b"\x1bLua"):
        raise ValueError("Not a Lua chunk")
    version = data[4]
    if version != 0x51:
        print(f"Warning: not Lua 5.1, version={version:02x}")
    endian_flag = data[6]
    endian = '<' if endian_flag == 1 else '>'
    sz_int = data[7]
    sz_size_t = data[8]
    sz_inst = data[9]; sz_num = data[10]
    num_is_int = data[11] != 0
    r = Reader(data[12:], endian, sz_int, sz_size_t, sz_num, num_is_int)

    patches = []  # list of (offset, old_val, new_val)

    def scan_func(depth=0):
        src = r.read_string()
        _ = r.read_int(); _ = r.read_int()
        nups = r.read_byte(); numparams = r.read_byte(); is_vararg = r.read_byte(); maxstack = r.read_byte()

        code_len = r.read_int(); r.read(code_len * 4)

        # constants
        k_len = r.read_int()
        k_offsets = []
        has_marker = False
        consts = []
        for i in range(k_len):
            t = r.read_byte()
            if t == 0:  # nil
                k_offsets.append(None)
                consts.append((t, None))
            elif t == 1:  # bool
                off = r.tell()
                b = r.read_byte()
                k_offsets.append(off)
                consts.append((t, bool(b)))
            elif t == 3:  # number
                off = r.tell()
                num = r.read_number()
                k_offsets.append(off)
                consts.append((t, num))
            elif t == 4:  # string
                off = r.tell()
                s = r.read_string()
                k_offsets.append(off)
                consts.append((t, s))
                if 'CreateInventoryItemSlots' in s:
                    has_marker = True
            else:
                # unknown type
                k_offsets.append(None)
                consts.append((t, None))

        # If this function uses CreateInventoryItemSlots, patch within this proto
        if has_marker:
            # patch numbers equal to old cols/rows
            for idx, (t, v) in enumerate(consts):
                if t == 3 and (v == cols_old or v == rows_old):
                    off = k_offsets[idx]
                    new_val = cols_new if v == cols_old else rows_new
                    patches.append((off, v, new_val))

        # Nested protos
        p_len = r.read_int()
        for _ in range(p_len):
            scan_func(depth+1)

        # Skip debug
        lineinfo_len = r.read_int(); r.read(lineinfo_len * r.sz_int)
        locvars_len = r.read_int()
        for _ in range(locvars_len):
            _ = r.read_string(); _ = r.read_int(); _ = r.read_int()
        upvals_len = r.read_int()
        for _ in range(upvals_len):
            _ = r.read_string()

    scan_func(0)

    # Apply patches
    if not patches:
        print("No patches planned (marker not found or values not present).")
        return False

    print("Planned patches:")
    for off, old, new in patches:
        print(f"  offset {off+12}: {old} -> {new}")

    if dry_run:
        return True

    # write new floats at offsets (relative to chunk start after 12-byte header)
    for off, old, new in patches:
        if num_is_int:
            raise ValueError("Chunk uses integer numbers; not supported for float patching.")
        fmt = {4: 'f', 8: 'd'}[sz_num]
        packed = struct.pack(endian + fmt, float(new))
        start = 12 + off
        data[start:start+len(packed)] = packed
    return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('file', help='path to .alb file')
    ap.add_argument('--cols', type=float, required=True, help='new columns per page (was 13)')
    ap.add_argument('--rows', type=float, required=True, help='new rows per page (was 10)')
    ap.add_argument('--old-cols', type=float, default=13.0)
    ap.add_argument('--old-rows', type=float, default=10.0)
    ap.add_argument('--dry-run', action='store_true')
    args = ap.parse_args()

    p = Path(args.file)
    original = p.read_bytes()
    data = bytearray(original)
    ok = parse_and_patch(data, args.cols, args.rows, args.old_cols, args.old_rows, args.dry_run)
    if ok and not args.dry_run:
        backup = p.with_suffix(p.suffix + '.bak')
        # Write backup of original if not already present
        if not backup.exists():
            backup.write_bytes(original)
        # Write patched file
        p.write_bytes(data)
        print(f"Patched {p}")


if __name__ == '__main__':
    main()
