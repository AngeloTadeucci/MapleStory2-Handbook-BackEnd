"""Audit a saved outfit snapshot against local XML and an immutable release.

Reads only local inputs. The output records missing inventory aliases, including
gear replaced by outfit slots. It never generates models or edits release files.
"""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import zlib


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--snapshot", type=Path, required=True)
    parser.add_argument("--xml", type=Path, required=True)
    parser.add_argument("--names", type=Path, required=True)
    parser.add_argument("--release", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    rows = [json.loads(line) for line in zlib.decompress(
        base64.b64decode(args.snapshot.read_text())).decode().splitlines()]
    catalog = read_json(args.release / "simulator-catalog.json")["items"]
    manifest = read_json(args.release / "native-manifest.json")["assets"]
    indexed = {(i["itemId"], i["bodyVariant"]): i for i in catalog}
    names = {int(e.get("id")): e.get("name") for e in ET.parse(args.names).getroot()}
    definitions = {}
    models = {}
    sources = {}
    # Search the whole supplied trees, not just the expected numeric prefix file.
    for folder, index in [("itemdata", definitions), ("itemmodel", models)]:
        for path in sorted((args.xml / folder).rglob("*.xml")):
            for element in ET.parse(path).getroot():
                if element.get("id"):
                    index.setdefault(int(element.get("id")), []).append((path, element))
    characters = [r for r in rows if r["kind"] == "character"]
    missing = {}
    for character in characters:
        body = "male" if character["gender"] == 0 else "female"
        records = [r for r in rows if r["kind"] == "item" and r["name"] == character["name"]
                   and r["group"] in (1, 2)]
        selected = {r["slot"]: r for r in sorted(records, key=lambda r: r["group"])}
        for row in records:
            key = (row["itemId"], body)
            if key in indexed:
                continue
            entry = missing.setdefault(key, {"itemId": key[0], "bodyVariant": body,
                                            "name": names.get(key[0]), "uses": []})
            entry["uses"].append({"character": character["name"], "group": row["group"],
                                  "slot": row["slot"], "selectedBySlot": selected[row["slot"]] is row})
    for (item_id, body), entry in sorted(missing.items()):
        found = definitions.get(item_id, [])
        entry["definitions"] = []
        presets = set()
        for path, element in found:
            sources[str(path)] = sha(path)
            entry["definitions"].append({"path": str(path), "xml": ET.tostring(element, encoding="unicode").strip()})
            presets.update(int(t.get("itemPreset")) for t in element.findall(".//tool") if t.get("itemPreset"))
        entry["presetIds"] = sorted(presets)
        if len(presets) != 1:
            entry["status"] = "missing-local-definition" if not found else "ambiguous-or-missing-preset"
            continue
        preset_id = next(iter(presets))
        entry["models"] = []
        for path, element in models.get(preset_id, []):
            sources[str(path)] = sha(path)
            entry["models"].append({"path": str(path), "xml": ET.tostring(element, encoding="unicode").strip()})
        preset = indexed.get((preset_id, body))
        entry["preset"] = preset
        if not preset:
            entry["status"] = "missing-release-preset"
            continue
        entry["bundles"] = []
        for part in preset["parts"]:
            matches = [a for a in manifest if a["id"] == part["assetId"]
                       and a.get("bodyVariant") == body and a.get("slot") == part["slot"]]
            if len(matches) != 1:
                raise ValueError(f"Missing/ambiguous manifest part: {part}")
            asset = matches[0]
            path = args.release / asset["uri"]
            gltf = read_json(path)
            dependencies = [path.parent / value["uri"] for kind in ("buffers", "images")
                            for value in gltf.get(kind, []) if "uri" in value
                            and not value["uri"].startswith("data:")]
            if not gltf.get("meshes") or not all(p.is_file() for p in dependencies):
                raise ValueError(f"Incomplete bundle: {path}")
            entry["bundles"].append({"manifest": asset, "sha256": sha(path),
                                     "dependencies": {str(p.relative_to(args.release)): sha(p) for p in dependencies}})
        entry["status"] = "preset-unavailable" if preset["availability"] == "unavailable" else "bundle-present"
    result = {"snapshotSha256": sha(args.snapshot), "characters": [c["name"] for c in characters],
              "scope": "All group 1/2 item-body pairs absent from the packaged catalog; selectedBySlot precedes full-outfit and transparency rules.",
              "catalogSha256": sha(args.release / "simulator-catalog.json"),
              "manifestSha256": sha(args.release / "native-manifest.json"),
              "namesSha256": sha(args.names), "sources": sources,
              "missing": [entry for _, entry in sorted(missing.items())]}
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    for entry in result["missing"]:
        print(entry["itemId"], entry["bodyVariant"], entry["presetIds"], entry["status"])


if __name__ == "__main__":
    main()
