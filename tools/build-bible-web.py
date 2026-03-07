#!/usr/bin/env python3
"""Build static HTML pages for the Slopworks game bible.

Reads all bible markdown files via bible_common, generates:
  - Main index page (docs/bible/index.html)
  - Category pages (docs/bible/{category}/index.html)
  - Entry detail pages (docs/bible/{entry_id}/index.html)

Usage:
    python3 tools/build-bible-web.py              # build all pages
    python3 tools/build-bible-web.py --clean       # remove generated HTML first
    python3 tools/build-bible-web.py --only items  # build only one category
"""

import argparse
import html
import shutil
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from bible_common import BIBLE_DIR, CATEGORIES, SUBCATEGORY_NAMES, collect_all_entries

OUTPUT_DIR = BIBLE_DIR  # generated HTML lives alongside .md source files


# ---------------------------------------------------------------------------
# Path helpers
# ---------------------------------------------------------------------------

def css_path(depth):
    """Return relative path to docs/css/style.css from a page at given depth."""
    return "../" * depth + "css/style.css"


def js_path(depth):
    """Return relative path to docs/js/main.js from a page at given depth."""
    return "../" * depth + "js/main.js"


def favicon_path(depth):
    """Return relative path to docs/favicon.svg from a page at given depth."""
    return "../" * depth + "favicon.svg"


def root_path(depth):
    """Return relative path back to docs/ from a page at given depth."""
    return "../" * depth


def img_path(depth, entry_id):
    """Return relative path to an entry's bible image."""
    return "../" * depth + f"assets/img/bible/{entry_id}.png"


def bible_root(depth):
    """Return relative path from current page to docs/bible/."""
    if depth == 2:
        return ""
    elif depth == 3:
        return "../"
    return "../" * (depth - 2)


def entry_url(depth, entry_id):
    """Return relative URL to an entry detail page."""
    return bible_root(depth) + f"{entry_id}/index.html"


def category_url(depth, category):
    """Return relative URL to a category page."""
    return bible_root(depth) + f"{category}/index.html"


# ---------------------------------------------------------------------------
# Data helpers
# ---------------------------------------------------------------------------

DISPLAY_FIELDS = {
    "_id", "_id_field", "_category", "_subcategory", "_source_file",
    "displayName", "description", "slopCommentary", "modelStyle",
    "rarity", "tags", "obtainedFrom", "text",
}

XREF_FIELDS = {"itemId", "effectId", "targetId", "recipeId", "machineId",
                "faunaId", "biomeId", "buildingId", "turretId", "weaponId",
                "ammoType", "ammoItemId", "upgradePath", "researchRequired",
                "craftingRecipe", "defaultRecipe", "requiredMachineType",
                "relatedItemId", "copy_from"}


def entry_display_name(entry):
    """Get display name, falling back to the ID or text snippet."""
    name = entry.get("displayName")
    if name:
        return name
    # Dialogue lines use 'text' instead of displayName
    text = entry.get("text", "")
    if text:
        return text[:60] + ("..." if len(text) > 60 else "")
    return entry.get("_id", "unknown")


def esc(text):
    """HTML-escape a string."""
    if text is None:
        return ""
    return html.escape(str(text))


def rarity_class(rarity):
    """Return CSS class suffix for a rarity value."""
    if not rarity:
        return ""
    return rarity.lower()


# ---------------------------------------------------------------------------
# Inline CSS for bible pages (not in shared stylesheet)
# ---------------------------------------------------------------------------

BIBLE_CSS = """
/* bible page styles */
.bible-breadcrumb {
    font-family: var(--font-mono);
    font-size: 0.75rem;
    color: var(--text-dim);
    letter-spacing: 0.05em;
    text-transform: uppercase;
    margin-bottom: 1.5rem;
}
.bible-breadcrumb a {
    color: var(--text-dim);
}
.bible-breadcrumb a:hover {
    color: var(--accent);
}
.bible-breadcrumb .sep {
    margin: 0 0.5rem;
    color: var(--border);
}
.bible-search {
    width: 100%;
    padding: 0.75rem 1rem;
    background: var(--bg-surface);
    border: 1px solid var(--border);
    border-radius: 4px;
    color: var(--text);
    font-family: var(--font-mono);
    font-size: 0.85rem;
    margin-bottom: 2rem;
    outline: none;
    transition: border-color 0.2s;
}
.bible-search:focus {
    border-color: var(--accent-dim);
}
.bible-search::placeholder {
    color: var(--text-dim);
}
.section-label {
    font-family: var(--font-display);
    font-size: 1rem;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    color: var(--accent-dim);
    border-bottom: 1px solid var(--border);
    padding-bottom: 0.5rem;
    margin: 2rem 0 1rem;
}
.category-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: 1rem;
}
.entry-card {
    background: var(--bg-card);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 0.75rem 1rem;
    display: flex;
    align-items: center;
    gap: 0.75rem;
    text-decoration: none;
    color: inherit;
    transition: border-color 0.3s, transform 0.2s;
}
.entry-card:hover {
    border-color: var(--accent-dim);
    transform: translateY(-1px);
    text-decoration: none;
}
.entry-card .entry-thumb {
    width: 48px;
    height: 48px;
    border-radius: 4px;
    object-fit: cover;
    flex-shrink: 0;
    background: var(--bg-surface);
}
.entry-card .entry-card-info {
    min-width: 0;
}
.entry-card .entry-card-name {
    font-family: var(--font-display);
    font-size: 0.9rem;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    color: var(--text);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
.entry-card .entry-card-id {
    font-family: var(--font-mono);
    font-size: 0.7rem;
    color: var(--text-dim);
}
.entry-header {
    display: flex;
    gap: 2rem;
    align-items: flex-start;
    margin-bottom: 2rem;
    flex-wrap: wrap;
}
.entry-icon {
    width: 200px;
    height: 200px;
    border-radius: 4px;
    border: 1px solid var(--border-rust);
    object-fit: cover;
    flex-shrink: 0;
    background: var(--bg-surface);
}
.entry-meta {
    flex: 1;
    min-width: 250px;
}
.entry-meta h1 {
    font-size: clamp(1.5rem, 4vw, 2.5rem);
    margin-bottom: 0.25rem;
}
.entry-meta .entry-id {
    font-family: var(--font-mono);
    font-size: 0.8rem;
    color: var(--text-dim);
    margin-bottom: 0.75rem;
}
.entry-meta .entry-desc {
    color: var(--text);
    line-height: 1.7;
    margin-bottom: 1rem;
}
.tag {
    display: inline-block;
    font-family: var(--font-mono);
    font-size: 0.7rem;
    color: var(--teal);
    border: 1px solid var(--teal-dim);
    border-radius: 2px;
    padding: 0.15rem 0.5rem;
    margin: 0.15rem 0.25rem 0.15rem 0;
    text-transform: lowercase;
}
.rarity-badge {
    display: inline-block;
    font-family: var(--font-mono);
    font-size: 0.7rem;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    padding: 0.2rem 0.6rem;
    border-radius: 2px;
    margin-bottom: 0.75rem;
}
.rarity-badge.common {
    color: #aaa;
    border: 1px solid #555;
    background: rgba(170, 170, 170, 0.08);
}
.rarity-badge.uncommon {
    color: #4CAF50;
    border: 1px solid #2E7D32;
    background: rgba(76, 175, 80, 0.08);
}
.rarity-badge.rare {
    color: #42A5F5;
    border: 1px solid #1565C0;
    background: rgba(66, 165, 245, 0.08);
}
.rarity-badge.epic {
    color: #AB47BC;
    border: 1px solid #7B1FA2;
    background: rgba(171, 71, 188, 0.08);
}
.rarity-badge.legendary {
    color: #FFB300;
    border: 1px solid #FF8F00;
    background: rgba(255, 179, 0, 0.12);
}
.slop-callout {
    border-left: 3px solid var(--accent);
    background: rgba(232, 160, 49, 0.05);
    padding: 1rem 1.5rem;
    margin: 1.5rem 0;
    font-family: var(--font-mono);
    font-size: 0.85rem;
    color: var(--accent);
    line-height: 1.6;
    position: relative;
}
.slop-callout::before {
    content: 'S.L.O.P.://';
    font-size: 0.7rem;
    color: var(--accent-dim);
    position: absolute;
    top: -0.8rem;
    left: 0;
}
.stats-table {
    width: 100%;
    border-collapse: collapse;
    margin: 1.5rem 0;
    font-size: 0.85rem;
}
.stats-table th {
    text-align: left;
    font-family: var(--font-mono);
    font-size: 0.7rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--text-dim);
    padding: 0.5rem 0.75rem;
    border-bottom: 1px solid var(--border);
    background: var(--bg-surface);
}
.stats-table td {
    padding: 0.5rem 0.75rem;
    border-bottom: 1px solid var(--border);
    color: var(--text);
    vertical-align: top;
}
.stats-table tr:hover {
    background: rgba(255, 255, 255, 0.02);
}
.stats-table .field-name {
    font-family: var(--font-mono);
    font-size: 0.8rem;
    color: var(--teal);
    white-space: nowrap;
    width: 200px;
}
.obtained-table {
    width: 100%;
    border-collapse: collapse;
    margin: 1rem 0;
    font-size: 0.85rem;
}
.obtained-table th {
    text-align: left;
    font-family: var(--font-mono);
    font-size: 0.7rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--text-dim);
    padding: 0.5rem 0.75rem;
    border-bottom: 1px solid var(--border);
    background: var(--bg-surface);
}
.obtained-table td {
    padding: 0.5rem 0.75rem;
    border-bottom: 1px solid var(--border);
    color: var(--text);
}
.img-pending {
    width: 100%;
    height: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    background: var(--bg-surface);
    font-family: var(--font-mono);
    font-size: 0.7rem;
    color: var(--text-dim);
    text-transform: uppercase;
    letter-spacing: 0.1em;
    border: 1px solid var(--border);
    border-radius: 4px;
}
.bible-count {
    font-family: var(--font-mono);
    font-size: 0.85rem;
    color: var(--teal);
    margin-bottom: 0.5rem;
}
@media (max-width: 768px) {
    .entry-header {
        flex-direction: column;
        align-items: center;
        text-align: center;
    }
    .entry-icon {
        width: 150px;
        height: 150px;
    }
    .entry-meta {
        min-width: unset;
    }
    .category-grid {
        grid-template-columns: 1fr;
    }
    .stats-table .field-name {
        width: auto;
    }
}
"""


# ---------------------------------------------------------------------------
# HTML page wrapper
# ---------------------------------------------------------------------------

def page_html(title, depth, body_content, extra_head=""):
    """Wrap body content in a full HTML page."""
    rp = root_path(depth)
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{esc(title)} — Slopworks</title>
    <link rel="icon" type="image/svg+xml" href="{esc(favicon_path(depth))}">
    <link rel="stylesheet" href="{esc(css_path(depth))}">
    <style>{BIBLE_CSS}</style>
{extra_head}</head>
<body>
    <div id="nav" data-root="{esc(rp)}"></div>

{body_content}

    <div id="footer"></div>
    <script src="{esc(js_path(depth))}"></script>
</body>
</html>"""


# ---------------------------------------------------------------------------
# Image fallback helper
# ---------------------------------------------------------------------------

def img_tag(depth, entry_id, css_class="entry-thumb", alt=""):
    """Return an img tag with onerror fallback to a pending-art placeholder."""
    src = img_path(depth, entry_id)
    fallback = (
        "this.style.display='none';"
        "var d=document.createElement('div');"
        "d.className='img-pending';"
        "d.style.width=this.width?this.width+'px':'48px';"
        "d.style.height=this.height?this.height+'px':'48px';"
        "d.textContent='[ART PENDING]';"
        "this.parentNode.insertBefore(d,this);"
    )
    return (
        f'<img src="{esc(src)}" alt="{esc(alt)}" class="{css_class}" '
        f'loading="lazy" onerror="{esc(fallback)}">'
    )


# ---------------------------------------------------------------------------
# Cross-reference rendering
# ---------------------------------------------------------------------------

def render_value(value, depth, all_ids):
    """Render a field value as HTML, with cross-reference links for known IDs."""
    if value is None:
        return '<span style="color: var(--text-dim)">null</span>'
    if isinstance(value, bool):
        return esc(str(value).lower())
    if isinstance(value, (int, float)):
        return esc(str(value))
    if isinstance(value, str):
        if value in all_ids:
            url = entry_url(depth, value)
            return f'<a href="{esc(url)}">{esc(value)}</a>'
        return esc(value)
    if isinstance(value, list):
        if not value:
            return '<span style="color: var(--text-dim)">[]</span>'
        # List of dicts (e.g. inputs, outputs, ports, obtainedFrom)
        if isinstance(value[0], dict):
            parts = []
            for item in value:
                rendered_fields = []
                for k, v in item.items():
                    rv = render_value(v, depth, all_ids)
                    rendered_fields.append(f'<span class="field-name" style="width:auto">{esc(k)}</span>: {rv}')
                parts.append("{" + ", ".join(rendered_fields) + "}")
            return "<br>".join(parts)
        # List of strings/primitives
        rendered = []
        for item in value:
            if isinstance(item, str) and item in all_ids:
                url = entry_url(depth, item)
                rendered.append(f'<a href="{esc(url)}">{esc(item)}</a>')
            else:
                rendered.append(esc(str(item)))
        return ", ".join(rendered)
    if isinstance(value, dict):
        parts = []
        for k, v in value.items():
            rv = render_value(v, depth, all_ids)
            parts.append(f'{esc(k)}: {rv}')
        return "; ".join(parts)
    return esc(str(value))


# ---------------------------------------------------------------------------
# Search script (inline JS)
# ---------------------------------------------------------------------------

SEARCH_JS = """
<script>
(function() {
    var input = document.querySelector('.bible-search');
    if (!input) return;
    var cards = document.querySelectorAll('[data-search]');
    var sections = document.querySelectorAll('[data-section]');
    input.addEventListener('input', function() {
        var q = this.value.toLowerCase().trim();
        var visibleSections = {};
        cards.forEach(function(card) {
            var text = card.getAttribute('data-search').toLowerCase();
            var match = !q || text.indexOf(q) !== -1;
            card.style.display = match ? '' : 'none';
            if (match && card.getAttribute('data-section-id')) {
                visibleSections[card.getAttribute('data-section-id')] = true;
            }
        });
        if (sections.length) {
            sections.forEach(function(sec) {
                var id = sec.getAttribute('data-section');
                sec.style.display = (!q || visibleSections[id]) ? '' : 'none';
            });
        }
    });
})();
</script>
"""


# ---------------------------------------------------------------------------
# Page builders
# ---------------------------------------------------------------------------

def build_entry_card(entry, depth):
    """Build a compact entry card (used in index and category pages)."""
    eid = entry["_id"]
    name = entry_display_name(entry)
    url = entry_url(depth, eid)
    thumb = img_tag(depth, eid, css_class="entry-thumb", alt=name)
    return (
        f'<a href="{esc(url)}" class="entry-card" '
        f'data-search="{esc(name)} {esc(eid)}" '
        f'data-section-id="{esc(entry["_subcategory"])}">'
        f'{thumb}'
        f'<div class="entry-card-info">'
        f'<div class="entry-card-name">{esc(name)}</div>'
        f'<div class="entry-card-id">{esc(eid)}</div>'
        f'</div>'
        f'</a>'
    )


def build_main_index(entries):
    """Build the main bible index page at docs/bible/index.html (depth=2)."""
    depth = 2
    count = len(entries)

    # Category cards
    cat_cards = []
    entries_by_cat = defaultdict(list)
    for e in entries:
        entries_by_cat[e["_category"]].append(e)

    for cat_key, (cat_name, cat_desc) in CATEGORIES.items():
        cat_count = len(entries_by_cat.get(cat_key, []))
        url = category_url(depth, cat_key)
        cat_cards.append(
            f'<a href="{esc(url)}" class="card">'
            f'<div class="card-body">'
            f'<div class="card-title">{esc(cat_name)}</div>'
            f'<div class="card-desc">{esc(cat_desc)}</div>'
            f'<div style="font-family:var(--font-mono);font-size:0.75rem;color:var(--teal);margin-top:0.5rem;">{cat_count} entries</div>'
            f'</div>'
            f'</a>'
        )

    # All entries grid
    all_cards = []
    for e in entries:
        if e["_id"]:
            all_cards.append(build_entry_card(e, depth))

    body = f"""
    <div class="caution-divider"></div>
    <section class="section" style="opacity:1;transform:none;">
        <div class="container">
            <h1>Game bible</h1>
            <div class="bible-count">{count} entries across {len(CATEGORIES)} categories</div>
            <p class="text-dim">Complete reference for every item, building, creature, recipe, and system in Slopworks.</p>

            <input type="text" class="bible-search" placeholder="Search all entries by name or ID...">

            <h2 style="margin-top:2rem;">Categories</h2>
            <div class="card-grid">
                {"".join(cat_cards)}
            </div>

            <h2 style="margin-top:3rem;">All entries</h2>
            <div class="category-grid">
                {"".join(all_cards)}
            </div>
        </div>
    </section>
{SEARCH_JS}"""

    return page_html("Game bible", depth, body)


def build_category_page(category, entries):
    """Build a category page at docs/bible/{category}/index.html (depth=3)."""
    depth = 3
    cat_name, cat_desc = CATEGORIES.get(category, (category, ""))

    # Group by subcategory
    by_sub = defaultdict(list)
    for e in entries:
        by_sub[e["_subcategory"]].append(e)

    sections_html = []
    for sub_key, sub_entries in sorted(by_sub.items()):
        sub_name = SUBCATEGORY_NAMES.get(sub_key, sub_key.replace("-", " ").title())
        cards = [build_entry_card(e, depth) for e in sub_entries if e["_id"]]
        sections_html.append(
            f'<div data-section="{esc(sub_key)}">'
            f'<div class="section-label">{esc(sub_name)} ({len(sub_entries)})</div>'
            f'<div class="category-grid">'
            f'{"".join(cards)}'
            f'</div>'
            f'</div>'
        )

    breadcrumb = (
        f'<div class="bible-breadcrumb">'
        f'<a href="{bible_root(depth)}index.html">Bible</a>'
        f'<span class="sep">/</span>'
        f'{esc(cat_name)}'
        f'</div>'
    )

    body = f"""
    <div class="caution-divider"></div>
    <section class="section" style="opacity:1;transform:none;">
        <div class="container">
            {breadcrumb}
            <h1>{esc(cat_name)}</h1>
            <p class="text-dim">{esc(cat_desc)}</p>
            <div class="bible-count">{len(entries)} entries</div>

            <input type="text" class="bible-search" placeholder="Search {esc(cat_name.lower())} by name or ID...">

            {"".join(sections_html)}
        </div>
    </section>
{SEARCH_JS}"""

    return page_html(cat_name, depth, body)


def build_entry_page(entry, all_ids):
    """Build an entry detail page at docs/bible/{entry_id}/index.html (depth=3)."""
    depth = 3
    eid = entry["_id"]
    name = entry_display_name(entry)
    category = entry["_category"]
    subcategory = entry["_subcategory"]
    cat_name = CATEGORIES.get(category, (category, ""))[0]
    sub_name = SUBCATEGORY_NAMES.get(subcategory, subcategory.replace("-", " ").title())

    # Breadcrumb
    breadcrumb = (
        f'<div class="bible-breadcrumb">'
        f'<a href="{bible_root(depth)}index.html">Bible</a>'
        f'<span class="sep">/</span>'
        f'<a href="{category_url(depth, category)}">{esc(cat_name)}</a>'
        f'<span class="sep">/</span>'
        f'{esc(sub_name)}'
        f'</div>'
    )

    # Rarity badge
    rarity = entry.get("rarity")
    rarity_html = ""
    if rarity:
        rc = rarity_class(rarity)
        rarity_html = f'<span class="rarity-badge {rc}">{esc(rarity)}</span><br>'

    # Tags
    tags = entry.get("tags", [])
    tags_html = ""
    if tags:
        tag_spans = "".join(f'<span class="tag">{esc(t)}</span>' for t in tags)
        tags_html = f'<div style="margin-top:0.5rem;">{tag_spans}</div>'

    # Description
    desc = entry.get("description", entry.get("text", ""))
    desc_html = f'<p class="entry-desc">{esc(desc)}</p>' if desc else ""

    # Icon
    icon = img_tag(depth, eid, css_class="entry-icon", alt=name)

    # Header
    header = f"""
    <div class="entry-header">
        {icon}
        <div class="entry-meta">
            <h1>{esc(name)}</h1>
            <div class="entry-id">{esc(eid)}</div>
            {rarity_html}
            {desc_html}
            {tags_html}
        </div>
    </div>"""

    # SLOP commentary callout
    slop = entry.get("slopCommentary", "")
    slop_html = ""
    if slop:
        slop_html = f'<div class="slop-callout">{esc(slop)}</div>'

    # Stats table — all fields except display/meta ones
    stats_rows = []
    skip_fields = {
        "_id", "_id_field", "_category", "_subcategory", "_source_file",
        "displayName", "description", "slopCommentary", "tags", "rarity",
        "obtainedFrom", "modelStyle", "text",
    }
    for key, value in entry.items():
        if key in skip_fields:
            continue
        rendered = render_value(value, depth, all_ids)
        stats_rows.append(
            f'<tr>'
            f'<td class="field-name">{esc(key)}</td>'
            f'<td>{rendered}</td>'
            f'</tr>'
        )

    stats_html = ""
    if stats_rows:
        stats_html = f"""
    <h2 style="margin-top:2rem;">Stats</h2>
    <table class="stats-table">
        <thead><tr><th>Field</th><th>Value</th></tr></thead>
        <tbody>{"".join(stats_rows)}</tbody>
    </table>"""

    # Obtained from table
    obtained = entry.get("obtainedFrom", [])
    obtained_html = ""
    if obtained and isinstance(obtained, list):
        obt_rows = []
        for src in obtained:
            if isinstance(src, dict):
                source = esc(src.get("source", ""))
                details = esc(src.get("details", ""))
                obt_rows.append(f'<tr><td>{source}</td><td>{details}</td></tr>')
        if obt_rows:
            obtained_html = f"""
    <h2 style="margin-top:2rem;">Obtained from</h2>
    <table class="obtained-table">
        <thead><tr><th>Source</th><th>Details</th></tr></thead>
        <tbody>{"".join(obt_rows)}</tbody>
    </table>"""

    body = f"""
    <div class="caution-divider"></div>
    <section class="section" style="opacity:1;transform:none;">
        <div class="container">
            {breadcrumb}
            {header}
            {slop_html}
            {stats_html}
            {obtained_html}
        </div>
    </section>"""

    return page_html(f"{name}", depth, body)


# ---------------------------------------------------------------------------
# File writing
# ---------------------------------------------------------------------------

def write_page(path, content):
    """Write an HTML file, creating parent directories as needed."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


# ---------------------------------------------------------------------------
# Clean
# ---------------------------------------------------------------------------

def clean_generated(only_category=None):
    """Remove generated HTML files without touching source .md files."""
    # Remove main index
    if not only_category:
        main_index = OUTPUT_DIR / "index.html"
        if main_index.exists():
            main_index.unlink()
            print(f"  removed {main_index}")

    # Remove category index pages and entry directories
    categories_to_clean = [only_category] if only_category else list(CATEGORIES.keys())

    for cat in categories_to_clean:
        cat_index = OUTPUT_DIR / cat / "index.html"
        if cat_index.exists():
            cat_index.unlink()
            print(f"  removed {cat_index}")

    # Remove entry directories (docs/bible/{entry_id}/ where entry_id is not a category dir)
    # Entry directories contain only index.html and are named after entry IDs
    category_dirs = set(CATEGORIES.keys())
    for item in OUTPUT_DIR.iterdir():
        if item.is_dir() and item.name not in category_dirs:
            index_file = item / "index.html"
            if index_file.exists():
                # Only remove if this looks like a generated entry dir
                # (contains index.html and optionally nothing else)
                contents = list(item.iterdir())
                if all(c.name == "index.html" for c in contents):
                    shutil.rmtree(item)
                    print(f"  removed {item}/")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Build static HTML for the Slopworks game bible")
    parser.add_argument("--clean", action="store_true", help="Remove generated HTML before building")
    parser.add_argument("--only", type=str, default=None,
                        help="Build only one category (e.g. 'items')")
    args = parser.parse_args()

    if args.clean:
        print("Cleaning generated HTML...")
        clean_generated(args.only)
        print()

    print("Collecting bible entries...")
    entries = collect_all_entries()
    print(f"  found {len(entries)} entries")

    # Build ID set for cross-references
    all_ids = set(e["_id"] for e in entries if e["_id"])

    # Group by category
    by_category = defaultdict(list)
    for e in entries:
        by_category[e["_category"]].append(e)

    # Determine which categories to build
    if args.only:
        if args.only not in CATEGORIES:
            print(f"Unknown category: {args.only}")
            print(f"Valid categories: {', '.join(CATEGORIES.keys())}")
            sys.exit(1)
        cats_to_build = [args.only]
    else:
        cats_to_build = list(CATEGORIES.keys())

    # Filter entries for --only mode
    if args.only:
        build_entries = [e for e in entries if e["_category"] == args.only]
    else:
        build_entries = entries

    pages_written = 0

    # 1. Main index (skip in --only mode)
    if not args.only:
        print("\nBuilding main index...")
        html_content = build_main_index(entries)
        write_page(OUTPUT_DIR / "index.html", html_content)
        pages_written += 1
        print(f"  wrote {OUTPUT_DIR / 'index.html'}")

    # 2. Category pages
    print("\nBuilding category pages...")
    for cat in cats_to_build:
        cat_entries = by_category.get(cat, [])
        html_content = build_category_page(cat, cat_entries)
        write_page(OUTPUT_DIR / cat / "index.html", html_content)
        pages_written += 1
        print(f"  wrote {OUTPUT_DIR / cat / 'index.html'} ({len(cat_entries)} entries)")

    # 3. Entry detail pages
    print("\nBuilding entry pages...")
    for entry in build_entries:
        eid = entry["_id"]
        if not eid:
            continue
        html_content = build_entry_page(entry, all_ids)
        write_page(OUTPUT_DIR / eid / "index.html", html_content)
        pages_written += 1

    print(f"\nDone: {pages_written} pages written")


if __name__ == "__main__":
    main()
