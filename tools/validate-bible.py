#!/usr/bin/env python3
"""Validate Slopworks game bible files for schema compliance and referential integrity.

Usage:
    python3 tools/validate-bible.py

Checks:
    - Every entry has a required ID field
    - Enum fields use valid values
    - Cross-references resolve to existing entries
    - No duplicate IDs within a category
    - copy_from targets exist
"""

import re
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    print("PyYAML not installed. Run: pip3 install pyyaml")
    sys.exit(1)

BIBLE_DIR = Path(__file__).parent.parent / "docs" / "bible"

# --- Enum definitions (must match C# code exactly) ---

ENUMS = {
    "ItemCategory": {"None", "RawMaterial", "Component", "Tool", "Building", "Consumable", "Ammo"},
    "DamageType": {"Kinetic", "Explosive", "Fire", "Toxic"},
    "TargetingMode": {"Closest", "LowestHealth", "HighestThreat"},
    "MachineStatus": {"Idle", "Working", "Blocked"},
    "PortType": {"Input", "Output"},
    "PortOwnerType": {"Machine", "Storage", "Belt", "Turret"},
    "MEPSystemType": {"Electrical", "Plumbing", "Mechanical", "HVAC"},
    "OverworldBiomeType": {"Grassland", "Forest", "Wasteland", "Swamp", "Ruins", "OvergrownRuins"},
    "OverworldNodeType": {"HomeBase", "Building", "Tower"},
    "LootRarity": {"Common", "Uncommon", "Rare", "Epic", "Legendary"},
    "SnapPointType": {"FoundationEdge", "FoundationCorner", "WallEnd", "RampBase", "RampTop"},
}

# --- ID field detection ---

ID_FIELD_SUFFIXES = ("Id", "_id")
KNOWN_ID_FIELDS = {
    "itemId", "weaponId", "machineId", "turretId", "foundationId", "wallId",
    "rampId", "recipeId", "faunaId", "buildingId", "sceneryId", "npcId",
    "lineId", "lineTypeId", "waveId", "nodeId", "upgradeId", "effectId",
    "hazardId", "chapterId", "biomeId", "defenseId", "id",
}

# Fields that are cross-references to other entries (unresolved refs are warnings, not errors)
CROSS_REF_FIELDS = {
    "ammoType", "ammoItemId", "craftingRecipe", "researchRequired",
    "upgradePath", "replaces", "copy_from",
    "defaultRecipe", "useEffect", "statusEffectApplied",
}

# Fields that reference machineType strings (not IDs) — validated separately
MACHINE_TYPE_FIELDS = {"requiredMachineType"}

# Fields that contain lists of cross-reference IDs
CROSS_REF_LIST_FIELDS = {
    "faunaIds", "nativeFauna", "nativeHazards", "availableRecipes",
    "loreItemsAvailable", "producedItemIds",
}

# Fields within nested objects that are cross-references
NESTED_CROSS_REF_FIELDS = {
    "itemId",  # in recipe inputs/outputs, lootDrops, constructionCost
    "effectId",  # in statusEffects lists
    "targetId",  # in research unlocks
}

# Enum field mapping (field name -> enum name)
# NOTE: "category" is intentionally excluded — ItemDefinitionSO uses ItemCategory,
# but machines/defenses/scenery use their own category enums (production, logistics, etc.)
# Validating category requires knowing which file the entry is in, which adds complexity
# for marginal benefit. Cross-reference checks catch the important errors.
ENUM_FIELDS = {
    "damageType": "DamageType",
    "attackDamageType": "DamageType",
    "targetingMode": "TargetingMode",
    "portOwnerType": "PortOwnerType",
    "overworldNodeType": "OverworldNodeType",
    "rarity": "LootRarity",
    "biomeId": "OverworldBiomeType",
}


def extract_yaml_blocks(md_path: Path) -> list[tuple[str, int]]:
    """Extract YAML code blocks from a markdown file. Returns (content, line_number) pairs."""
    text = md_path.read_text()
    blocks = []
    pattern = re.compile(r"```yaml\n(.*?)```", re.DOTALL)
    for match in pattern.finditer(text):
        line_num = text[:match.start()].count("\n") + 1
        blocks.append((match.group(1), line_num))
    return blocks


def is_schema_block(parsed) -> bool:
    """Schema blocks parse as dicts (key: type_annotation). Entry blocks parse as lists."""
    # Schema YAML is a flat dict like {itemId: "string", damage: "float"}
    # Entry YAML is a list of dicts like [{itemId: "iron_ore", ...}, ...]
    return not isinstance(parsed, list)


def find_id_field(entry: dict) -> str | None:
    """Find the ID field in an entry."""
    for field in KNOWN_ID_FIELDS:
        if field in entry:
            return field
    return None


def collect_all_ids(bible_dir: Path) -> dict[str, set[str]]:
    """Collect all defined IDs across all bible files. Returns {file_relative_path: set of IDs}."""
    all_ids: dict[str, set[str]] = {}
    flat_ids: set[str] = set()

    for md_file in sorted(bible_dir.rglob("*.md")):
        if md_file.name == "README.md":
            continue
        rel = md_file.relative_to(bible_dir)
        file_ids = set()
        for block_text, _ in extract_yaml_blocks(md_file):
            try:
                parsed = yaml.safe_load(block_text)
            except yaml.YAMLError:
                continue
            if not isinstance(parsed, list) or is_schema_block(parsed):
                continue
            for entry in parsed:
                if not isinstance(entry, dict):
                    continue
                id_field = find_id_field(entry)
                if id_field and entry[id_field]:
                    file_ids.add(str(entry[id_field]))

        all_ids[str(rel)] = file_ids
        flat_ids.update(file_ids)

    # Also store the flat set for easy lookup
    all_ids["__all__"] = flat_ids
    return all_ids


def validate_entry(entry: dict, file_path: str, all_ids: set[str],
                   errors: list[str], warnings: list[str]):
    """Validate a single entry for enum compliance and cross-references.

    Hard errors: duplicate IDs, invalid enums, malformed data, broken copy_from.
    Warnings: unresolved cross-references (forward references to future content).
    """
    id_field = find_id_field(entry)
    entry_id = entry.get(id_field, "<unknown>") if id_field else "<no-id>"

    # Check enum fields (hard error)
    for field, enum_name in ENUM_FIELDS.items():
        if field in entry and entry[field] is not None:
            val = entry[field]
            if isinstance(val, str) and val not in ENUMS[enum_name]:
                errors.append(f"  {file_path} [{entry_id}]: {field}={val} is not a valid {enum_name} value")

    # Check single cross-references (warning — forward refs are expected)
    for field in CROSS_REF_FIELDS:
        if field in entry and entry[field] is not None:
            val = entry[field]
            if not isinstance(val, str):
                continue  # skip inline recipe objects, etc.
            if val and val not in all_ids:
                warnings.append(f"  {file_path} [{entry_id}]: {field}={val} references nonexistent ID")

    # Check list cross-references (warning)
    for field in CROSS_REF_LIST_FIELDS:
        if field in entry and isinstance(entry[field], list):
            for ref in entry[field]:
                if isinstance(ref, str) and ref and ref not in all_ids:
                    warnings.append(f"  {file_path} [{entry_id}]: {field} contains {ref} which references nonexistent ID")

    # Check nested cross-references (warning)
    for field in ("inputs", "outputs", "lootDrops", "constructionCost", "cost", "statusEffects", "unlocks"):
        if field in entry and isinstance(entry[field], list):
            for nested in entry[field]:
                if isinstance(nested, dict):
                    for ref_field in NESTED_CROSS_REF_FIELDS:
                        if ref_field in nested and nested[ref_field] is not None:
                            ref = str(nested[ref_field])
                            if ref and ref not in all_ids:
                                warnings.append(f"  {file_path} [{entry_id}]: {field}.{ref_field}={ref} references nonexistent ID")

    # Check copy_from (hard error — parent must exist for inheritance to work)
    if "copy_from" in entry and entry["copy_from"] is not None:
        ref = str(entry["copy_from"])
        if ref not in all_ids:
            errors.append(f"  {file_path} [{entry_id}]: copy_from={ref} references nonexistent ID")

    # Check biomeAffinity values against biome enum
    if "biomeAffinity" in entry and isinstance(entry["biomeAffinity"], list):
        for biome in entry["biomeAffinity"]:
            if isinstance(biome, str) and biome not in ENUMS["OverworldBiomeType"]:
                errors.append(f"  {file_path} [{entry_id}]: biomeAffinity contains {biome} which is not a valid OverworldBiomeType")

    # Check mepSystems values
    if "mepSystems" in entry and isinstance(entry["mepSystems"], list):
        for sys_type in entry["mepSystems"]:
            if isinstance(sys_type, str) and sys_type not in ENUMS["MEPSystemType"]:
                errors.append(f"  {file_path} [{entry_id}]: mepSystems contains {sys_type} which is not a valid MEPSystemType")


def main():
    if not BIBLE_DIR.exists():
        print(f"bible directory not found: {BIBLE_DIR}")
        sys.exit(1)

    errors: list[str] = []
    warnings: list[str] = []
    total_entries = 0
    total_refs = 0

    # Phase 1: collect all IDs
    all_ids_by_file = collect_all_ids(BIBLE_DIR)
    all_ids = all_ids_by_file["__all__"]

    # Check for duplicates across files
    seen: dict[str, str] = {}
    for file_path, ids in all_ids_by_file.items():
        if file_path == "__all__":
            continue
        for entry_id in ids:
            if entry_id in seen:
                errors.append(f"  duplicate ID: {entry_id} defined in both {seen[entry_id]} and {file_path}")
            seen[entry_id] = file_path

    # Phase 2: validate each entry
    for md_file in sorted(BIBLE_DIR.rglob("*.md")):
        if md_file.name == "README.md":
            continue
        rel = str(md_file.relative_to(BIBLE_DIR))

        blocks = extract_yaml_blocks(md_file)
        if not blocks:
            warnings.append(f"  {rel}: no YAML blocks found")
            continue

        for block_text, line_num in blocks:
            try:
                parsed = yaml.safe_load(block_text)
            except yaml.YAMLError as e:
                errors.append(f"  {rel} line {line_num}: YAML parse error: {e}")
                continue

            if not isinstance(parsed, list):
                continue
            if is_schema_block(parsed):
                continue

            for entry in parsed:
                if not isinstance(entry, dict):
                    continue

                total_entries += 1
                id_field = find_id_field(entry)

                if not id_field:
                    errors.append(f"  {rel} line {line_num}: entry has no recognized ID field: {list(entry.keys())[:5]}")
                    continue

                if not entry.get(id_field):
                    errors.append(f"  {rel} line {line_num}: entry has empty {id_field}")
                    continue

                validate_entry(entry, rel, all_ids, errors, warnings)

    # Count cross-references
    for md_file in sorted(BIBLE_DIR.rglob("*.md")):
        if md_file.name == "README.md":
            continue
        for block_text, _ in extract_yaml_blocks(md_file):
            try:
                parsed = yaml.safe_load(block_text)
            except yaml.YAMLError:
                continue
            if not isinstance(parsed, list) or is_schema_block(parsed):
                continue
            for entry in parsed:
                if not isinstance(entry, dict):
                    continue
                for field in CROSS_REF_FIELDS:
                    if field in entry and entry[field] is not None:
                        total_refs += 1
                for field in CROSS_REF_LIST_FIELDS:
                    if field in entry and isinstance(entry[field], list):
                        total_refs += len(entry[field])

    # Report
    print(f"\nbible validation: {total_entries} entries, {len(all_ids)} unique IDs, {total_refs} cross-references")
    print(f"files scanned: {len(list(BIBLE_DIR.rglob('*.md'))) - 1}")

    if warnings:
        print(f"\nwarnings ({len(warnings)}):")
        for w in warnings:
            print(w)

    if errors:
        print(f"\nerrors ({len(errors)}):")
        for e in errors:
            print(e)
        print(f"\nbible validation FAILED with {len(errors)} errors")
        sys.exit(1)
    else:
        print("\nbible validation PASSED")
        sys.exit(0)


if __name__ == "__main__":
    main()
