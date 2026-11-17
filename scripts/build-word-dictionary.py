"""Build a seekable, deterministic English/Chinese resource; Python is build-only."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import struct

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument("csv", type=Path)
args = parser.parse_args()
lock = json.loads((root / "assets/word-dictionary-source.json").read_text(encoding="utf-8-sig"))
source_hash = hashlib.file_digest(args.csv.open("rb"), "sha256").hexdigest()
if source_hash != lock["csvSha256"]:
    raise SystemExit("Dictionary source SHA-256 mismatch")
words = {}
eligible = re.compile(r"[a-z][a-z0-9 _+./'-]{0,127}\Z")
han = re.compile(r"[\u3400-\u9fff\uf900-\ufaff]")
csv.field_size_limit(8 * 1024 * 1024)
with args.csv.open("r", encoding="utf-8-sig", newline="") as stream:
    for row in csv.DictReader(stream):
        key = row["word"].strip().lower()
        text = row["translation"].replace("\\n", "\n").replace("\x00", "").strip()
        if not eligible.fullmatch(key) or not han.search(text):
            continue
        if key in words:
            if text not in words[key]:
                words[key] += "\n" + text
        else:
            words[key] = text
items = sorted(words.items())
output = root / "assets/word-dictionary.dat"
offsets = []
with output.open("wb") as stream:
    stream.write(b"YIKEDIC1" + struct.pack("<I", len(items)))
    stream.write(b"\0" * (4 * (len(items) + 1)))
    for key, value in items:
        offsets.append(stream.tell())
        encoded_key = key.encode("ascii")
        encoded_value = value[:2000].encode("utf-8")
        if len(encoded_key) > 128 or len(encoded_value) > 8192:
            raise SystemExit("Unexpected dictionary record length")
        stream.write(struct.pack("<H", len(encoded_key)))
        stream.write(encoded_key)
        stream.write(struct.pack("<H", len(encoded_value)))
        stream.write(encoded_value)
    offsets.append(stream.tell())
    if offsets[-1] >= 2**32:
        raise SystemExit("Dictionary exceeds the 32-bit offset format")
    stream.seek(12)
    for offset in offsets:
        stream.write(struct.pack("<I", offset))
manifest = {
    "format": "YIKEDIC1",
    "source": lock["repository"],
    "commit": lock["commit"],
    "sourceCsvSha256": source_hash,
    "entries": len(items),
    "bytes": output.stat().st_size,
    "sha256": hashlib.file_digest(output.open("rb"), "sha256").hexdigest(),
    "selection": "ASCII lexical keys, Chinese translation present, exact lookup; 2000-character result bound",
    "license": "MIT; assets/local-licenses/ECDICT-MIT.txt",
}
(root / "assets/word-dictionary.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(manifest, ensure_ascii=False))
