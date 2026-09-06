"""Build exact item/body bundles from client itemmodel XML, never filename IDs.

prepare selects a reproducible candidate library and writes the native batch plan.
catalog joins completed exports without treating conversion as visual acceptance.
All paths in the resulting contracts are relative to the supplied resource root.
"""
import argparse
import collections
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET


SLOTS = {"CP", "CL", "PA", "GL", "SH", "MT", "HR", "FA", "EA", "OH", "RH", "LH"}


def asset_path(name):
    name = name.replace("\\", "/").lower()
    prefix = "data/resource/model/item/"
    return name[len(prefix):] if name.startswith(prefix) else None


def read_items(xml_root, index):
    indexed = {path.lower(): path for path in index}
    urns = collections.defaultdict(list)
    for path in index:
        urns[Path(path).stem.lower()].append(path)
    for xml in sorted((xml_root / "itemmodel").glob("*.xml")):
        for item in ET.parse(xml).getroot().findall("ItemModel"):
            for gender, variant in [("0", "male"), ("1", "female")]:
                parts = []
                for slot in item.findall("slots/slot"):
                    for asset in slot.findall("asset"):
                        if asset.get("gender", gender) != gender:
                            continue
                        path = asset_path(asset.get("name", ""))
                        if asset.get('name', '').lower().startswith('urn:'):
                            matches = urns[asset.get('name')[4:].lower()]
                            if len(matches) == 1:
                                path = matches[0].lower()
                        # Hair/face itemmodel entries omit gender even though the
                        # source model is sex-specific. DB gender is checked again by the API.
                        if slot.get("name") in {"HR", "FA"} and path and re.search(r"_" + ("f" if gender == "0" else "m") + r"[_.]", path):
                            continue
                        parts.append({"slot": slot.get("name"), "source": indexed.get(path),
                                      "declared": asset.get("name"), "selfNode": asset.get("selfnode"),
                                      "targetNode": asset.get("targetnode"), "replace": asset.get("replace") == "1"})
                if not parts or not all(p["slot"] in SLOTS for p in parts):
                    continue
                customization = item.find("customize")
                yield {"itemId": int(item.get("id")), "bodyVariant": variant,
                       "sourceName": item.get("desc", "").strip(), "slots": sorted({p["slot"] for p in parts}),
                       "parts": parts, "itemModel": xml.relative_to(xml_root.parent.parent).as_posix(),
                       "customize": dict(customization.attrib) if customization is not None else {},
                       "cutting": [m.get("name") for m in item.findall("cutting/mesh") if m.get("gender", gender) == gender]}


def prepare(args):
    root = Path(args.resources)
    index = json.loads(Path(args.index).read_text(encoding="utf-8-sig"))
    items = list(read_items(root / "SimulatorSources/Xml", index))
    selected, counts, signatures = [], collections.Counter(), set()
    explicit = {11400350, 11400158, 11300212, 10200230, 10200224, 10200009, 10200013, 11200028, 11800291, 13100068}
    for item in sorted(items, key=lambda i: (i["itemId"] not in explicit, i["itemId"])):
        if any(p["source"] is None for p in item["parts"]):
            continue
        key = (item["bodyVariant"], tuple(item["slots"]))
        signature = (item["bodyVariant"], tuple((p["source"], p["selfNode"]) for p in item["parts"]))
        if signature in signatures or (counts[key] >= args.per_slot and item["itemId"] not in explicit):
            continue
        signatures.add(signature)
        counts[key] += 1
        selected.append(item)
    models = json.loads(Path("NifToGltf/Diagnostics/acceptance-plan.json").read_text())["models"][:2]
    for item in selected:
        for number, part in enumerate(item["parts"]):
            identity = f'{item["itemId"]}-{item["bodyVariant"]}-{number}'
            part["assetId"] = identity
            models.append({"id": identity, "input": "SimulatorSources/Item/" + part["source"],
                           "output": f'{item["bodyVariant"]}/{item["itemId"]}/{number}.gltf',
                           "bodyVariant": item["bodyVariant"], "slot": part["slot"],
                           "skeleton": f'Models/Character/{item["bodyVariant"]}/{item["bodyVariant"][0]}_body.nif',
                           "itemModel": item["itemModel"], "itemId": str(item["itemId"])})
    dest = Path(args.output)
    dest.mkdir(parents=True, exist_ok=True)
    (dest / "plan.json").write_text(json.dumps({"version": 1, "models": models}, indent=2))
    (dest / "candidates.json").write_text(json.dumps(selected, indent=2, ensure_ascii=False), encoding="utf-8")
    names = sorted({Path(p["source"]).stem for i in selected for p in i["parts"]})
    # Include sibling DDS maps sharing each exact source stem, not other items.
    (dest / "extract-regex.txt").write_text(r"(^|/)(" + "|".join(re.escape(n) for n in names) + r")([_.][^/]*)?\.(nif|dds)$")
    print(json.dumps({"bundles": len(selected), "parts": len(models) - 2, "coverage": {str(k): v for k, v in counts.items()}}, indent=2))


def catalog(args):
    dest = Path(args.output)
    candidates = json.loads((dest / "candidates.json").read_text(encoding="utf-8"))
    release = Path(args.release)
    manifest = json.loads((release / "native-manifest.json").read_text())
    assets = {a["id"]: a for a in manifest["assets"]}
    report = json.loads((release / "batch-report.json").read_text())
    errors = {e["input"]: e["error"] for e in report["failed"] + report["missing"]}
    for item in candidates:
        item["availability"] = "preview" if all(p["assetId"] in assets for p in item["parts"]) else "unavailable"
        item["reason"] = "Appearance has not been verified" if item["availability"] == "preview" else "This item is not supported by the current model library"
        item["failures"] = [errors.get("SimulatorSources/Item/" + p["source"]) for p in item["parts"] if p["assetId"] not in assets]
    document = {"version": 1, "nativeManifestVersion": 1, "items": candidates}
    (release / "simulator-catalog.json").write_text(json.dumps(document, ensure_ascii=False, indent=2), encoding="utf-8")
    print(collections.Counter(i["availability"] for i in candidates))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["prepare", "catalog"])
    parser.add_argument("--resources", default="Maple2Storage/Resources")
    parser.add_argument("--index", default="NifToGltf/obj/research/simulator-item-index.json")
    parser.add_argument("--output", default="NifToGltf/obj/simulator")
    parser.add_argument("--release")
    parser.add_argument("--per-slot", type=int, default=4)
    arguments = parser.parse_args()
    globals()[arguments.mode](arguments)
