"""De-identify a D2L export for the Gas Laws Dive study.

Reads a CSV with a username column (a D2L quiz or grades export), works out each student's study
ID and game version with the same steps as the game, replaces the username with the study ID, drops
the name and ID columns, and writes a new CSV. Run it only on files from which the opt-outs and
under-18 students have already been removed (IRB plan, A9 steps 3 and 4).

    python tools/link_ids.py export.csv deidentified.csv
    python tools/link_ids.py export.csv deidentified.csv --username-column "Username"

The study secret is read from Assets/DiveGame/Scripts/Study/StudySecrets.cs (or given with
--secret-file), so this script itself holds no secret and may be published.
"""
import argparse
import csv
import hashlib
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SECRETS_CS = ROOT / "Assets/DiveGame/Scripts/Study/StudySecrets.cs"

# Columns that identify a student and are dropped (matched case-insensitively, ignoring spaces).
DROP = {"username", "orgdefinedid", "firstname", "lastname", "name", "email", "studentid", "id", "userid"}


def normalize(u):
    """Same as the game: trim, lowercase, remove spaces, drop anything after an @."""
    u = (u or "").strip().lower().replace(" ", "")
    return u.split("@", 1)[0]


def study_hash(secret, username):
    return hashlib.sha256((secret + "|" + normalize(username)).encode("utf-8")).digest()


def study_id(secret, username):
    return study_hash(secret, username)[:5].hex()


def version(secret, username):
    return "A" if study_hash(secret, username)[31] % 2 == 0 else "B"


def read_secret(secret_file):
    if secret_file:
        return Path(secret_file).read_text(encoding="utf-8").strip()
    m = re.search(r'StudySecret\s*=\s*"([^"]+)"', SECRETS_CS.read_text(encoding="utf-8"))
    if not m:
        sys.exit("No StudySecret found in " + str(SECRETS_CS))
    return m.group(1)


def key(name):
    return re.sub(r"[\s_#-]", "", name or "").lower()


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("input")
    p.add_argument("output")
    p.add_argument("--username-column", help="name of the username column (default: the one called Username)")
    p.add_argument("--secret-file", help="a text file holding the study secret, instead of StudySecrets.cs")
    a = p.parse_args()
    secret = read_secret(a.secret_file)

    with open(a.input, newline="", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)
        cols = reader.fieldnames or []
        ucol = a.username_column or next((c for c in cols if key(c) == "username"), None)
        if ucol not in cols:
            sys.exit("No username column found. Columns are: " + ", ".join(cols))
        keep = [c for c in cols if key(c) not in DROP and c != ucol]
        rows = list(reader)

    with open(a.output, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["study_id", "version"] + keep)
        for r in rows:
            u = r.get(ucol, "")
            if not normalize(u):
                continue
            w.writerow([study_id(secret, u), version(secret, u)] + [r.get(c, "") for c in keep])
    print(f"Wrote {len(rows)} rows to {a.output}; dropped columns: {', '.join(c for c in cols if c not in keep)}")


if __name__ == "__main__":
    main()
