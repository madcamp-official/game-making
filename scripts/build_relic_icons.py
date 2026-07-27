"""유물 아이콘 아틀라스를 다시 만든다.

item.png는 배경이 투명이 아니라 불투명한 회색(211,211,211)이고 셀 경계에 자홍색(255,0,255)
격자 표시가 있다. 그대로 잘라 쓰면 HUD에서 아이콘마다 회색 네모가 붙어 보인다.
그래서 필요한 셀만 모아 테두리에서 흘러들어가는 배경만 투명으로 바꾼 전용 아틀라스를 만든다.

안쪽까지 색으로 지우지 않고 테두리에서 시작하는 플러드 필을 쓴다. 아이템 내부에 우연히
같은 회색이 있어도 구멍이 뚫리지 않는다.

아이콘을 바꾸려면 아래 SPRITES 표의 (행, 열)만 고치고 다시 실행한 뒤,
찍혀 나오는 internalID를 해당 유물 에셋의 icon 필드에 넣는다.
좌표는 item.png를 1부터 세는 행·열이다.

    python3 scripts/build_relic_icons.py
"""
import hashlib
import os
import sys
from collections import deque

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "Assets/ThirdParty/SpritersResource/PMD_Explorers_Fonts/Source"))
import png

CELL = 40
PITCH = 41
BG = {(211, 211, 211, 255), (255, 0, 255, 255)}

ITEM = os.path.join(ROOT, "Assets/Game/Art/Items/item.png")
ICONS = os.path.join(ROOT, "Assets/Game/Art/Items/relic_icons.png")

# (스프라이트 이름, 원본 시트, 셀 좌상단 x, y)
# item.png는 41px 격자에 좌상단이 (1,1)이고, 사용자가 준 좌표는 1부터 센 행·열이다.
def item_cell(row1, col1):
    return (ITEM, 1 + PITCH * (col1 - 1), 1 + PITCH * (row1 - 1))

SPRITES = [
    # 앞의 둘은 예전 아틀라스에서 쓰던 아이콘이다. internalID를 그대로 물려줘야
    # 이미 이 ID를 가리키는 유물 에셋의 참조가 끊기지 않는다.
    ("SitrusBerry", item_cell(10, 2), -858097255324596723),
    ("HappyEgg",    item_cell(14, 6), -9144173604413439809),

    ("EnergyRoot",  item_cell(3, 1),   None),
    ("AmuletCoin",  item_cell(13, 10), None),
    ("ChoiceBand",  item_cell(13, 7),  None),
    ("ChoiceSpecs", item_cell(26, 10), None),
    ("ChoiceScarf", item_cell(26, 6),  None),
    ("BigRoot",     item_cell(26, 9),  None),
    ("Leftovers",   item_cell(14, 9),  None),
    ("WideLens",    item_cell(25, 7),  None),
    ("ShellBell",   item_cell(16, 4),  None),
    ("LifeOrb",     item_cell(25, 11), None),
]


def sprite_id(name):
    return hashlib.md5(("relicicon:" + name).encode()).hexdigest()[:16] + "0800000000000000"


def internal_id(name):
    return -(int(hashlib.md5(("reliciconid:" + name).encode()).hexdigest()[:15], 16) | 1)


def load(path, cache={}):
    if path not in cache:
        cache[path] = png.read(path)
    return cache[path]


def cut(src, x0, y0):
    """40x40 셀을 잘라 테두리에서 이어지는 배경을 투명으로 만든다."""
    w, h, pix = load(src)
    cell = bytearray(CELL * CELL * 4)
    for y in range(CELL):
        for x in range(CELL):
            s = ((y0 + y) * w + (x0 + x)) * 4
            d = (y * CELL + x) * 4
            cell[d:d + 4] = pix[s:s + 4]

    def at(x, y):
        i = (y * CELL + x) * 4
        return tuple(cell[i:i + 4])

    seen = [[False] * CELL for _ in range(CELL)]
    q = deque()
    for i in range(CELL):
        for (x, y) in ((i, 0), (i, CELL - 1), (0, i), (CELL - 1, i)):
            if not seen[y][x] and at(x, y) in BG:
                seen[y][x] = True
                q.append((x, y))

    cleared = 0
    while q:
        x, y = q.popleft()
        d = (y * CELL + x) * 4
        cell[d:d + 4] = b"\x00\x00\x00\x00"
        cleared += 1
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < CELL and 0 <= ny < CELL and not seen[ny][nx] and at(nx, ny) in BG:
                seen[ny][nx] = True
                q.append((nx, ny))
    return cell, cleared


def main():
    out_w, out_h = CELL * len(SPRITES), CELL
    atlas = bytearray(out_w * out_h * 4)
    for i, (name, (src, x0, y0), _) in enumerate(SPRITES):
        cell, cleared = cut(src, x0, y0)
        for y in range(CELL):
            for x in range(CELL):
                s = (y * CELL + x) * 4
                d = (y * out_w + i * CELL + x) * 4
                atlas[d:d + 4] = cell[s:s + 4]
        print("  %-12s 배경 %d/1600 픽셀 제거" % (name, cleared))

    png.write(ICONS, out_w, out_h, bytes(atlas))
    print("아틀라스: %dx%d, %d개" % (out_w, out_h, len(SPRITES)))

    # --- .meta 재작성 ---
    ids = {}
    entries, table, names = [], [], []
    for i, (name, _, fixed) in enumerate(SPRITES):
        iid = fixed if fixed is not None else internal_id(name)
        ids[name] = iid
        # Unity 스프라이트 rect의 y는 텍스처 아래에서 잰다. 한 줄짜리라 항상 0.
        entries.append("""    - serializedVersion: 2
      name: {n}
      rect:
        serializedVersion: 2
        x: {x}
        y: 0
        width: 40
        height: 40
      alignment: 0
      pivot: {{x: 0, y: 0}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {sid}
      internalID: {iid}
      vertices: []
      indices:
      edges: []
      weights: []
""".format(n=name, x=i * CELL, sid=sprite_id(name), iid=iid))
        table.append("  - first:\n      213: %d\n    second: %s\n" % (iid, name))
        names.append("      %s: %d\n" % (name, iid))

    meta = open(ICONS + ".meta", encoding="utf-8").read()

    def replace_block(text, start_marker, end_marker, body):
        a = text.index(start_marker) + len(start_marker)
        b = text.index(end_marker, a)
        return text[:a] + body + text[b:]

    meta = replace_block(meta, "  internalIDToNameTable:\n", "  externalObjects:", "".join(table))
    meta = replace_block(meta, "    sprites:\n", "    outline: []\n    customData:", "".join(entries))
    meta = replace_block(meta, "    nameFileIdTable:\n", "  mipmapLimitGroupName:", "".join(names))
    open(ICONS + ".meta", "w", encoding="utf-8").write(meta)
    print(".meta 갱신 완료")

    # 유물 에셋의 icon 필드에 넣을 값. guid는 relic_icons.png의 것을 쓴다.
    print("\n유물 에셋 icon 참조용 internalID:")
    for name in sorted(ids):
        print("  %-12s %d" % (name, ids[name]))


main()
