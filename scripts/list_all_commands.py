import re

with open(r'src/net8.0-windows/BIN/Panel.cs', 'r', encoding='utf-8') as f:
    content = f.read()

pattern = r'CreatePushData\(\s*"([^"]+)",\s*"([^"]+)",\s*"([^"]+)",\s*"([^"]+)",\s*"([^"]+)"\s*\)'

matches = re.findall(pattern, content)
print(f"Total commands found: {len(matches)}")

tab1_panels = ["BIM - MICRO", "BIM - MEP", "BIM - SPRINKLER", "BIM - DRAINAGE", "BIM - SUPPORT", "BIM - CHECK"]
tab2_panels = ["BIM - BOQ & EXCEL", "BIM - SHEET & VIEW", "BIM - 2D ANNOTATION", "BIM - TRANSFER & LINK"]

for tag, text, cmd, img, tip in matches:
    name_in_ks = text.replace(r'\n', ' ')
    print(f"{name_in_ks} | {tag} | {tip}")
