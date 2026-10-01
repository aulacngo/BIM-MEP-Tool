import os
import shutil
import re
from datetime import datetime

xml_path = os.path.expandvars(r'%APPDATA%\Autodesk\Revit\Autodesk Revit 2023\KeyboardShortcuts.xml')

if not os.path.exists(xml_path):
    print(f"File not found: {xml_path}")
    exit(1)

backup_path = xml_path + f".backup_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
shutil.copy2(xml_path, backup_path)
print(f"1. Backup saved to: {backup_path}")

with open(xml_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Replace all occurrences of BIN with BIM in ribbon references
replacements = [
    ('BIN - MEP', 'BIM - MEP'),
    ('BIN - DOCS', 'BIM - DOCS'),
    ('BIN - MICRO', 'BIM - MICRO'),
    ('BIN - SPRINKLER', 'BIM - SPRINKLER'),
    ('BIN - DRAINAGE', 'BIM - DRAINAGE'),
    ('BIN - SUPPORT', 'BIM - SUPPORT'),
    ('BIN - CHECK', 'BIM - CHECK'),
    ('BIN - BOQ &amp; EXCEL', 'BIM - BOQ &amp; EXCEL'),
    ('BIN - SHEET &amp; VIEW', 'BIM - SHEET &amp; VIEW'),
    ('BIN - 2D &amp; ANNO', 'BIM - 2D ANNOTATION'),
    ('BIN - TRANSFER &amp; LINK', 'BIM - TRANSFER &amp; LINK'),
]

changes = 0
for old, new in replacements:
    count = content.count(old)
    if count > 0:
        content = content.replace(old, new)
        changes += count
        print(f"   Replaced '{old}' -> '{new}' ({count} times)")

with open(xml_path, 'w', encoding='utf-8') as f:
    f.write(content)

print(f"\n2. Successfully migrated {changes} items in KeyboardShortcuts.xml!")

# Also generate a standalone portable XML file for company machine
portable_xml = os.path.join(os.path.dirname(__file__), "..", "release", "BIM_KeyboardShortcuts_Import.xml")
os.makedirs(os.path.dirname(portable_xml), exist_ok=True)
shutil.copy2(xml_path, portable_xml)
print(f"3. Exported clean shortcut file for company machine: {portable_xml}")
