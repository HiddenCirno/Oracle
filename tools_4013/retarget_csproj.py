#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把 Oracle.csproj 的引用根目录从 I:\TKF\ 切换到 I:\TKFCoop\（4013 分支专用）。
用法: python retarget_csproj.py [--revert]
"""
import sys
import io

CSPROJ = "Oracle.csproj"
OLD = "I:\\TKF\\"
NEW = "I:\\TKFCoop\\"


def main():
    revert = "--revert" in sys.argv
    with io.open(CSPROJ, encoding="utf-8-sig") as f:
        txt = f.read()
    old = NEW if revert else OLD
    new = OLD if revert else NEW
    n = txt.count(old)
    txt = txt.replace(old, new)
    with io.open(CSPROJ, "w", encoding="utf-8", newline="\n") as f:
        f.write(txt)
    print(f"{'回退' if revert else '切换'}: {old!r} -> {new!r} 共 {n} 处")


if __name__ == "__main__":
    main()
