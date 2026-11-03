"""A stand-in for the Google Apps Script, for testing the game's logging on this computer.

    python tools/mock_sheet.py 8796

It answers like tools/apps_script.gs: checks the token and each row, skips a row_id it already has,
splits Pilot and Study rows, and replies {"ok": true, ...}. Rows are written to
tools/private/mock_sheet_<tab>.csv.
  GET /status        how many rows each tab holds
  GET /fail, /ok     make the next posts fail (HTTP 500) or succeed again, to test the game's retries
"""
import csv
import json
import re
import sys
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TOKEN = re.search(r'LogToken\s*=\s*"([^"]+)"',
                  (ROOT / "Assets/DiveGame/Scripts/Core/GameConfig.cs").read_text(encoding="utf-8")).group(1)
COLUMNS = ["row_id", "study_id", "section", "version", "build_version", "session_id", "dive_id",
           "question_id", "answer", "correct", "attempt_number", "time_on_question_s",
           "time_in_dive_s", "platform", "timestamp", "fps_avg", "fps_p5", "canvas_w", "canvas_h",
           "received"]
MEASURES = ("fps_avg", "fps_p5", "canvas_w", "canvas_h")   # 0 = not measured: written as an empty cell
OUT = ROOT / "tools/private"
OUT.mkdir(parents=True, exist_ok=True)
tabs = {}          # tab -> list of rows
failing = False


def looks_right(r):
    return (isinstance(r, dict)
            and re.fullmatch(r"[0-9a-f]{12}-\d{4}", r.get("row_id") or "")
            and re.fullmatch(r"[0-9a-f]{10}", r.get("study_id") or "")
            and r.get("version") in ("A", "B")
            and re.fullmatch(r"[A-Z0-9]{1,10}|none", r.get("section") or "")
            and isinstance(r.get("build_version"), str) and len(r["build_version"]) <= 40
            and isinstance(r.get("question_id"), str) and len(r["question_id"]) <= 40
            and isinstance(r.get("answer"), str) and len(r["answer"]) <= 2000
            and r.get("platform") in ("desktop", "mobile")
            and in_range(r.get("fps_avg"), 0, 1000) and in_range(r.get("fps_p5"), 0, 1000)
            and in_range(r.get("canvas_w"), 0, 20000) and in_range(r.get("canvas_h"), 0, 20000))


def in_range(v, lo, hi):
    return v is None or (isinstance(v, (int, float)) and not isinstance(v, bool) and lo <= v <= hi)


class Handler(BaseHTTPRequestHandler):
    def reply(self, code, obj):
        body = json.dumps(obj).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        global failing
        if self.path.startswith("/fail"): failing = True
        if self.path.startswith("/ok"): failing = False
        self.reply(200, {"failing": failing, "rows": {t: len(v) for t, v in tabs.items()}})

    def do_POST(self):
        data = self.rfile.read(int(self.headers.get("Content-Length", 0)))
        print(f"POST {len(data)} bytes, Content-Type: {self.headers.get('Content-Type')}", flush=True)
        if failing:
            return self.reply(500, {"ok": False, "error": "failing on purpose"})
        try:
            batch = json.loads(data)
        except Exception:
            return self.reply(200, {"ok": False, "error": "not JSON"})
        if batch.get("token") != TOKEN:
            return self.reply(200, {"ok": False, "error": "bad token"})
        added = dup = refused = 0
        for r in (batch.get("rows") or [])[:200]:
            if not looks_right(r):
                refused += 1
                continue
            tab = "Pilot" if r["build_version"].endswith("-pilot") else "Study"
            rows = tabs.setdefault(tab, [])
            if any(x["row_id"] == r["row_id"] for x in rows):
                dup += 1
                continue
            r = dict(r, received=datetime.now(timezone.utc).isoformat())
            for m in MEASURES:
                if not r.get(m): r[m] = ""
            rows.append(r)
            added += 1
            with open(OUT / f"mock_sheet_{tab}.csv", "w", newline="", encoding="utf-8") as f:
                w = csv.DictWriter(f, COLUMNS, extrasaction="ignore")
                w.writeheader()
                w.writerows(rows)
        print(f"  added {added}, duplicates {dup}, refused {refused}", flush=True)
        self.reply(200, {"ok": True, "added": added, "duplicates": dup, "refused": refused})

    def log_message(self, *args):
        pass


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8796
    print(f"Stand-in Sheet on http://127.0.0.1:{port}/", flush=True)
    ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
