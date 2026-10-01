import os
import math
from PIL import Image, ImageDraw, ImageFilter

def draw_3d_cylinder_h(draw, x1, y1, x2, y2, base_rgb=(30, 136, 229), outline_rgb=(10, 50, 120)):
    """Draw a horizontal 3D pipe cylinder with gradient shine and 3D end cap."""
    h = y2 - y1
    r_cap = h / 2.0
    steps = int(h)
    
    # Outer dark outline
    draw.rounded_rectangle([x1 - 2, y1 - 2, x2 + 2, y2 + 2], radius=int(r_cap + 2), fill=outline_rgb)
    
    # Horizontal gradient stripes for 3D cylindrical lighting
    for i in range(steps):
        y = y1 + i
        t = i / float(steps)
        # cylindrical cosine highlight
        intensity = math.sin(t * math.pi)
        specular = math.pow(math.sin(t * math.pi), 4) * 0.4 if (0.1 < t < 0.45) else 0.0
        
        # Shade
        factor = 0.45 + 0.55 * intensity + specular
        r = min(255, int(base_rgb[0] * factor + (255 - base_rgb[0]) * specular))
        g = min(255, int(base_rgb[1] * factor + (255 - base_rgb[1]) * specular))
        b = min(255, int(base_rgb[2] * factor + (255 - base_rgb[2]) * specular))
        
        draw.line([(x1 + int(r_cap * 0.3), y), (x2 - int(r_cap * 0.3), y)], fill=(r, g, b, 255), width=1)
        
    # Rim caps (ellipses)
    draw.ellipse([x1 - 4, y1, x1 + r_cap, y2], fill=(int(base_rgb[0]*0.7), int(base_rgb[1]*0.7), int(base_rgb[2]*0.7), 255), outline=outline_rgb, width=2)
    draw.ellipse([x2 - r_cap, y1, x2 + 4, y2], fill=(int(base_rgb[0]*1.1), int(base_rgb[1]*1.1), int(base_rgb[2]*1.1), 255), outline=outline_rgb, width=2)
    # Inner rim ellipse highlight
    draw.ellipse([x2 - r_cap + 2, y1 + 3, x2 + 2, y2 - 3], fill=(220, 240, 255, 200))

def draw_3d_arrow(draw, tip, base_start, base_end, color=(76, 175, 80), outline=(27, 94, 32)):
    """Draw a 3D beveled arrow."""
    # Shadow
    s_offset = 3
    draw.polygon([(tip[0], tip[1] + s_offset), (base_start[0], base_start[1] + s_offset), (base_end[0], base_end[1] + s_offset)], fill=(0, 0, 0, 70))
    # Main arrow body
    draw.polygon([tip, base_start, base_end], fill=color, outline=outline)
    # Bevel highlight on one side
    draw.line([tip, base_start], fill=(255, 255, 255, 180), width=3)
    draw.line([tip, base_end], fill=(0, 0, 0, 80), width=2)

def create_3d_icons():
    S = 256  # High-res canvas 256x256
    icons = {}

    # =========================================================================
    # 1. AlignPipeElevation
    # =========================================================================
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 3D Datum / Elevation leveling plane (glow grid)
    mid_y = 128
    # Glowing level plane band
    for dy in range(-8, 9):
        alpha = int(120 * (1.0 - abs(dy) / 8.0))
        draw.line([(20, mid_y + dy), (236, mid_y + dy)], fill=(255, 193, 7, alpha), width=1)
    # Dashed center datum
    for x in range(24, 232, 28):
        draw.line([(x, mid_y), (x + 16, mid_y)], fill=(255, 248, 225, 255), width=5)

    # Left pipe (lower: Y ~ 150 - 210) moving UP
    draw_3d_cylinder_h(draw, 24, 156, 110, 214, base_rgb=(33, 150, 243), outline_rgb=(13, 71, 161))
    # Green Up Arrow lifting pipe
    draw_3d_arrow(draw, (67, 108), (44, 146), (90, 146), color=(76, 175, 80), outline=(27, 94, 32))
    # Vertical lift track
    draw.line([(67, 146), (67, 170)], fill=(76, 175, 80, 255), width=12)

    # Right pipe (higher: Y ~ 42 - 100) moving DOWN
    draw_3d_cylinder_h(draw, 146, 42, 232, 100, base_rgb=(33, 150, 243), outline_rgb=(13, 71, 161))
    # Green Down Arrow pushing pipe down
    draw_3d_arrow(draw, (189, 148), (166, 110), (212, 110), color=(76, 175, 80), outline=(27, 94, 32))
    # Vertical push track
    draw.line([(189, 86), (189, 110)], fill=(76, 175, 80, 255), width=12)

    # Floating Elevation Datum Badge "EL" in upper left
    draw.ellipse([22, 24, 66, 68], fill=(255, 179, 0, 255), outline=(191, 54, 12, 255), width=3)
    draw.line([(44, 32), (44, 60)], fill=(255, 255, 255, 255), width=4)
    draw.line([(44, 60), (58, 60)], fill=(255, 255, 255, 255), width=4)
    draw.line([(34, 46), (54, 46)], fill=(255, 255, 255, 255), width=4)

    icons["AlignPipeElevation"] = img

    # =========================================================================
    # 2. ConnectSprinklerFlexPipe
    # =========================================================================
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Top pipe cylinder
    draw_3d_cylinder_h(draw, 24, 20, 232, 68, base_rgb=(25, 118, 210), outline_rgb=(10, 45, 110))

    # Connection nipple / Tee fitting at center
    draw.rectangle([112, 64, 144, 90], fill=(21, 101, 192, 255), outline=(10, 45, 110, 255), width=3)
    draw.rectangle([118, 66, 138, 88], fill=(144, 202, 249, 200)) # highlight

    # S-Curve Flexible Braided Metal Hose with realistic ribs
    # Path spline points
    spline = []
    for t_step in range(40):
        u = t_step / 39.0
        # Smooth S-curve
        x = 128.0 + 36.0 * math.sin(u * math.pi * 1.5)
        y = 90.0 + u * 86.0
        spline.append((x, y))

    # Draw corrugated outer shadow & stainless steel rings
    for i in range(len(spline) - 1):
        p1 = spline[i]
        p2 = spline[i + 1]
        draw.line([p1, p2], fill=(55, 71, 79, 255), width=24)
        draw.line([p1, p2], fill=(255, 167, 38, 255), width=18)
        draw.line([p1, p2], fill=(255, 243, 224, 255), width=8)

    # Individual metallic rib rings
    for i in range(2, len(spline) - 2, 2):
        pt = spline[i]
        draw.ellipse([pt[0] - 12, pt[1] - 5, pt[0] + 12, pt[1] + 5], fill=(255, 183, 77, 255), outline=(230, 81, 0, 255), width=2)
        draw.arc([pt[0] - 10, pt[1] - 3, pt[0] + 10, pt[1] + 3], start=180, end=360, fill=(255, 255, 255, 240), width=2)

    # Hex Nut Reducer at bottom of flex
    end_pt = spline[-1]
    hx, hy = int(end_pt[0]), int(end_pt[1])
    draw.polygon([(hx - 16, hy), (hx + 16, hy), (hx + 12, hy + 18), (hx - 12, hy + 18)], fill=(120, 144, 156, 255), outline=(38, 50, 56, 255), width=3)
    draw.rectangle([hx - 8, hy + 18, hx + 8, hy + 26], fill=(255, 193, 7, 255), outline=(191, 54, 12, 255), width=2)

    # Brass Sprinkler Frame Arms (Pendant)
    draw.polygon([(hx, hy + 26), (hx - 22, hy + 50), (hx + 22, hy + 50)], outline=(245, 127, 23, 255), fill=(255, 248, 225, 60), width=5)

    # Red Thermal Liquid Glass Bulb
    draw.rounded_rectangle([hx - 5, hy + 26, hx + 5, hy + 46], radius=4, fill=(229, 57, 53, 255), outline=(183, 28, 28, 255), width=2)
    # Glass shine
    draw.line([(hx - 2, hy + 28), (hx - 2, hy + 42)], fill=(255, 255, 255, 220), width=2)

    # Golden Deflector Plate (serrated teeth)
    def_y = hy + 50
    draw.polygon([(hx - 30, def_y), (hx + 30, def_y), (hx + 26, def_y + 8), (hx - 26, def_y + 8)], fill=(255, 179, 0, 255), outline=(191, 54, 12, 255), width=2)
    # Deflector teeth
    for tooth_x in range(hx - 24, hx + 25, 8):
        draw.polygon([(tooth_x, def_y + 8), (tooth_x + 4, def_y + 14), (tooth_x + 8, def_y + 8)], fill=(245, 127, 23, 255))

    # Water Spray Drops / Mist Arcs
    draw.arc([hx - 38, def_y + 6, hx + 38, def_y + 26], start=30, end=150, fill=(3, 169, 244, 220), width=4)
    draw.arc([hx - 46, def_y + 10, hx + 46, def_y + 36], start=20, end=160, fill=(3, 169, 244, 180), width=3)

    icons["ConnectSprinklerFlexPipe"] = img

    # =========================================================================
    # 3. ConnectSprinklerFlexMulti
    # =========================================================================
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Top main branch pipe
    draw_3d_cylinder_h(draw, 16, 16, 240, 58, base_rgb=(25, 118, 210), outline_rgb=(10, 45, 110))

    # 2 Sprinkler Drop Clusters (Left: x=76, Right: x=180)
    for cx in [76, 180]:
        # Tee fitting
        draw.rectangle([cx - 12, 54, cx + 12, 74], fill=(21, 101, 192, 255), outline=(10, 45, 110, 255), width=2)
        
        # S-curve
        sub_spline = []
        sign = -1 if cx < 128 else 1
        for step in range(25):
            v = step / 24.0
            x = cx + sign * 16.0 * math.sin(v * math.pi)
            y = 74.0 + v * 68.0
            sub_spline.append((x, y))

        for j in range(len(sub_spline) - 1):
            draw.line([sub_spline[j], sub_spline[j + 1]], fill=(255, 152, 0, 255), width=16)
            draw.line([sub_spline[j], sub_spline[j + 1]], fill=(255, 236, 179, 255), width=6)

        for j in range(2, len(sub_spline) - 2, 3):
            pt = sub_spline[j]
            draw.ellipse([pt[0] - 8, pt[1] - 4, pt[0] + 8, pt[1] + 4], fill=(255, 183, 77, 255), outline=(230, 81, 0, 255), width=2)

        # Bottom head
        by = 142
        draw.rectangle([cx - 8, by, cx + 8, by + 12], fill=(120, 144, 156, 255), outline=(38, 50, 56, 255), width=2)
        draw.polygon([(cx, by + 12), (cx - 18, by + 34), (cx + 18, by + 34)], outline=(245, 127, 23, 255), fill=(255, 248, 225, 40), width=4)
        draw.rounded_rectangle([cx - 4, by + 14, cx + 4, by + 30], radius=3, fill=(229, 57, 53, 255))
        draw.polygon([(cx - 24, by + 34), (cx + 24, by + 34), (cx + 20, by + 40), (cx - 20, by + 40)], fill=(255, 179, 0, 255))

    # Multi Bracket / Link arc connecting both clusters at bottom
    draw.arc([60, 170, 196, 230], start=0, end=180, fill=(76, 175, 80, 255), width=8)
    draw.polygon([(128, 238), (116, 218), (140, 218)], fill=(76, 175, 80, 255), outline=(27, 94, 32, 255), width=2)

    # Multi Badge "xN" in center
    draw.ellipse([108, 102, 148, 142], fill=(76, 175, 80, 255), outline=(255, 255, 255, 255), width=3)
    # Plus sign
    draw.line([(128, 112), (128, 132)], fill=(255, 255, 255, 255), width=5)
    draw.line([(118, 122), (138, 122)], fill=(255, 255, 255, 255), width=5)

    icons["ConnectSprinklerFlexMulti"] = img

    # =========================================================================
    # 4. CheckUndefinedPipe
    # =========================================================================
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Metallic pipe cylinder with open disconnected flange
    draw_3d_cylinder_h(draw, 24, 110, 180, 180, base_rgb=(100, 116, 139), outline_rgb=(30, 41, 59))
    # Flange ring with bolt holes
    draw.ellipse([160, 104, 196, 186], fill=(71, 85, 105, 255), outline=(15, 23, 42, 255), width=4)
    draw.ellipse([168, 114, 188, 176], fill=(15, 23, 42, 255)) # dark inner hollow
    # Bolt holes
    for by in [112, 145, 178]:
        draw.ellipse([175, by - 3, 181, by + 3], fill=(203, 213, 225, 255))

    # Glowing 3D Warning Glass Badge in upper-right
    bx1, by1, bx2, by2 = 120, 24, 236, 140
    # Soft drop shadow
    draw.ellipse([bx1 + 6, by1 + 8, bx2 + 6, by2 + 8], fill=(0, 0, 0, 90))
    # Amber/Orange gradient circle
    draw.ellipse([bx1, by1, bx2, by2], fill=(245, 158, 11, 255), outline=(180, 83, 9, 255), width=6)
    # Glass reflection arc
    draw.arc([bx1 + 10, by1 + 10, bx2 - 10, by2 - 10], start=190, end=350, fill=(255, 251, 235, 220), width=6)

    # Bold 3D White Question Mark "?"
    # Top arc
    draw.arc([150, 44, 206, 92], start=180, end=360, fill=(255, 255, 255, 255), width=14)
    draw.line([(206, 68), (178, 96)], fill=(255, 255, 255, 255), width=14)
    draw.line([(178, 96), (178, 106)], fill=(255, 255, 255, 255), width=14)
    # Dot
    draw.ellipse([171, 114, 185, 128], fill=(255, 255, 255, 255))

    icons["CheckUndefinedPipe"] = img

    # =========================================================================
    # 5. FlexDuctAvoidMep
    # =========================================================================
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Red Obstacle Beam / Pipe in center
    draw.rounded_rectangle([92, 100, 164, 172], radius=10, fill=(239, 68, 68, 255), outline=(153, 27, 27, 255), width=5)
    # Warning stripes on obstacle
    for i in range(104, 168, 16):
        draw.line([(96, i), (i - 96 + 96, 168)], fill=(254, 226, 226, 180), width=4)
    # Obstacle X
    draw.line([(108, 116), (148, 156)], fill=(255, 255, 255, 240), width=6)
    draw.line([(148, 116), (108, 156)], fill=(255, 255, 255, 240), width=6)

    # Flexible Duct gracefully arching over obstacle
    arch_pts = []
    for step in range(40):
        t = step / 39.0
        # parabolic arch
        x = 24.0 + t * 208.0
        y = 200.0 - 150.0 * math.sin(t * math.pi)
        arch_pts.append((x, y))

    # Outer corrugated duct tube
    for i in range(len(arch_pts) - 1):
        draw.line([arch_pts[i], arch_pts[i + 1]], fill=(51, 65, 85, 255), width=32)
        draw.line([arch_pts[i], arch_pts[i + 1]], fill=(148, 163, 184, 255), width=24)
        draw.line([arch_pts[i], arch_pts[i + 1]], fill=(241, 245, 249, 255), width=10)

    # Rib rings on flexible duct
    for i in range(1, len(arch_pts) - 1, 2):
        pt = arch_pts[i]
        draw.ellipse([pt[0] - 8, pt[1] - 14, pt[0] + 8, pt[1] + 14], fill=(100, 116, 139, 255), outline=(30, 41, 59, 255), width=2)
        draw.arc([pt[0] - 6, pt[1] - 12, pt[0] + 6, pt[1] + 12], start=180, end=360, fill=(255, 255, 255, 220), width=2)

    # Avoidance green motion arrow on top
    draw.arc([60, 20, 196, 80], start=190, end=350, fill=(34, 197, 94, 255), width=7)
    draw.polygon([(194, 56), (216, 56), (206, 36)], fill=(34, 197, 94, 255), outline=(20, 83, 45, 255), width=2)

    icons["FlexDuctAvoidMep"] = img

    # =========================================================================
    # 6. CheckPipeClash
    # =========================================================================
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Pipe 1 (Horizontal - Main, bottom Z)
    draw_3d_cylinder_h(draw, 20, 142, 236, 204, base_rgb=(37, 99, 235), outline_rgb=(30, 64, 175))

    # Pipe 2 (Vertical - Cross, top Z)
    vx1, vy1, vx2, vy2 = 98, 20, 158, 236
    draw.rounded_rectangle([vx1 - 2, vy1 - 2, vx2 + 2, vy2 + 2], radius=16, fill=(15, 118, 110, 255))
    # Vertical gradient shine
    for step in range(vx2 - vx1):
        x = vx1 + step
        u = step / float(vx2 - vx1)
        spec = math.pow(math.sin(u * math.pi), 3)
        r = int(13 * (1 - spec) + 204 * spec)
        g = int(148 * (1 - spec) + 251 * spec)
        b = int(136 * (1 - spec) + 241 * spec)
        draw.line([(x, vy1 + 8), (x, vy2 - 8)], fill=(r, g, b, 255), width=1)
    draw.ellipse([vx1, vy1 - 4, vx2, vy1 + 16], fill=(45, 212, 191, 255), outline=(15, 118, 110, 255), width=3)

    # Dashed insulation clearance boundary
    ins_box = [70, 92, 186, 208]
    draw.rounded_rectangle(ins_box, radius=12, fill=(255, 235, 59, 35), outline=(245, 158, 11, 220), width=4)

    # Center Clash Radar Burst & Warning Star
    cx, cy = 128, 173
    # Burst glow
    draw.ellipse([cx - 46, cy - 46, cx + 46, cy + 46], fill=(239, 68, 68, 220), outline=(255, 255, 255, 255), width=4)
    # Exclamation mark
    draw.line([(cx, cy - 26), (cx, cy + 8)], fill=(255, 255, 255, 255), width=10)
    draw.ellipse([cx - 6, cy + 18, cx + 6, cy + 30], fill=(255, 255, 255, 255))

    # Clearance Caliper Measurement Arrow
    draw.line([(40, 112), (90, 112)], fill=(245, 158, 11, 255), width=6)
    draw.polygon([(40, 112), (54, 104), (54, 120)], fill=(245, 158, 11, 255))
    draw.polygon([(90, 112), (76, 104), (76, 120)], fill=(245, 158, 11, 255))
    draw.line([(40, 98), (40, 126)], fill=(245, 158, 11, 255), width=4)
    draw.line([(90, 98), (90, 126)], fill=(245, 158, 11, 255), width=4)

    icons["CheckPipeClash"] = img

    # =========================================================================
    # 7. CopyFilter
    # =========================================================================
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 3D View Panes / Sheets behind the filter
    # Back pane (sheet)
    draw.rounded_rectangle([130, 40, 226, 140], radius=8, fill=(203, 213, 225, 240), outline=(71, 85, 105, 255), width=4)
    draw.line([(146, 68), (210, 68)], fill=(148, 163, 184, 255), width=4)
    draw.line([(146, 92), (196, 92)], fill=(148, 163, 184, 255), width=4)

    # Front pane (sheet receiving filter)
    draw.rounded_rectangle([150, 66, 246, 166], radius=8, fill=(248, 250, 252, 255), outline=(51, 65, 85, 255), width=4)
    # Filtered layer rows (with colors indicating view filter overrides)
    draw.rounded_rectangle([166, 84, 230, 98], radius=3, fill=(239, 68, 68, 220))
    draw.rounded_rectangle([166, 106, 230, 120], radius=3, fill=(59, 130, 246, 220))
    draw.rounded_rectangle([166, 128, 214, 142], radius=3, fill=(16, 185, 129, 220))

    # Translucent 3D Funnel Filter
    funnel_poly = [
        (26, 32), (150, 32), (104, 116), (104, 204), (72, 204), (72, 116)
    ]
    # Funnel outer shadow
    draw.polygon([(p[0] + 4, p[1] + 4) for p in funnel_poly], fill=(0, 0, 0, 60))
    # Funnel gradient body
    draw.polygon(funnel_poly, fill=(6, 182, 212, 240), outline=(14, 116, 144, 255), width=5)
    # Liquid surface at top
    draw.ellipse([26, 24, 150, 44], fill=(34, 211, 238, 255), outline=(14, 116, 144, 255), width=4)
    # Glass specular shine
    draw.polygon([(36, 40), (140, 40), (100, 104), (80, 104)], fill=(207, 250, 254, 150))
    draw.line([(80, 120), (80, 196)], fill=(255, 255, 255, 200), width=4)

    # Green Copy Duplicate Arrow & Plus symbol
    draw.arc([100, 150, 216, 236], start=30, end=170, fill=(34, 197, 94, 255), width=9)
    draw.polygon([(194, 160), (220, 166), (208, 140)], fill=(34, 197, 94, 255), outline=(20, 83, 45, 255), width=2)
    # Plus Badge
    draw.ellipse([196, 176, 244, 224], fill=(34, 197, 94, 255), outline=(255, 255, 255, 255), width=3)
    draw.line([(220, 188), (220, 212)], fill=(255, 255, 255, 255), width=5)
    draw.line([(208, 200), (232, 200)], fill=(255, 255, 255, 255), width=5)

    icons["CopyFilter"] = img

    return icons

def main():
    icons = create_3d_icons()
    dest_dirs = [
        r"C:\Users\aulac\AppData\Roaming\BIN TOOL\BIN.bundle\Contents\net48\Resources",
        r"C:\Users\aulac\AppData\Roaming\BIN TOOL\BIN.bundle\Contents\net8.0-windows\Resources",
        r"C:\Users\aulac\AppData\Roaming\BIN TOOL\BIN.bundle\Contents\Resources",
        r"D:\Tool Revit\src\net48\Resources",
        r"D:\Tool Revit\src\net48\bin\Release\net48\Resources",
        r"D:\Tool Revit\src\net8.0-windows\Resources",
        r"D:\Tool Revit\src\net8.0-windows\bin\Release\net8.0-windows\Resources",
        r"D:\Tool Revit\release\BIM-Tool-Portable-20261001-Current\BIN.bundle\Contents\net48\Resources",
        r"D:\Tool Revit\release\BIM-Tool-Portable-20261001-Current\BIN.bundle\Contents\net8.0-windows\Resources",
    ]

    for d in dest_dirs:
        os.makedirs(d, exist_ok=True)

    for name, img in icons.items():
        img_64 = img.resize((64, 64), Image.Resampling.LANCZOS)
        img_32 = img.resize((32, 32), Image.Resampling.LANCZOS)
        img_16 = img.resize((16, 16), Image.Resampling.LANCZOS)

        for d in dest_dirs:
            p_32 = os.path.join(d, f"{name}.png")
            p_16 = os.path.join(d, f"{name}_small.png")
            p_64 = os.path.join(d, f"{name}_tooltip.png")
            
            img_32.save(p_32, "PNG")
            img_16.save(p_16, "PNG")
            img_64.save(p_64, "PNG")
            print(f"Rendered: {name} -> {d}")

if __name__ == "__main__":
    main()
