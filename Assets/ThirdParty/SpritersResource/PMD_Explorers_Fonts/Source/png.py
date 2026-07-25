"""의존성 없는 PNG 디코더/인코더 (RGBA 8bit)."""
import zlib, struct


def read(path):
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    pos, idat, pal, trns = 8, b'', None, None
    w = h = depth = ctype = 0
    while pos < len(data):
        ln = struct.unpack('>I', data[pos:pos + 4])[0]
        typ = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + ln]
        if typ == b'IHDR':
            w, h, depth, ctype = struct.unpack('>IIBB', chunk[:10])
        elif typ == b'PLTE':
            pal = chunk
        elif typ == b'tRNS':
            trns = chunk
        elif typ == b'IDAT':
            idat += chunk
        elif typ == b'IEND':
            break
        pos += 12 + ln
    assert depth == 8, f'8bit만 지원 (depth={depth})'
    ch = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ctype]
    raw = zlib.decompress(idat)
    stride = w * ch
    out = bytearray(stride * h)
    prev = bytearray(stride)
    p = 0
    for y in range(h):
        f = raw[p]; p += 1
        line = bytearray(raw[p:p + stride]); p += stride
        if f == 1:
            for i in range(ch, stride):
                line[i] = (line[i] + line[i - ch]) & 255
        elif f == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 255
        elif f == 3:
            for i in range(stride):
                a = line[i - ch] if i >= ch else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 255
        elif f == 4:
            for i in range(stride):
                a = line[i - ch] if i >= ch else 0
                b = prev[i]
                c = prev[i - ch] if i >= ch else 0
                pp = a + b - c
                pa, pb, pc = abs(pp - a), abs(pp - b), abs(pp - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        out[y * stride:(y + 1) * stride] = line
        prev = line
    # RGBA로 변환
    px = bytearray(w * h * 4)
    for i in range(w * h):
        if ctype == 6:
            px[i * 4:i * 4 + 4] = out[i * 4:i * 4 + 4]
        elif ctype == 2:
            px[i * 4:i * 4 + 3] = out[i * 3:i * 3 + 3]; px[i * 4 + 3] = 255
        elif ctype == 0:
            v = out[i]; px[i * 4:i * 4 + 4] = bytes([v, v, v, 255])
        elif ctype == 4:
            v = out[i * 2]; px[i * 4:i * 4 + 4] = bytes([v, v, v, out[i * 2 + 1]])
        elif ctype == 3:
            idx = out[i]
            px[i * 4:i * 4 + 3] = pal[idx * 3:idx * 3 + 3]
            px[i * 4 + 3] = trns[idx] if trns and idx < len(trns) else 255
    return w, h, px


def write(path, w, h, px):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        raw += px[y * w * 4:(y + 1) * w * 4]
    def chunk(t, d):
        c = struct.pack('>I', len(d)) + t + d
        return c + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    body = (b'\x89PNG\r\n\x1a\n'
            + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
            + chunk(b'IEND', b''))
    open(path, 'wb').write(body)
