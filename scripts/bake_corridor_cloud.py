"""통로를 막는 안개 그림(CorridorCloud.png)에 뭉게구름별 짙기 차이를 구워 넣는다.

원본은 뭉게구름 여덟 덩이가 이어 붙은 한 장인데, 알파가 가로 전체에 걸쳐 거의 255라
통짜 벽처럼 보였다. 한때는 실행 중에 알파를 오르내리게 했지만(숨쉬기), 통로를 꽉 채운
안개가 통째로 옅어졌다 짙어졌다 하니 오히려 어색했다 — 안개는 가만히 있고, 대신
**덩이마다 짙기가 다르면** 된다. 그림 한 장에 구워 넣으면 실행 중에는 아무 일도 없다.

덩이를 가르는 법: 그림에는 덩이마다 조금 어두운 윤곽선(휘도 210~225)이 그려져 있고
속은 밝다(228~238). 휘도로 윤곽선을 떼어 내면 속만 섬처럼 남아, 그 섬을 세면 그것이
곧 덩이다. 윤곽선과 바깥 흐린 자락은 가장 가까운 섬의 배율을 물려받는다(BFS).
덩이 사이 경계는 윤곽선이 덮고 있어서 배율이 갑자기 바뀌어도 이음매가 보이지 않는다.

    python scripts/bake_corridor_cloud.py

Pillow만 있으면 된다. 결과는 제자리에 덮어쓴다.
"""

import random
from collections import deque

from PIL import Image

SOURCE = "Assets/Game/Art/Environment/CorridorCloud.png"

# 윤곽선과 속을 가르는 휘도. 히스토그램이 210~225(윤곽선)와 228~238(속)로 갈라져 있어
# 그 사이 어디를 잡아도 되지만, 안티에일리어싱된 경계가 윤곽선 쪽에 붙도록 위쪽을 잡는다.
FILL_LUMINANCE = 226

# 알파가 이보다 낮으면 그림 밖으로 본다. 바깥 자락은 여기까지만 덩이에 딸려 간다.
ALPHA_FLOOR = 40

# 섬이 이보다 작으면 덩이가 아니라 윤곽선 사이에 낀 부스러기다.
MIN_BLOB_PIXELS = 60

# 덩이별 짙기 배율의 범위. 0.86까지 내려 보면 가장 옅은 덩이가 뒤가 비쳐 구멍처럼 읽히고,
# 0.94부터는 차이가 눈에 잡히지 않는다. 0.90이 "덩이마다 다르다"와 "막혀 있다"가 함께 서는 자리다.
DENSITY_MIN, DENSITY_MAX = 0.90, 1.0

# 같은 그림이 나오도록 못박는다.
SEED = 20260730


def luminance(pixel):
    r, g, b, _ = pixel
    return (r * 299 + g * 587 + b * 114) // 1000


def label_blobs(px, width, height):
    """윤곽선으로 둘러싸인 속을 하나씩 센다. (라벨 배열, 덩이 수)"""
    labels = [[0] * width for _ in range(height)]
    count = 0
    for y in range(height):
        for x in range(width):
            if labels[y][x] or px[x, y][3] < ALPHA_FLOOR:
                continue
            if luminance(px[x, y]) < FILL_LUMINANCE:
                continue

            count += 1
            island = []
            queue = deque([(x, y)])
            labels[y][x] = count
            while queue:
                cx, cy = queue.popleft()
                island.append((cx, cy))
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if not (0 <= nx < width and 0 <= ny < height):
                        continue
                    if labels[ny][nx] or px[nx, ny][3] < ALPHA_FLOOR:
                        continue
                    if luminance(px[nx, ny]) < FILL_LUMINANCE:
                        continue
                    labels[ny][nx] = count
                    queue.append((nx, ny))

            # 부스러기는 없던 것으로 되돌린다.
            if len(island) < MIN_BLOB_PIXELS:
                for cx, cy in island:
                    labels[cy][cx] = 0
                count -= 1
    return labels, count


def spread_labels(labels, px, width, height):
    """윤곽선·바깥 자락을 가장 가까운 덩이에 붙인다. 라벨을 넓혀 나가는 BFS 한 번이면 된다."""
    queue = deque((x, y) for y in range(height) for x in range(width) if labels[y][x])
    while queue:
        cx, cy = queue.popleft()
        for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
            if not (0 <= nx < width and 0 <= ny < height):
                continue
            if labels[ny][nx] or px[nx, ny][3] == 0:
                continue
            labels[ny][nx] = labels[cy][cx]
            queue.append((nx, ny))


def pick_densities(count):
    """범위에 고르게 펼친 뒤 섞는다. 무작위로 뽑기만 하면 여덟이 한쪽에 몰리는 판이 나온다."""
    if count <= 1:
        return {1: DENSITY_MAX}
    step = (DENSITY_MAX - DENSITY_MIN) / (count - 1)
    values = [DENSITY_MIN + step * i for i in range(count)]
    random.Random(SEED).shuffle(values)
    return {i + 1: values[i] for i in range(count)}


def main():
    image = Image.open(SOURCE).convert("RGBA")
    width, height = image.size
    px = image.load()

    labels, count = label_blobs(px, width, height)
    spread_labels(labels, px, width, height)
    density = pick_densities(count)
    print(f"뭉게구름 {count}덩이")

    for y in range(height):
        for x in range(width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            scale = density.get(labels[y][x], 1.0)
            px[x, y] = (r, g, b, round(a * scale))

    image.save(SOURCE)
    for index in sorted(density):
        print(f"  {index}번 덩이 x{density[index]:.2f}")


if __name__ == "__main__":
    main()
