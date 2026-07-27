"""PMD 폰트 시트 2장 -> Unity 비트맵 폰트용 아틀라스 + 글리프 메타데이터.

라틴 시트: 15px 간격 13x12 격자, 흰 글자 + 우하단 1px 검은 그림자(원본 그대로).
한글 시트: 10x13px 격자 64x38, 흰 글자만 있으므로 같은 그림자를 생성해 붙인다.
글리프는 셀 폭 10px을 전부 쓴다 — `ㅏ`의 가로획이 마지막 열에 있어서 9px로 자르면 `ㅣ`가 된다.
"""
import png

LAT = 'PMD_Font_Latin.png'
KOR = 'PMD_Font_Korean.png'

CELL_W, CELL_H = 12, 14      # 아틀라스 셀 간격
COLS = 64
BASELINE = 8                 # 라틴 셀 상단에서 베이스라인까지의 픽셀 수

LATIN_ROWS = [3, 16, 29, 42, 55, 68, 87, 100, 113, 126, 139, 152]
LATIN_LAYOUT = [
    "abcdefghijklm", "nopqrstuvwxyz", "ABCDEFGHIJKLM", "NOPQRSTUVWXYZ",
    "1234567890:+-", ",.¡!¿?‘’“”♂♀_",
    "àáâãäåæçèéêëì",
    "íîïðñòóôõöøœš",
    "þùúûüýÿž",
    "ÀÁÂÃÄÅÆÇÈÉÊËÌ",
    "ÍÎÏÐÑÒÓÔÕÖØŒŠ",
    "ÞÙÚÛÜÝŸŽß…",
]

# 원본 시트에 없지만 게임 UI 텍스트가 실제로 쓰는 글자들. PMD 스타일(흰 획 + 우하단 그림자)로 새로 그린다.
# '#' = 흰 획. 각 줄은 위에서부터, 마지막 줄이 베이스라인 위 첫 줄(=베이스라인 바로 위)이 되도록 top 값으로 맞춘다.
EXTRA = {
    '(': (["..#", ".#.", "#..", "#..", "#..", "#..", "#..", ".#.", "..#"], 8),
    ')': (["#..", ".#.", "..#", "..#", "..#", "..#", "..#", ".#.", "#.."], 8),
    '/': (["...#", "...#", "..#.", "..#.", ".#..", ".#..", "#...", "#..."], 8),
    '·': (["##", "##"], 4),                       # 가운뎃점
    '—': (["#######"], 4),                        # 엠 대시
    '=': (["####", "....", "####"], 5),                # 등호
    '%': (["#..#", "..#.", ".#..", "#..#"], 6),        # 퍼센트(간이형)
}


def load_latin():
    w, h, px = png.read(LAT)
    glyphs = {}
    for ri, y0 in enumerate(LATIN_ROWS):
        for ci, ch in enumerate(LATIN_LAYOUT[ri]):
            x0, x1 = ci * 15, min(ci * 15 + 15, w)
            y1 = min(y0 + 12, h)
            xs = [x for x in range(x0, x1)
                  if any(px[((y * w + x) * 4) + 3] > 0 for y in range(y0, y1))]
            ys = [y for y in range(y0, y1)
                  if any(px[((y * w + x) * 4) + 3] > 0 for x in range(x0, x1))]
            if not xs:
                raise SystemExit(f'라틴 격자 빈칸: {ri},{ci}')
            gx, gy = min(xs), min(ys)
            gw, gh = max(xs) - gx + 1, max(ys) - gy + 1
            bmp = bytearray(gw * gh * 4)
            for y in range(gh):
                for x in range(gw):
                    s = (((gy + y) * w) + gx + x) * 4
                    d = (y * gw + x) * 4
                    bmp[d:d + 4] = px[s:s + 4]
            # 셀 상단 기준 오프셋 -> 베이스라인 기준 (Y 위쪽 +)
            top = BASELINE - (gy - y0)
            glyphs[ch] = (gw, gh, top, bmp)
    return glyphs


def draw_extra():
    out = {}
    for ch, (rows, top) in EXTRA.items():
        gw = max(len(r) for r in rows) + 1     # 그림자 1px
        gh = len(rows) + 1
        bmp = bytearray(gw * gh * 4)
        def put(x, y, c):
            d = (y * gw + x) * 4
            bmp[d:d + 3] = bytes(c)
            bmp[d + 3] = 255
        for y, r in enumerate(rows):           # 그림자 먼저
            for x, c in enumerate(r):
                if c == '#':
                    put(x + 1, y + 1, (0, 0, 0))
        for y, r in enumerate(rows):
            for x, c in enumerate(r):
                if c == '#':
                    put(x, y, (255, 255, 255))
        out[ch] = (gw, gh, top, bmp)
    return out


def load_korean():
    w, h, px = png.read(KOR)
    syll = [bytes([hi, lo]).decode('euc-kr')
            for hi in range(0xB0, 0xC9) for lo in range(0xA1, 0xFF)]
    del syll[syll.index('읍')]             # 시트에 '읍'이 빠져 있다
    jamo = ('ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆ'
            'ㅇㅈㅉㅊㅋㅌㅍㅎ'
            'ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅛㅜ'
            'ㅠㅡㅣ')
    order = syll + list(jamo)                  # 셀 0 .. 2381
    assert len(order) == 2382, len(order)
    glyphs = {}
    for i, ch in enumerate(order):
        c, r = i % 64, i // 64
        gw, gh = 11, 11                        # 10x10 + 그림자 1px
        bmp = bytearray(gw * gh * 4)
        def put(x, y, c3):
            d = (y * gw + x) * 4
            bmp[d:d + 3] = bytes(c3)
            bmp[d + 3] = 255
        src = [[px[(((r * 13 + y) * w) + c * 10 + x) * 4] > 128 for x in range(10)]
               for y in range(10)]
        for y in range(10):
            for x in range(10):
                if src[y][x]:
                    put(x + 1, y + 1, (0, 0, 0))
        for y in range(10):
            for x in range(10):
                if src[y][x]:
                    put(x, y, (255, 255, 255))
        glyphs[ch] = (gw, gh, BASELINE + 1, bmp)   # 한글 블록은 베이스라인 1px 아래까지
    return glyphs


def main():
    glyphs = {}
    glyphs.update(load_latin())
    glyphs.update(draw_extra())
    glyphs.update(load_korean())
    keys = list(glyphs.keys())
    rows = (len(keys) + COLS - 1) // COLS
    W, H = COLS * CELL_W, rows * CELL_H
    atlas = bytearray(W * H * 4)
    meta = []
    for i, ch in enumerate(keys):
        gw, gh, top, bmp = glyphs[ch]
        ax, ay = (i % COLS) * CELL_W, (i // COLS) * CELL_H
        for y in range(gh):
            for x in range(gw):
                s = (y * gw + x) * 4
                d = ((ay + y) * W + ax + x) * 4
                atlas[d:d + 4] = bmp[s:s + 4]
        advance = gw                                      # 그림자 1px이 글자 사이 간격 역할을 한다
        meta.append((ord(ch), ax, ay, gw, gh, advance, 0, top - gh, ))
    png.write('PMDFont_Atlas.png', W, H, atlas)
    with open('PMDFont_glyphs.txt', 'w', encoding='utf-8') as f:
        f.write(f'{W} {H} {len(meta) + 1}\n')
        f.write('32 0 0 0 0 4 0 0\n')                     # 공백
        for m in meta:
            f.write(' '.join(str(v) for v in m) + '\n')
    print(f'글리프 {len(meta)}자, 아틀라스 {W}x{H}')


main()
