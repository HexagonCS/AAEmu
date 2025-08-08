#!/usr/bin/env python3
"""
Batch-decompile Lua 5.1 .alb (bytecode) to .lua source using unluac.

Requirements:
- Java (java -version must work)
- Tools/unluac.jar present (https://github.com/nenofite/unluac or other unluac)

Usage examples:
- python3 Tools/decompile_alb.py --root "Exported Game Pak" --out "DecompiledLua"
- python3 Tools/decompile_alb.py --root "Exported Game Pak/game/scriptsbin/x2ui" --out "DecompiledLua/x2ui" --as-alb

Flags:
- --as-alb: emit a second copy with .alb extension (plain text Lua) so you can repack as text.
- --toc: also write a toc.txt that lists relative .alb paths you can include in a toc.g.
"""
import argparse
import os
import shutil
import subprocess
from pathlib import Path


def is_lua_chunk(path: Path) -> bool:
    try:
        with path.open('rb') as f:
            sig = f.read(4)
            return sig == b"\x1bLua"
    except Exception:
        return False


def ensure_unluac(jar_path: Path):
    if not jar_path.exists():
        raise SystemExit(
            f"Missing {jar_path}. Download unluac.jar and place it there.\n"
            "Example: https://github.com/nenofite/unluac/releases (or any Lua 5.1 unluac jar)"
        )


def run_unluac(jar: Path, input_file: Path) -> str:
    # unluac prints to stdout; capture
    try:
        proc = subprocess.run(
            ["java", "-jar", str(jar), str(input_file)],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=True,
        )
        return proc.stdout.decode('utf-8', errors='replace')
    except subprocess.CalledProcessError as e:
        raise RuntimeError(f"unluac failed for {input_file}: {e.stderr.decode('utf-8', errors='replace')}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', required=True, help='root folder to scan for .alb')
    ap.add_argument('--out', required=True, help='output root for .lua/.alb (text) files')
    ap.add_argument('--as-alb', action='store_true', help='also emit text .alb copies for repacking (replaces compiled .alb in output tree)')
    ap.add_argument('--toc', action='store_true', help='write a toc.txt listing relative .alb paths produced')
    ap.add_argument('--copy-others', action='store_true', default=True, help='copy non-.alb files to the output (mirrors full tree)')
    args = ap.parse_args()

    root = Path(args.root)
    out_root = Path(args.out)
    jar = Path('Tools/unluac.jar')
    ensure_unluac(jar)

    if not root.exists():
        raise SystemExit(f"Root not found: {root}")
    out_root.mkdir(parents=True, exist_ok=True)

    produced_alb = []

    alb_files = [p for p in root.rglob('*.alb')]
    print(f"Found {len(alb_files)} .alb files under {root}")
    for alb in alb_files:
        rel = alb.relative_to(root)
        # Where to write .lua
        out_lua = out_root / rel.with_suffix('.lua')
        out_lua.parent.mkdir(parents=True, exist_ok=True)

        try:
            if is_lua_chunk(alb):
                src = run_unluac(jar, alb)
                out_lua.write_text(src, encoding='utf-8')
                print(f"decompiled: {rel}")
            else:
                # Not bytecode: copy as text (rename to .lua)
                shutil.copy2(alb, out_lua)
                print(f"copied-text: {rel}")
        except Exception as e:
            print(f"ERROR {rel}: {e}")
            continue

        if args.as_alb:
            out_alb = out_root / rel  # same rel path, .alb extension
            out_alb.parent.mkdir(parents=True, exist_ok=True)
            # Make text .alb with the same Lua source
            if out_lua.exists():
                shutil.copy2(out_lua, out_alb)
                produced_alb.append(out_alb.relative_to(out_root).as_posix())

    # Copy other files (non-.alb) to mirror the tree
    if args.copy_others:
        total_copied = 0
        for p in root.rglob('*'):
            if p.is_dir():
                continue
            if p.suffix.lower() == '.alb':
                continue  # already handled
            rel = p.relative_to(root)
            dst = out_root / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            try:
                shutil.copy2(p, dst)
                total_copied += 1
            except Exception as e:
                print(f"WARN copy {rel}: {e}")
        print(f"Copied {total_copied} non-.alb files to {out_root}")

    if args.toc and produced_alb:
        toc_path = out_root / 'toc.txt'
        toc_path.write_text("\n".join(sorted(produced_alb)), encoding='utf-8')
        print(f"Wrote toc listing: {toc_path}")

    print("Done.")


if __name__ == '__main__':
    main()
