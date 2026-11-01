"""Check Assets/StreamingAssets/questions.json before a build.

    python tools/check_questions.py

It checks that the file reads, every question is complete (id, dive, type, prompt, 2 to 6
options, a correct letter, one feedback line per option), ids are not repeated, the dives in
the build have 3 to 5 questions each, every calculation's "check" works out to the stated value
and that value appears in the correct option, and that no question repeats the numbers of one of
the 16 D2L test items (IRB Part C1), which must stay separate from the game.
"""
import json
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FILE = ROOT / "Assets/StreamingAssets/questions.json"
CONFIG = ROOT / "Assets/DiveGame/Scripts/Core/GameConfig.cs"
LETTERS = "ABCDEF"

# Number pairs that identify a D2L test item (IRB Part C1, items 13 to 16). A question holding
# every number of one set is too close to that item.
TEST_ITEMS = {
    "item 13 (6.0 L, 1.0 to 3.0 atm)": ["6.0 L", "3.0 atm", "1.0 atm"],
    "item 14 (4.0 L, 28 to 18 °C)": ["4.0 L", "28 °C", "18 °C"],
    "item 14 temperatures (28 to 18 °C, volume)": ["28 °C", "18 °C", " L"],
    "item 15 (1.5 L, 4.0 atm, 12 to 28 °C)": ["1.5 L", "4.0 atm", "12 °C"],
    "item 16 (40 m, 21% oxygen)": ["40 m", "21%"],
    "item 16 (5.0 atm, oxygen)": ["5.0 atm", "oxygen"],
}

# Volumes (in L) the side panel shows at each dive's set moments (DiveDirector.cs). A calculation
# question must not use one of its own dive's volumes in its prompt or its right answer: the
# student could read the answer off the screen instead of working it out (his rule, 2026-11-01).
# Dive 1: 5.00 L at the surface, 2.50 at 10 m, 1.67 at 20 m, 1.25 at 30 m, 2.50 after adding air.
# Dive 2: 3.50 L at 28 °C, 3.35 L at 15 °C. Dive 5: lungs 2.00 L at 30 m, 2.80 L when told to
# breathe out, 2.02 L at the surface.
SCREEN_VOLUMES = {
    1: [5.00, 2.50, 1.67, 1.25],
    2: [3.50, 3.35],
    5: [2.00, 2.80, 2.02],
}


def volumes_in(text):
    """Every 'number L' in the text (not mL), with how many decimals it was written with."""
    out = []
    for m in re.finditer(r"(\d+(?:\.(\d+))?)\s*L\b", text):
        if m.start() > 0 and text[m.start() - 1].isalpha():
            continue
        out.append((float(m.group(1)), len(m.group(2) or "")))
    return out


def dives_in_build():
    m = re.search(r"Dives\s*=\s*\{([^}]*)\}", CONFIG.read_text(encoding="utf-8"))
    return [int(x) for x in m.group(1).split(",")] if m else [1, 2, 3, 4, 5]


def safe_eval(expr):
    if not re.fullmatch(r"[0-9+\-*/(). ]+", expr):
        raise ValueError("only numbers and + - * / ( ) are allowed")
    return eval(expr, {"__builtins__": {}}, {})


def main():
    problems, notes = [], []
    try:
        data = json.loads(FILE.read_text(encoding="utf-8"))
    except Exception as e:
        print("questions.json does not read as JSON:", e)
        return 1

    seen, per_dive = set(), {}
    for i, q in enumerate(data.get("questions", [])):
        qid = q.get("id") or f"question #{i + 1}"
        where = f"{qid}:"
        for field in ("id", "dive", "type", "prompt", "options", "correct", "feedback"):
            if q.get(field) in (None, "", []):
                problems.append(f"{where} missing '{field}'")
        if qid in seen:
            problems.append(f"{where} id used twice")
        seen.add(qid)
        if q.get("type") not in ("particle", "calculation"):
            problems.append(f"{where} type must be 'particle' or 'calculation'")
        opts = q.get("options") or []
        if not 2 <= len(opts) <= 6:
            problems.append(f"{where} needs 2 to 6 options")
        letter = str(q.get("correct", "")).strip().upper()
        if len(letter) != 1 or letter not in LETTERS[: len(opts)]:
            problems.append(f"{where} correct must be one of {LETTERS[:len(opts)]}")
        if len(q.get("feedback") or []) != len(opts):
            problems.append(f"{where} needs one feedback line per option ({len(opts)})")
        per_dive[q.get("dive")] = per_dive.get(q.get("dive"), 0) + 1

        check = q.get("check") or {}
        expr = (check.get("expr") or "").strip()
        if q.get("type") == "calculation" and not expr:
            problems.append(f"{where} a calculation needs a check expression")
        if expr:
            try:
                got = safe_eval(expr)
                want = float(check.get("value"))
                digits = len(str(check.get("value")).split(".")[1]) if "." in str(check.get("value")) else 0
                if round(got, digits) != round(want, digits):
                    problems.append(f"{where} check gives {got:.4g}, but the stated value is {want}")
                elif letter in LETTERS[: len(opts)] and q.get("type") == "calculation":
                    shown = f"{want:.{digits}f}"
                    if shown not in opts[LETTERS.index(letter)]:
                        problems.append(f"{where} the correct option does not show {shown}")
                notes.append(f"{qid}: {expr} = {got:.4f} -> {want}")
            except Exception as e:
                problems.append(f"{where} check expression fails: {e}")

        text = q.get("prompt", "") + " " + " ".join(opts)
        for item, parts in TEST_ITEMS.items():
            if all(p in text for p in parts):
                problems.append(f"{where} too close to D2L test {item}")

        if q.get("type") == "calculation" and letter in LETTERS[: len(opts)]:
            own = q.get("prompt", "") + " " + opts[LETTERS.index(letter)]
            for x, dec in volumes_in(own):
                for v in SCREEN_VOLUMES.get(q.get("dive"), []):
                    if abs(x - v) <= 0.5 * 10 ** -dec + 1e-9:
                        problems.append(f"{where} uses {x} L, which Dive {q.get('dive')} shows on screen ({v:.2f} L)")

    # A right answer must not turn up as a wrong option in another question.
    qs = data.get("questions", [])
    rights = {}
    for q in qs:
        k = "ABCDEF".find(str(q.get("correct", "")).strip().upper())
        if q.get("type") == "calculation" and 0 <= k < len(q.get("options") or []):
            rights[q["options"][k].strip()] = q.get("id")
    for q in qs:
        k = "ABCDEF".find(str(q.get("correct", "")).strip().upper())
        for i, o in enumerate(q.get("options") or []):
            owner = rights.get(o.strip())
            if i != k and owner and owner != q.get("id"):
                problems.append(f"{q.get('id')}: wrong option '{o}' is the right answer of {owner}")

    # The right answer should not keep falling on the same letter.
    letters = [str(q.get("correct", "")).strip().upper() for q in data.get("questions", [])]
    for L in set(letters):
        if letters and letters.count(L) > len(letters) / 2:
            problems.append(f"the right answer is {L} in {letters.count(L)} of {len(letters)} questions; mix the letters")

    for d in dives_in_build():
        n = per_dive.get(d, 0)
        if not 3 <= n <= 5:
            problems.append(f"Dive {d} has {n} questions; it needs 3 to 5")

    for n in notes:
        print("  checked", n)
    if problems:
        print(f"\n{len(problems)} problem(s):")
        for p in problems:
            print("  -", p)
        return 1
    print(f"\nAll {len(seen)} questions pass. Questions per dive: {dict(sorted(per_dive.items()))}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
