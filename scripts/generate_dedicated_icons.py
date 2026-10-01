import os
import math
from PIL import Image, ImageDraw, ImageFont

def create_icons():
    SCALE = 4  # Draw at 128x128, downscale to 32x32 and 16x16
    W, H = 32 * SCALE, 32 * SCALE

    icons = {}

    # -------------------------------------------------------------
    # 1. AlignPipeElevation
    # -------------------------------------------------------------
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Datum reference line (dashed yellow/orange)
    for x in range(12, W - 12, 16):
        draw.line([(x, H // 2), (x + 8, H // 2)], fill=(255, 179, 0, 240), width=4)

    # Pipe 1 (Left - lower than datum, moving UP)
    # Left pipe cylinder
    draw.rounded_rectangle([12, 72, 52, 104], radius=6, fill=(33, 150, 243, 255), outline=(13, 71, 161, 255), width=4)
    draw.rectangle([16, 76, 48, 86], fill=(144, 202, 249, 220)) # shine
    # Green Up Arrow
    draw.polygon([(32, 44), (20, 64), (28, 64), (28, 70), (36, 70), (36, 64), (44, 64)], fill=(76, 175, 80, 255), outline=(27, 94, 32, 255))

    # Pipe 2 (Right - higher than datum, moving DOWN)
    # Right pipe cylinder
    draw.rounded_rectangle([76, 24, 116, 56], radius=6, fill=(33, 150, 243, 255), outline=(13, 71, 161, 255), width=4)
    draw.rectangle([80, 28, 112, 38], fill=(144, 202, 249, 220)) # shine
    # Green Down Arrow
    draw.polygon([(96, 84), (84, 64), (92, 64), (92, 58), (100, 58), (100, 64), (108, 64)], fill=(76, 175, 80, 255), outline=(27, 94, 32, 255))

    icons["AlignPipeElevation"] = img

    # -------------------------------------------------------------
    # 2. ConnectSprinklerFlexPipe
    # -------------------------------------------------------------
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Top main pipe (horizontal)
    draw.rounded_rectangle([16, 12, W - 16, 36], radius=6, fill=(30, 136, 229, 255), outline=(13, 71, 161, 255), width=4)
    draw.rectangle([20, 16, W - 20, 24], fill=(144, 202, 249, 230))

    # Fitting nipple at center of top pipe
    draw.rectangle([56, 36, 72, 48], fill=(21, 101, 192, 255), outline=(13, 71, 161, 255), width=3)

    # Flexible hose (S-curve corrugated hose)
    # Draw ribbed hose segments
    points = [
        (64, 48), (62, 56), (54, 64), (50, 72), (54, 80), (62, 88), (64, 96)
    ]
    for i in range(len(points) - 1):
        p1 = points[i]
        p2 = points[i+1]
        draw.line([p1, p2], fill=(255, 152, 0, 255), width=10)
        draw.line([p1, p2], fill=(255, 224, 130, 255), width=4)

    # Reducer / nipple at bottom of flex
    draw.rectangle([58, 96, 70, 104], fill=(120, 144, 156, 255), outline=(55, 71, 79, 255), width=3)

    # Sprinkler head
    # Frame arms
    draw.polygon([(64, 104), (52, 116), (76, 116)], outline=(245, 127, 23, 255), fill=None, width=4)
    # Red glass bulb
    draw.ellipse([61, 106, 67, 114], fill=(229, 57, 53, 255))
    # Deflector plate
    draw.polygon([(46, 118), (82, 118), (80, 122), (48, 122)], fill=(255, 179, 0, 255), outline=(191, 54, 12, 255), width=2)
    # Water spray droplets (cyan)
    draw.arc([42, 116, 86, 128], start=20, end=160, fill=(3, 169, 244, 255), width=3)

    icons["ConnectSprinklerFlexPipe"] = img

    # -------------------------------------------------------------
    # 3. ConnectSprinklerFlexMulti
    # -------------------------------------------------------------
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Top pipe
    draw.rounded_rectangle([10, 10, W - 10, 32], radius=5, fill=(30, 136, 229, 255), outline=(13, 71, 161, 255), width=4)
    draw.rectangle([14, 14, W - 14, 20], fill=(144, 202, 249, 230))

    # Drop 1 (left)
    draw.rectangle([32, 32, 44, 42], fill=(21, 101, 192, 255), outline=(13, 71, 161, 255), width=2)
    # Curve 1
    pts1 = [(38, 42), (32, 54), (32, 68), (38, 80), (38, 88)]
    for i in range(len(pts1) - 1):
        draw.line([pts1[i], pts1[i+1]], fill=(255, 152, 0, 255), width=8)
        draw.line([pts1[i], pts1[i+1]], fill=(255, 224, 130, 255), width=3)
    # Head 1
    draw.ellipse([35, 90, 41, 98], fill=(229, 57, 53, 255))
    draw.polygon([(26, 100), (50, 100), (48, 104), (28, 104)], fill=(255, 179, 0, 255))

    # Drop 2 (right)
    draw.rectangle([84, 32, 96, 42], fill=(21, 101, 192, 255), outline=(13, 71, 161, 255), width=2)
    # Curve 2
    pts2 = [(90, 42), (96, 54), (96, 68), (90, 80), (90, 88)]
    for i in range(len(pts2) - 1):
        draw.line([pts2[i], pts2[i+1]], fill=(255, 152, 0, 255), width=8)
        draw.line([pts2[i], pts2[i+1]], fill=(255, 224, 130, 255), width=3)
    # Head 2
    draw.ellipse([87, 90, 93, 98], fill=(229, 57, 53, 255))
    draw.polygon([(78, 100), (102, 100), (100, 104), (80, 104)], fill=(255, 179, 0, 255))

    # Multi badge "+2" or link bracket
    draw.arc([36, 104, 92, 126], start=0, end=180, fill=(76, 175, 80, 255), width=4)
    draw.polygon([(64, 124), (60, 114), (68, 114)], fill=(76, 175, 80, 255))

    icons["ConnectSprinklerFlexMulti"] = img

    # -------------------------------------------------------------
    # 4. CheckUndefinedPipe
    # -------------------------------------------------------------
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Pipe body
    draw.rounded_rectangle([14, 44, 94, 84], radius=8, fill=(100, 116, 139, 255), outline=(51, 65, 85, 255), width=5)
    draw.rectangle([20, 52, 88, 62], fill=(203, 213, 225, 220)) # shine
    # Flange ring
    draw.rounded_rectangle([88, 38, 102, 90], radius=4, fill=(71, 85, 105, 255), outline=(30, 41, 59, 255), width=4)

    # Orange / Amber Warning Badge
    draw.ellipse([64, 16, 120, 72], fill=(245, 158, 11, 255), outline=(180, 83, 9, 255), width=4)
    # Question Mark "?" in white
    # Top arc
    draw.arc([80, 26, 104, 46], start=180, end=360, fill=(255, 255, 255, 255), width=6)
    draw.line([(104, 36), (92, 48)], fill=(255, 255, 255, 255), width=6)
    draw.line([(92, 48), (92, 54)], fill=(255, 255, 255, 255), width=6)
    # Dot
    draw.ellipse([89, 58, 95, 64], fill=(255, 255, 255, 255))

    icons["CheckUndefinedPipe"] = img

    # -------------------------------------------------------------
    # 5. FlexDuctAvoidMep
    # -------------------------------------------------------------
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Obstacle in center (Red MEP Box / clash element)
    draw.rounded_rectangle([44, 44, 84, 84], radius=6, fill=(239, 68, 68, 255), outline=(185, 28, 28, 255), width=4)
    # Cross on obstacle
    draw.line([(52, 52), (76, 76)], fill=(255, 255, 255, 220), width=4)
    draw.line([(76, 52), (52, 76)], fill=(255, 255, 255, 220), width=4)

    # Flexible duct looping OVER the obstacle
    # Draw outer corrugated flex duct
    flex_pts = [
        (12, 96), (24, 76), (42, 28), (64, 20), (86, 28), (104, 76), (116, 96)
    ]
    for i in range(len(flex_pts) - 1):
        p1 = flex_pts[i]
        p2 = flex_pts[i+1]
        draw.line([p1, p2], fill=(148, 163, 184, 255), width=16)
        draw.line([p1, p2], fill=(241, 245, 249, 255), width=8)

    # Ribs on flex duct
    for pt in flex_pts:
        draw.ellipse([pt[0] - 6, pt[1] - 6, pt[0] + 6, pt[1] + 6], fill=(71, 85, 105, 255))

    # Avoidance green motion arrow
    draw.arc([30, 10, 98, 48], start=200, end=340, fill=(34, 197, 94, 255), width=4)
    draw.polygon([(96, 26), (106, 26), (102, 16)], fill=(34, 197, 94, 255))

    icons["FlexDuctAvoidMep"] = img

    # -------------------------------------------------------------
    # 6. CheckPipeClash
    # -------------------------------------------------------------
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Horizontal Pipe (Bottom-Z)
    draw.rounded_rectangle([12, 72, 116, 102], radius=6, fill=(37, 99, 235, 255), outline=(30, 64, 175, 255), width=4)
    draw.rectangle([16, 76, 112, 84], fill=(147, 197, 253, 220))

    # Vertical / Angled Crossing Pipe (Top-Z)
    draw.rounded_rectangle([48, 12, 80, 116], radius=6, fill=(13, 148, 136, 255), outline=(15, 118, 110, 255), width=4)
    draw.rectangle([54, 16, 62, 112], fill=(153, 246, 228, 220))

    # Clash & Clearance symbol at intersection (Radar / Caliper)
    # Flashing clash spark
    cx, cy = 64, 87
    draw.ellipse([cx - 24, cy - 24, cx + 24, cy + 24], fill=(239, 68, 68, 220), outline=(255, 255, 255, 255), width=3)
    # Warning exclamation mark
    draw.line([(cx, cy - 14), (cx, cy + 3)], fill=(255, 255, 255, 255), width=5)
    draw.ellipse([cx - 3, cy + 8, cx + 3, cy + 14], fill=(255, 255, 255, 255))

    # Clearance measurement arrows
    draw.line([(24, 56), (44, 56)], fill=(245, 158, 11, 255), width=4)
    draw.polygon([(24, 56), (30, 51), (30, 61)], fill=(245, 158, 11, 255))
    draw.polygon([(44, 56), (38, 51), (38, 61)], fill=(245, 158, 11, 255))

    icons["CheckPipeClash"] = img

    # -------------------------------------------------------------
    # 7. CopyFilter
    # -------------------------------------------------------------
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Funnel Filter Body (Cyan / Blue Gradient)
    # Top rim
    draw.polygon([(16, 18), (84, 18), (58, 58), (58, 96), (42, 96), (42, 58)], fill=(6, 182, 212, 255), outline=(14, 116, 144, 255), width=4)
    # Liquid / filter content
    draw.polygon([(24, 24), (76, 24), (54, 54), (46, 54)], fill=(165, 243, 252, 200))
    # Spout bottom
    draw.line([(50, 96), (50, 112)], fill=(14, 116, 144, 255), width=5)

    # Copy Duplicate icon at bottom-right
    # Back document / filter
    draw.rounded_rectangle([72, 56, 112, 96], radius=4, fill=(226, 232, 240, 255), outline=(100, 116, 139, 255), width=3)
    # Front document
    draw.rounded_rectangle([82, 66, 122, 106], radius=4, fill=(255, 255, 255, 255), outline=(71, 85, 105, 255), width=3)
    # Green Plus "+" symbol
    draw.line([(102, 76), (102, 96)], fill=(34, 197, 94, 255), width=4)
    draw.line([(92, 86), (112, 86)], fill=(34, 197, 94, 255), width=4)

    icons["CopyFilter"] = img

    return icons

def main():
    icons = create_icons()
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
        img_32 = img.resize((32, 32), Image.Resampling.LANCZOS)
        img_16 = img.resize((16, 16), Image.Resampling.LANCZOS)

        for d in dest_dirs:
            p_32 = os.path.join(d, f"{name}.png")
            p_16 = os.path.join(d, f"{name}_small.png")
            img_32.save(p_32, "PNG")
            img_16.save(p_16, "PNG")
            print(f"Saved: {p_32} and {p_16}")

if __name__ == "__main__":
    main()
