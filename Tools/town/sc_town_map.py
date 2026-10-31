#!/usr/bin/env python3
"""Draw the Hollowmere approval maps from the layout data (Core/Town/HollowmereLayout.cs), with PIL, no Unity.

    dotnet run --project Tools/town/HollowmereDump.csproj -- <scratch>/layout.json      # validates and exports the layout (exit 1 on any rule broken)
    python3 Tools/town/sc_town_map.py <scratch>/layout.json <out dir>                  # writes Hollowmere_Map_Town.jpg and Hollowmere_Map_Square.jpg

Top-down, +Z (north) up. Ground: height tint, hillshade and 1 m / 5 m contours from the exported heightmap; lake below WaterY.
Foundations: ring colour, id and size class, door side marked; landmarks named; lanes, the town wall, the keep wall and the NPC path graph drawn; scale bar.
"""
import json, math, os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

RING = {1: (226, 170, 60), 2: (84, 160, 150), 3: (160, 110, 190)}
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"


def font(px, bold=False):
    return ImageFont.truetype(BOLD if bold else FONT, px)


class View:
    def __init__(self, x0, x1, z0, z1, ppm, margin_top=150, margin_bottom=40):
        self.x0, self.x1, self.z0, self.z1, self.ppm, self.mt = x0, x1, z0, z1, ppm, margin_top
        self.w = int((x1 - x0) * ppm)
        self.h = int((z1 - z0) * ppm) + margin_top + margin_bottom

    def p(self, x, z):
        return ((x - self.x0) * self.ppm, self.mt + (self.z1 - z) * self.ppm)


def corners(cx, cz, sx, sz, yaw):
    r = math.radians(yaw)
    ax, az = (math.cos(r), -math.sin(r)), (math.sin(r), math.cos(r))
    out = []
    for lx, lz in ((-sx / 2, -sz / 2), (sx / 2, -sz / 2), (sx / 2, sz / 2), (-sx / 2, sz / 2)):
        out.append((cx + ax[0] * lx + az[0] * lz, cz + ax[1] * lx + az[1] * lz))
    return out


def local_to_world(cx, cz, yaw, lx, lz):
    r = math.radians(yaw)
    return cx + math.cos(r) * lx + math.sin(r) * lz, cz - math.sin(r) * lx + math.cos(r) * lz


def ground_image(d, v):
    hm = d["hm"]
    nx, nz, step, x0, z0, h = hm["nx"], hm["nz"], hm["step"], hm["x0"], hm["z0"], hm["h"]

    def H(x, z):
        fx, fz = (x - x0) / step, (z - z0) / step
        i, j = max(0, min(nx - 2, int(fx))), max(0, min(nz - 2, int(fz)))
        tx, tz = min(1, max(0, fx - i)), min(1, max(0, fz - j))
        a, b = h[j * nx + i], h[j * nx + i + 1]
        c, e = h[(j + 1) * nx + i], h[(j + 1) * nx + i + 1]
        return (a * (1 - tx) + b * tx) * (1 - tz) + (c * (1 - tx) + e * tx) * tz

    # Render at 1 sample per 0.5 m (or the map scale if coarser) and scale up.
    res = 0.5 if v.ppm >= 8 else 1.0
    gw, gh = int((v.x1 - v.x0) / res), int((v.z1 - v.z0) / res)
    img = Image.new("RGB", (gw, gh))
    px = img.load()
    water = d["waterY"]
    hs = [[0.0] * gw for _ in range(gh)]
    for j in range(gh):
        z = v.z1 - (j + 0.5) * res
        for i in range(gw):
            hs[j][i] = H(v.x0 + (i + 0.5) * res, z)
    for j in range(gh):
        for i in range(gw):
            y = hs[j][i]
            if y < water:
                depth = min(1.0, (water - y) / 3.5)
                col = (int(118 - 40 * depth), int(160 - 35 * depth), int(178 - 10 * depth))
            else:
                t = max(0.0, min(1.0, (y + 10) / 18.0))   # meadow green low, ochre-green up the hill
                col = (int(150 + 50 * t), int(170 + 22 * t), int(112 + 18 * t))
                # hillshade from the north-west
                dx = hs[j][min(gw - 1, i + 1)] - hs[j][max(0, i - 1)]
                dz = hs[max(0, j - 1)][i] - hs[min(gh - 1, j + 1)][i]
                shade = max(-1.0, min(1.0, (-dx + dz) / (4 * res) * 1.6))
                col = tuple(int(max(0, min(255, c * (1 + 0.22 * shade)))) for c in col)
                # contours
                c1 = math.floor(y)
                edge = any(math.floor(hs[jj][ii]) != c1 for jj, ii in ((j, min(gw - 1, i + 1)), (min(gh - 1, j + 1), i)))
                if edge:
                    major = any(math.floor(q / 5) != math.floor(y / 5) for q in (hs[j][min(gw - 1, i + 1)], hs[min(gh - 1, j + 1)][i]))
                    k = 0.72 if major else 0.88
                    col = tuple(int(c * k) for c in col)
            px[i, j] = col
    img = img.resize((v.w, int((v.z1 - v.z0) * v.ppm)), Image.BILINEAR)
    canvas = Image.new("RGB", (v.w, v.h), (244, 238, 224))
    canvas.paste(img, (0, v.mt))
    return canvas, H


def poly_line(dr, v, pts, col, width, closed=False):
    xy = [v.p(x, z) for x, z in pts]
    if closed:
        xy.append(xy[0])
    dr.line(xy, fill=col, width=max(1, int(width)), joint="curve")


def draw(d, v, out, title, detail):
    img, H = ground_image(d, v)
    dr = ImageDraw.Draw(img, "RGBA")
    s = v.ppm
    nodes = {n["id"]: n for n in d["nodes"]}

    # stream and lanes
    poly_line(dr, v, d["stream"], (96, 150, 180), 3.5 * s)
    for lane in d["lanes"]:
        pts = [(p[0], p[1]) for p in lane["pts"]]
        poly_line(dr, v, pts, (120, 104, 84), lane["width"] * s + 2)
        poly_line(dr, v, pts, (222, 208, 180), lane["width"] * s)
    # square paving and the keep terrace
    sq = [(-20, -21), (20, -21), (20, 7), (-20, 7)]
    dr.polygon([v.p(*q) for q in sq], fill=(222, 208, 180, 255), outline=(120, 104, 84))
    kw, kz = d["keepHalfWidth"], d["keepWallZ"]
    dr.polygon([v.p(-kw, kz), v.p(kw, kz), v.p(kw, kz + 16), v.p(-kw, kz + 16)], fill=(200, 192, 170, 150))
    # walls
    for key, col, wd in (("terrace", (140, 120, 96), max(2, 0.5 * s)), ("wall", (92, 72, 58), max(3, 1.0 * s)), ("keepwall", (92, 72, 58), max(3, 1.0 * s))):
        for a in d[key]:
            dr.line([v.p(a[0], a[1]), v.p(a[2], a[3])], fill=col, width=int(wd))

    # foundations
    fs = font(max(11, int(1.5 * s)) if detail else max(10, int(1.9 * s)), True)
    fsmall = font(max(9, int(1.05 * s)) if detail else max(9, int(1.4 * s)))
    for f in d["foundations"]:
        c = corners(f["x"], f["z"], f["side"], f["side"], f["yaw"])
        col = RING[f["ring"]]
        dr.polygon([v.p(*q) for q in c], fill=col + (205,), outline=tuple(int(k * 0.55) for k in col))
        dr.line([v.p(*q) for q in c + [c[0]]], fill=tuple(int(k * 0.5) for k in col), width=max(2, int(0.18 * s)))
        # door side: a dark tab on the facing edge
        dirs = {"North": (0, 1), "East": (1, 0), "South": (0, -1), "West": (-1, 0)}
        fx, fz = dirs[f["facing"]]
        hside = f["side"] / 2
        tab = [local_to_world(f["x"], f["z"], f["yaw"], fx * hside + (-fz) * a + fx * b, fz * hside + fx * a + fz * b)
               for a, b in ((-0.9, 0), (0.9, 0), (0.9, -0.8), (-0.9, -0.8))]
        dr.polygon([v.p(*q) for q in tab], fill=(60, 40, 30, 230))
        cx, cy = v.p(f["x"], f["z"])
        label = f["id"]
        dr.text((cx, cy - 0.35 * fs.size), label, font=fs, fill=(30, 22, 16), anchor="mm")
        dr.text((cx, cy + 0.75 * fs.size), f["size"] + " %dm" % f["side"], font=fsmall, fill=(30, 22, 16), anchor="mm")

    # landmarks
    lf = font(max(11, int(1.2 * s)) if detail else max(11, int(1.6 * s)), True)
    for l in d["landmarks"]:
        c = corners(l["x"], l["z"], l["sx"], l["sz"], l["yaw"])
        fill = (70, 66, 72, 235) if l["kind"] != "Stall" else (150, 80, 60, 235)
        if l["kind"] == "Fountain":
            cx, cy = v.p(l["x"], l["z"])
            r1, r2 = 2.9 * s, 1.7 * s
            dr.ellipse([cx - r1, cy - r1, cx + r1, cy + r1], fill=(200, 182, 140, 255), outline=(110, 96, 70))
            dr.ellipse([cx - r2, cy - r2, cx + r2, cy + r2], fill=(150, 140, 120, 255), outline=(70, 60, 50))
        else:
            dr.polygon([v.p(*q) for q in c], fill=fill, outline=(20, 20, 24))

    # path graph
    ew = max(1, int(0.12 * s))
    for a, b in d["edges"]:
        na, nb = nodes[a], nodes[b]
        dr.line([v.p(na["x"], na["z"]), v.p(nb["x"], nb["z"])], fill=(200, 40, 40, 210), width=ew)
    for n in d["nodes"]:
        x, y = v.p(n["x"], n["z"])
        r = (0.32 if n["kind"] in ("Doorstep", "Door") else 0.22) * s
        r = max(1.5, r)
        col = {"Doorstep": (255, 236, 120), "Door": (255, 255, 255), "Corner": (210, 60, 60)}.get(n["kind"], (200, 40, 40))
        dr.ellipse([x - r, y - r, x + r, y + r], fill=col + (255,), outline=(90, 20, 20))

    # landmark labels last (on top of the graph), with a halo
    def halo(xy, text, f, fill=(255, 255, 255), anchor="mm"):
        dr.text(xy, text, font=f, fill=fill, anchor=anchor, stroke_width=max(2, f.size // 6), stroke_fill=(30, 26, 30))

    names = {
        "DryFountain": ("Dry fountain", (0, -4.6)), "BellTower": ("Bell tower", (0, 0)), "TitheGate": ("Sealed tithe gate", (0, -2.2)),
        "HeroesHome_Cistern": ("Heroes' home\ncistern house\n(Undercroft stair)", (0, 0.4)), "HeroesHome_Annex": ("Bell-loft\nannex", (0, 0)),
        "Mill": ("Mill", (0, 0)), "LowerGate_W": ("Lower Gate", (3.4, -3.2)), "EastGate_N": ("East Gate", (4, 2)),
        "Stall_1": ("Stall 1", (0, 2.0)), "Stall_2": ("Stall 2", (-2.6, 1.6)), "Stall_3": ("Stall 3", (2.4, -1.8)),
    }
    for l in d["landmarks"]:
        if l["id"] not in names:
            continue
        text, (ox, oz) = names[l["id"]]
        if not detail and l["kind"] == "Stall":
            continue
        x, y = v.p(l["x"] + ox, l["z"] + oz)
        halo((x, y), text, lf)
    if not detail:
        halo(v.p(0, -14), "THE SQUARE", font(int(2.2 * s), True), fill=(255, 240, 200))
    lane_labels = {"RingLane": (-56, -2, 90), "MarketStreet": (3.2, -40, 90), "WestStub": (-41, -5, 0), "EastStub": (41, -14, 0),
                   "NorthEastStub": (34, 14.2, 0), "LowerLane": (-12, -62, 62), "SaltwakeRoad": (-75, -80, 0), "MillLane": (51, -44, -38),
                   "NorthWestGinnel": (-33, 19, 0)}
    for lane in d["lanes"]:
        if lane["id"] not in lane_labels:
            continue
        x, z, rot = lane_labels[lane["id"]]
        if not (v.x0 < x < v.x1 and v.z0 < z < v.z1):
            continue
        name = lane["name"] + (" to Saltwake" if lane["id"] == "SaltwakeRoad" else "")
        f = font(max(11, int(1.4 * s)), True)
        tw = dr.textbbox((0, 0), name, font=f)
        tile = Image.new("RGBA", (tw[2] + 12, tw[3] + 12), (0, 0, 0, 0))
        ImageDraw.Draw(tile).text((6, 6), name, font=f, fill=(70, 50, 30), stroke_width=3, stroke_fill=(246, 240, 226))
        tile = tile.rotate(rot, expand=True, resample=Image.BICUBIC)
        cx, cy = v.p(x, z)
        img.paste(tile, (int(cx - tile.width / 2), int(cy - tile.height / 2)), tile)
    if not detail:
        halo(v.p(-60, 22), "old town wall (ring 2 | ring 3)", font(int(1.3 * s)), fill=(240, 230, 210))
        halo(v.p(30, -112), "LAKE", font(int(2.6 * s), True), fill=(220, 236, 244))
        halo(v.p(-2, kz + 10), "THE KEEP (+4 m terrace)", font(int(1.5 * s), True), fill=(240, 230, 210))
    else:
        halo(v.p(-14, kz + 1.6), "keep terrace wall (+4 m)", font(int(1.1 * s)), fill=(240, 230, 210))

    # header, legend, scale bar, north arrow
    dr.rectangle([0, 0, v.w, v.mt - 8], fill=(244, 238, 224, 255))
    dr.text((24, 18), title, font=font(46, True), fill=(40, 30, 22))
    counts = {}
    for f in d["foundations"]:
        counts.setdefault(f["ring"], {"S": 0, "M": 0, "L": 0})[f["size"]] += 1
    line = "   ".join("Ring %d: %d foundations (%d L, %d M, %d S)" % (r, sum(c.values()), c["L"], c["M"], c["S"]) for r, c in sorted(counts.items()))
    dr.text((26, 76), line, font=font(24), fill=(60, 48, 36))
    lx = 26
    ly = 112
    for r, name in ((1, "ring 1 (square)"), (2, "ring 2 (lanes)"), (3, "ring 3 (mill, road)")):
        dr.rectangle([lx, ly, lx + 26, ly + 22], fill=RING[r], outline=(60, 40, 30))
        dr.text((lx + 34, ly + 11), name, font=font(20), fill=(40, 30, 22), anchor="lm")
        lx += 250
    dr.rectangle([lx, ly, lx + 26, ly + 22], fill=(70, 66, 72), outline=(0, 0, 0)); dr.text((lx + 34, ly + 11), "fixed landmark", font=font(20), fill=(40, 30, 22), anchor="lm"); lx += 210
    dr.line([lx, ly + 11, lx + 30, ly + 11], fill=(200, 40, 40), width=3); dr.ellipse([lx + 11, ly + 7, lx + 19, ly + 15], fill=(255, 236, 120), outline=(90, 20, 20))
    dr.text((lx + 40, ly + 11), "NPC path graph (yellow = doorstep)", font=font(20), fill=(40, 30, 22), anchor="lm"); lx += 420
    dr.rectangle([lx, ly + 4, lx + 22, ly + 18], fill=(60, 40, 30)); dr.text((lx + 30, ly + 11), "default door side", font=font(20), fill=(40, 30, 22), anchor="lm")

    bar = 20 if detail else 50
    bx, by = 30, v.h - 30
    dr.rectangle([bx - 10, by - 44, bx + bar * s + 70, by + 16], fill=(244, 238, 224, 230))
    for k in range(5):
        x0 = bx + k * bar * s / 5
        dr.rectangle([x0, by - 10, x0 + bar * s / 5, by], fill=(30, 30, 30) if k % 2 == 0 else (250, 250, 250), outline=(30, 30, 30))
    dr.text((bx, by - 14), "0", font=font(18), fill=(30, 30, 30), anchor="ms")
    dr.text((bx + bar * s, by - 14), "%d m" % bar, font=font(18), fill=(30, 30, 30), anchor="ms")
    nx_, ny_ = v.w - 60, v.mt + 70
    dr.polygon([(nx_, ny_ - 40), (nx_ - 16, ny_ + 10), (nx_, ny_), (nx_ + 16, ny_ + 10)], fill=(40, 30, 22))
    dr.text((nx_, ny_ - 48), "N", font=font(28, True), fill=(40, 30, 22), anchor="ms")
    img.save(out, quality=90)
    print("wrote", out, img.size)


def main():
    d = json.load(open(sys.argv[1]))
    out = sys.argv[2]
    os.makedirs(out, exist_ok=True)
    draw(d, View(-100, 100, -135, 45, 12), os.path.join(out, "Hollowmere_Map_Town.jpg"), "Hollowmere: town plan (Wave 0 greybox layout)", False)
    draw(d, View(-36, 36, -38, 32, 28), os.path.join(out, "Hollowmere_Map_Square.jpg"), "Hollowmere: ring 1, the square", True)


if __name__ == "__main__":
    main()
