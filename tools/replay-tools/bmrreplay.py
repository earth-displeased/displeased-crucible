"""Minimal Python reader for BossModReborn binary replay logs (BLCB / BLOG).

Port of RawReplayParserLog.BinaryInput + op parsers. Emits a list of (timestamp_seconds, tag, dict).
Only the fields needed for combat analysis are kept, but every op is fully consumed so the stream stays aligned.
"""
import ctypes, struct, sys, datetime

try:  # Windows / any OS: pip install brotli
    import brotli as _brotli_mod
except ImportError:
    _brotli_mod = None
_dec = None if _brotli_mod else ctypes.CDLL("libbrotlidec.so.1")


def brotli_decompress(data: bytes) -> bytes:
    if _brotli_mod:
        d = _brotli_mod.Decompressor(); out = d.process(data); return out  # tolerates truncated logs
    # streaming API
    _dec.BrotliDecoderCreateInstance.restype = ctypes.c_void_p
    _dec.BrotliDecoderCreateInstance.argtypes = [ctypes.c_void_p] * 3
    _dec.BrotliDecoderDecompressStream.restype = ctypes.c_int
    _dec.BrotliDecoderDecompressStream.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_size_t), ctypes.POINTER(ctypes.c_void_p),
                                                   ctypes.POINTER(ctypes.c_size_t), ctypes.POINTER(ctypes.c_void_p), ctypes.c_void_p]
    _dec.BrotliDecoderDestroyInstance.argtypes = [ctypes.c_void_p]
    st = _dec.BrotliDecoderCreateInstance(None, None, None)
    inbuf = ctypes.create_string_buffer(data, len(data))
    avail_in = ctypes.c_size_t(len(data))
    next_in = ctypes.c_void_p(ctypes.addressof(inbuf))
    out = bytearray()
    chunk = 1 << 22
    obuf = ctypes.create_string_buffer(chunk)
    while True:
        avail_out = ctypes.c_size_t(chunk)
        next_out = ctypes.c_void_p(ctypes.addressof(obuf))
        r = _dec.BrotliDecoderDecompressStream(st, ctypes.byref(avail_in), ctypes.byref(next_in), ctypes.byref(avail_out), ctypes.byref(next_out), None)
        out += obuf.raw[:chunk - avail_out.value]
        if r == 1:  # success
            break
        if r == 0:  # error
            # truncated logs (game closed mid-write) - keep what we have
            break
        if r == 2 and avail_in.value == 0:  # needs more input -> truncated
            break
    _dec.BrotliDecoderDestroyInstance(st)
    return bytes(out)


class R:
    def __init__(s, b):
        s.b = b; s.p = 0
    def u8(s): v = s.b[s.p]; s.p += 1; return v
    def i8(s): v = struct.unpack_from('<b', s.b, s.p)[0]; s.p += 1; return v
    def b_(s): return s.u8() != 0
    def u16(s): v = struct.unpack_from('<H', s.b, s.p)[0]; s.p += 2; return v
    def i16(s): v = struct.unpack_from('<h', s.b, s.p)[0]; s.p += 2; return v
    def u32(s): v = struct.unpack_from('<I', s.b, s.p)[0]; s.p += 4; return v
    def i32(s): v = struct.unpack_from('<i', s.b, s.p)[0]; s.p += 4; return v
    def u64(s): v = struct.unpack_from('<Q', s.b, s.p)[0]; s.p += 8; return v
    def i64(s): v = struct.unpack_from('<q', s.b, s.p)[0]; s.p += 8; return v
    def f32(s): v = struct.unpack_from('<f', s.b, s.p)[0]; s.p += 4; return v
    def f64(s): v = struct.unpack_from('<d', s.b, s.p)[0]; s.p += 8; return v
    def vec3(s): v = struct.unpack_from('<fff', s.b, s.p); s.p += 12; return v
    def str_(s):
        n = 0; shift = 0
        while True:
            c = s.u8(); n |= (c & 0x7f) << shift; shift += 7
            if not c & 0x80: break
        v = s.b[s.p:s.p + n].decode('utf-8', 'replace'); s.p += n; return v
    def bytes_(s): n = s.i32(); v = s.b[s.p:s.p + n]; s.p += n; return v
    def effects(s): return [s.u64() for _ in range(8)]
    def status(s): return dict(id=s.u32(), extra=s.u16(), expire=s.i64(), source=s.u64())


def ticks_to_dt(t):
    return datetime.datetime(1, 1, 1) + datetime.timedelta(microseconds=t // 10)


def parse(path):
    raw = open(path, 'rb').read()
    magic = raw[:4]
    if magic == b'BLCB':
        data = brotli_decompress(raw[4:])
    elif magic == b'BLOG':
        data = raw[4:]
    else:
        raise ValueError('text replay not supported')
    r = R(data)
    ver = 0; ts_start = None; qpc_start = 0; inv_qpf = 1e-7
    now = None
    ops = []
    end = len(data)

    def actor(): return r.u64()

    while r.p + 4 <= end:
        tag = data[r.p:r.p + 4].decode('latin1'); r.p += 4
        try:
            o = None
            if tag == 'VER ':
                ver = r.i32(); qpf = r.u64() if ver >= 10 else 10_000_000
                gv = r.str_() if ver >= 11 else 'old'
                ts_start = ticks_to_dt(r.i64()); inv_qpf = 1.0 / qpf
                o = dict(version=ver, game=gv, start=ts_start)
            elif tag == 'FRAM':
                r.f64(); g1 = r.u64(); g2 = r.u64() if ver >= 18 else 0
                qpc = r.u64(); idx = r.u32(); r.f32(); dur = r.f32(); r.f32()
                if qpc_start == 0: qpc_start = qpc
                now = ts_start + datetime.timedelta(seconds=(qpc - qpc_start) * inv_qpf)
                if ver >= 16: r.f32()
                continue
            elif tag == 'UMRK': o = dict(text=r.str_())
            elif tag == 'RSV ': r.str_(); r.str_(); continue
            elif tag == 'ZONE': o = dict(zone=r.u16(), cfc=r.u16() if ver >= 13 else 0)
            elif tag in ('PVP+', 'PVP-'): continue
            elif tag == 'DIRU': o = dict(v=[r.u32() for _ in range(6)])
            elif tag == 'ENVC':
                if ver < 11: r.u32()
                o = dict(index=r.u8(), state=r.u32())
            elif tag == 'LEME': r.u8(); r.u8(); r.bytes_(); continue
            elif tag == 'SLOG':
                i = r.u32(); n = r.i32(); o = dict(id=i, args=[r.i32() for _ in range(n)])
            elif tag in ('WAY+', 'WAY-'):
                r.u8()
                if tag == 'WAY+': r.vec3()
                continue
            elif tag in ('SGN+', 'SGN-'):
                r.u8()
                if tag == 'SGN+': actor()
                continue
            elif tag == 'ACT+':
                o = dict(id=r.u64(), oid=r.u32(), spawn=r.i32())
                if ver >= 26: o['layout'] = r.u32()
                o['name'] = r.str_()
                if ver >= 13: o['nameid'] = r.u32()
                o['type'] = r.u16(); o['cls'] = r.u8()
                if ver >= 12: o['level'] = r.i32()
                o['pos'] = r.vec3(); o['rot'] = r.f32(); o['radius'] = r.f32()
                o['hp'] = r.u32(); o['maxhp'] = r.u32(); o['shield'] = r.u32(); o['mp'] = r.u32()
                if ver >= 24: r.u32()
                o['targetable'] = r.b_(); o['ally'] = r.b_(); o['owner'] = actor()
                if ver >= 14: o['fate'] = r.u32()
                if ver >= 27: r.i32()
            elif tag == 'ACT-': o = dict(id=actor())
            elif tag == 'NAME':
                o = dict(id=r.u64(), name=r.str_())
                if ver >= 13: r.u32()
            elif tag == 'CLSR':
                o = dict(id=actor(), cls=r.u8())
                if ver >= 12: r.i32()
            elif tag == 'MOVE':
                o = dict(id=r.u64(), pos=r.vec3(), rot=r.f32())
            elif tag == 'ACSZ': actor(); r.f32(); continue
            elif tag == 'HP  ':
                o = dict(id=actor(), hp=r.u32(), maxhp=r.u32(), shield=r.u32(), mp=r.u32())
                if ver >= 24: r.u32()
            elif tag in ('ATG+', 'ATG-'): o = dict(id=actor())
            elif tag == 'ALLY': actor(); r.b_(); continue
            elif tag in ('DIE+', 'DIE-'): o = dict(id=actor())
            elif tag in ('COM+', 'COM-'): o = dict(id=actor())
            elif tag == 'NENP': actor(); r.b_(); continue
            elif tag == 'MDLS': actor(); r.u8(); r.u8(); r.u8(); continue
            elif tag == 'EVTS': actor(); r.u8(); continue
            elif tag == 'TARG': o = dict(id=actor(), target=actor())
            elif tag == 'MNTD': actor(); r.u32(); continue
            elif tag == 'FORA': actor(); r.u8(); r.u8(); continue
            elif tag in ('TETH', 'TET+'): o = dict(id=actor(), tid=r.u32(), target=actor())
            elif tag == 'TET-': o = dict(id=actor())
            elif tag == 'RFLG': actor(); r.i32(); continue
            elif tag == 'CST+':
                o = dict(id=actor(), action=r.u32(), target=actor(), loc=r.vec3())
                if ver >= 17: o['elapsed'] = r.f32(); o['total'] = r.f32()
                else: r.i64(); o['total'] = r.f32()
                o['interruptible'] = r.b_(); o['rot'] = r.f32()
            elif tag == 'CST-': o = dict(id=actor())
            elif tag == 'CST!':
                o = dict(id=actor(), action=r.u32(), target=actor(), lock=r.f32(), seq=r.u32())
                if ver >= 6: o['pos'] = r.vec3()
                if ver >= 7: r.u32()
                if ver >= 9: r.u32()
                if ver >= 21: r.f32()
                n = r.i32(); o['targets'] = [(r.u64(), r.effects()) for _ in range(n)]
            elif tag == 'ER  ': o = dict(id=actor(), seq=r.u32(), idx=r.i32())
            elif tag in ('STA+', 'STA!'): o = dict(id=actor(), idx=r.i32(), st=r.status())
            elif tag == 'STA-': o = dict(id=actor(), idx=r.i32())
            elif tag == 'AIE+': actor(); r.i32(); r.u32(); r.i32(); actor(); r.u32(); r.effects(); continue
            elif tag == 'AIE-': actor(); r.i32(); continue
            elif tag == 'ICON':
                o = dict(id=actor(), icon=r.u32())
                if ver >= 22: o['target'] = actor()
            elif tag == 'VFX ': o = dict(id=actor(), vfx=r.u32(), target=actor())
            elif tag == 'ESTA': actor(); r.u16(); continue
            elif tag == 'EANM': actor(); r.u16(); r.u16(); continue
            elif tag == 'PATE': actor(); r.u16(); continue
            elif tag == 'PATS':
                actor(); n = r.i32()
                for _ in range(n): actor(); r.u16()
                continue
            elif tag == 'NYEL': actor(); r.u16(); continue
            elif tag == 'OPNT': actor(); continue
            elif tag == 'AVIS': actor(); r.u16(); continue
            elif tag in ('PAR ', 'PAR+', 'PAR!'):
                r.i32(); r.u64(); r.u64()
                if ver >= 15: r.b_()
                if 15 <= ver < 31: r.str_()
                continue
            elif tag == 'PAR-': r.i32(); continue
            elif tag == 'LB  ': r.i32(); r.i32(); continue
            elif tag == 'CLAR':
                o = dict(action=r.u32(), target=actor(), pos=r.vec3(), seq=r.u32(), lock=r.f32(), ce=r.f32(), ct=r.f32(), re=r.f32(), rt=r.f32())
            elif tag == 'CLRJ':
                o = dict(action=r.u32(), seq=r.u32(), re=r.f32(), rt=r.f32(), msg=r.u32())
            elif tag == 'CDN+': r.f32(); continue
            elif tag == 'CDN-': continue
            elif tag == 'CLAL': r.f32(); continue
            elif tag == 'CLCB': r.u32(); r.f32(); continue
            elif tag == 'CLST': r.i32(); r.i32(); r.i32(); continue
            elif tag == 'CLMV': r.f32(); continue
            elif tag in ('FLY+', 'FLY-'): continue
            elif tag == 'CLCD':
                r.b_(); n = r.u8()
                for _ in range(n): r.u8(); r.f32(); r.f32()
                continue
            elif tag == 'CLDA':
                if ver < 25:
                    for _ in range(2):
                        r.u32()
                        if ver >= 20: r.u8(); r.u8()
                else:
                    n = r.u8()
                    for _ in range(n): r.u32(); r.u8(); r.u8()
                continue
            elif tag == 'CLBH':
                n = r.u8()
                for _ in range(n): r.u8(); r.u8()
                continue
            elif tag == 'CBLU':
                n = r.u8(); [r.u32() for _ in range(n)]; continue
            elif tag == 'CBST':
                n = r.u8(); o = dict(beasts=[r.u8() for _ in range(n)])
            elif tag == 'CLVL':
                n = r.u8(); [r.i16() for _ in range(n)]; continue
            elif tag == 'CLAF':
                r.u32(); r.vec3(); r.f32()
                if ver >= 27: r.u8(); r.u8()
                if ver >= 29: r.u32()
                continue
            elif tag == 'CPET': o = dict(id=r.u64(), a=r.u8(), b=r.u8())
            elif tag == 'CHOC':
                r.u64(); r.u8(); r.f32()
                if ver >= 30: r.b_()
                continue
            elif tag == 'CLFT': r.u64(); continue
            elif tag == 'CLFD': r.f32(); continue
            elif tag == 'CLKV': [r.u32() for _ in range(6)]; continue
            elif tag == 'FATE': r.u32(); r.i64(); continue
            elif tag == 'HATE':
                actor(); n = r.i32()
                for _ in range(n): actor(); r.i32()
                continue
            elif tag == 'CLPR': [r.f32() for _ in range(4)]; continue
            elif tag == 'INVT': o = dict(item=r.u32(), qty=r.u32())
            elif tag == 'FLOS': r.u32(); actor(); continue
            elif tag == 'DDPG':
                r.u8(); [r.u8() for _ in range(8)]
                if ver >= 29: r.b_()
                continue
            elif tag == 'DDMP':
                if ver < 23: r.bytes_()
                else:
                    for _ in range(25): (r.u16() if ver >= 29 else r.u8())
                continue
            elif tag == 'DDPT':
                for _ in range(4): actor(); r.u8()
                continue
            elif tag == 'DDIT':
                for _ in range(16): r.u8(); r.u8()
                continue
            elif tag == 'DDCT':
                for _ in range(16): r.u8(); r.u8()
                continue
            elif tag == 'DDMG': r.bytes_(); continue
            elif tag == 'IPCI': r.u32(); continue
            elif tag == 'IPCX': [r.u32() for _ in range(5)]; continue
            elif tag == 'IPCS':
                r.i32(); r.u16(); r.u32(); r.u32(); r.i64(); r.bytes_(); continue
            else:
                print(f'unknown tag {tag!r} at {r.p}', file=sys.stderr)
                break
            ops.append((now, tag, o))
        except (struct.error, IndexError):
            break
    return ops


if __name__ == '__main__':
    ops = parse(sys.argv[1])
    from collections import Counter
    print(len(ops), Counter(t for _, t, _ in ops).most_common(40))
