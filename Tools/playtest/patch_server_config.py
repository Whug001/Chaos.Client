import pathlib
import re
import sys

bin_dir = pathlib.Path(sys.argv[1])
staging = sys.argv[2].rstrip("\\") + "\\"
base = int(sys.argv[3])
escaped = staging.replace("\\", "\\\\")

for path in bin_dir.glob("appsettings*.json"):
    text = path.read_text(encoding="utf-8-sig")
    text = re.sub(r'("StagingDirectory"\s*:\s*")[^"]*(")', lambda m: m.group(1) + escaped + m.group(2), text)
    for old, new in ((4200, base), (4201, base + 1), (4202, base + 2)):
        text = re.sub(rf'(?<!\d){old}(?!\d)', str(new), text)
    path.write_text(text, encoding="utf-8")
    print(f"patched {path.name}")
