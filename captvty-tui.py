#!/usr/bin/env python3

import curses
import json
import os
import re
import subprocess
import sys
import unicodedata
from collections import defaultdict
from pathlib import Path


CACHE = Path("captvty-cache.json")


def load_cache():
    with CACHE.open(encoding="utf-8") as f:
        data = json.load(f)

    by_channel = defaultdict(list)

    for p in data.get("programs", []):
        channel = p.get("channel", "")
        title = p.get("title", "")
        subtitle = p.get("subtitle", "")

        if channel and title:
            by_channel[channel].append({
                "title": title,
                "subtitle": subtitle,
            })

    for programs in by_channel.values():
        programs.sort(key=lambda x: (
            x["title"].casefold(),
            x["subtitle"].casefold()
        ))

    status = data.get("status", {})
    updated = data.get("updated", "")

    return by_channel, status, updated


def clip(text, width):
    if width <= 0:
        return ""
    if len(text) <= width:
        return text
    if width <= 1:
        return text[:width]
    return text[:width - 1] + "…"


def addstr_safe(win, y, x, text, attr=0):
    h, w = win.getmaxyx()

    if y < 0 or y >= h or x < 0 or x >= w:
        return

    try:
        win.addstr(y, x, clip(text, w - x - 1), attr)
    except curses.error:
        pass


def normalize_search(text):
    """Normalise texte pour une recherche simple et tolérante."""
    text = unicodedata.normalize("NFKD", text.casefold())
    text = "".join(
        c for c in text
        if not unicodedata.combining(c)
    )

    # Ponctuation -> espaces
    text = "".join(
        c if c.isalnum() else " "
        for c in text
    )

    return " ".join(text.split())


def search_programs(by_channel, query):
    words = normalize_search(query).split()

    if not words:
        return []

    result = []

    for channel, programs in by_channel.items():
        for p in programs:
            haystack = normalize_search(
                channel + " " +
                p["title"] + " " +
                p["subtitle"]
            )

            if all(word in haystack for word in words):
                result.append((channel, p))

    return result

def popup(stdscr, lines):
    h, w = stdscr.getmaxyx()

    width = min(
        w - 4,
        max(40, max((len(x) for x in lines), default=0) + 4)
    )
    height = min(h - 4, len(lines) + 4)

    if width < 10 or height < 5:
        return

    y = (h - height) // 2
    x = (w - width) // 2

    win = curses.newwin(height, width, y, x)
    win.keypad(True)
    win.box()

    for i, line in enumerate(lines[:height - 3]):
        addstr_safe(win, i + 1, 2, line)

    addstr_safe(
        win,
        height - 2,
        2,
        "Entrée/Echap : fermer",
        curses.A_DIM
    )

    win.refresh()

    while True:
        k = win.getch()
        if k in (10, 13, 27, ord("q")):
            break


def parse_qualities(lines):
    qualities = []

    rx = re.compile(
        r"^\s*(\*)?\s*"
        r"(\d+)x(\d+)\s+"
        r"(\d+)p\s+"
        r"(\d+)\s+kb/s"
    )

    for line in lines:
        m = rx.match(line)
        if not m:
            continue

        qualities.append({
            "number": len(qualities) + 1,
            "best": bool(m.group(1)),
            "width": int(m.group(2)),
            "height": int(m.group(3)),
            "label": int(m.group(4)),
            "bitrate": int(m.group(5)),
        })

    return qualities


def quality_popup(stdscr, qualities):
    if not qualities:
        popup(stdscr, ["Aucune qualité trouvée."])
        return None

    h, w = stdscr.getmaxyx()

    width = min(w - 4, 64)
    height = min(h - 4, len(qualities) + 5)

    if width < 30 or height < 6:
        return None

    y = (h - height) // 2
    x = (w - width) // 2

    win = curses.newwin(height, width, y, x)
    win.keypad(True)

    pos = 0

    while True:
        win.erase()
        win.box()

        addstr_safe(
            win, 0, 2,
            " Qualité ",
            curses.A_BOLD
        )

        for i, q in enumerate(qualities):
            marker = ">" if i == pos else " "
            best = "  [choix Captvty]" if q["best"] else ""

            text = (
                f"{marker} {q['number']}. "
                f"{q['width']}x{q['height']}  "
                f"{q['label']}p  "
                f"{q['bitrate']} kb/s"
                f"{best}"
            )

            attr = curses.A_REVERSE if i == pos else 0
            addstr_safe(win, i + 2, 2, text, attr)

        addstr_safe(
            win,
            height - 2,
            2,
            "↑↓ choisir  G télécharger  Echap annuler",
            curses.A_DIM
        )

        win.refresh()

        k = win.getch()

        if k == curses.KEY_UP:
            pos = max(0, pos - 1)

        elif k == curses.KEY_DOWN:
            pos = min(len(qualities) - 1, pos + 1)

        elif k in (ord("g"), ord("G")):
            return qualities[pos]["number"]

        elif k in (27, ord("q"), ord("Q"), 10, 13):
            return None


def prompt(stdscr, label):
    h, w = stdscr.getmaxyx()

    curses.echo()
    curses.curs_set(1)

    stdscr.move(h - 1, 0)
    stdscr.clrtoeol()
    addstr_safe(stdscr, h - 1, 0, label, curses.A_BOLD)
    stdscr.refresh()

    try:
        raw = stdscr.getstr(
            h - 1,
            min(len(label), w - 1),
            max(1, w - len(label) - 2)
        )
        return raw.decode("utf-8", errors="replace")
    finally:
        curses.noecho()
        curses.curs_set(0)



def run_info(channel, title):
    env = os.environ.copy()

    cwd = os.getcwd()
    env["MONO_PATH"] = f"{cwd}:{cwd}/bin"

    try:
        r = subprocess.run(
            [
                "mono",
                "captvty-cli.exe",
                "info",
                channel,
                title,
            ],
            env=env,
            capture_output=True,
            text=True,
            timeout=600,
        )
    except subprocess.TimeoutExpired:
        return ["Timeout pendant info"]
    except Exception as e:
        return [f"Erreur : {e}"]

    output = r.stdout

    if r.stderr:
        if output:
            output += "\n"
        output += r.stderr

    lines = output.splitlines()

    if not lines:
        lines = [f"info terminé, code retour {r.returncode}"]

    return lines

def run_get(channel, title, subtitle, quality):
    env = os.environ.copy()

    cwd = os.getcwd()
    env["MONO_PATH"] = f"{cwd}:{cwd}/bin"

    try:
        r = subprocess.run(
            [
                "mono",
                "captvty-cli.exe",
                "get",
                channel,
                title,
                subtitle,
                str(quality),
            ],
            env=env,
            capture_output=True,
            text=True,
            timeout=3600,
        )
    except subprocess.TimeoutExpired:
        return ["Timeout pendant le téléchargement"]
    except Exception as e:
        return [f"Erreur : {e}"]

    output = r.stdout

    if r.stderr:
        if output:
            output += "\n"
        output += r.stderr

    lines = output.splitlines()

    if not lines:
        lines = [
            f"get terminé, code retour {r.returncode}"
        ]

    # Le provider termine par :
    # DOWNLOAD <chaîne> <titre> <chemin final>
    # Le chemin peut contenir des espaces : on cherche donc parmi
    # les lignes de sortie le fichier final réellement créé.
    if r.returncode == 0:
        final_path = None

        for line in reversed(lines):
            if not line.startswith("DOWNLOAD "):
                continue

            # Le chemin final est absolu dans la sortie du provider.
            marker = " /"
            pos = line.find(marker)

            if pos >= 0:
                candidate = line[pos + 1:].strip()

                if os.path.isfile(candidate):
                    final_path = candidate
                    break

        if final_path:
            size = os.path.getsize(final_path)
            size_mb = size / 1_000_000

            lines.extend([
                "",
                f"Taille : {size_mb:.1f} Mo"
            ])

    return lines


def main(stdscr):
    curses.curs_set(0)
    stdscr.keypad(True)

    by_channel, status, updated = load_cache()

    channels = sorted(by_channel, key=str.casefold)

    channel_pos = 0
    program_pos = 0
    focus = 0

    search_results = None
    search_query = ""

    while True:
        stdscr.erase()
        h, w = stdscr.getmaxyx()

        if h < 12 or w < 70:
            addstr_safe(
                stdscr, 0, 0,
                "Terminal trop petit (minimum conseillé : 70x12)"
            )
            stdscr.refresh()
            stdscr.getch()
            continue

        left_w = min(30, max(20, w // 4))
        right_x = (
            left_w + 2
            if search_results is None
            else 0
        )

        selected_channel = (
            channels[channel_pos] if channels else ""
        )

        if search_results is None:
            programs = [
                (selected_channel, p)
                for p in by_channel.get(selected_channel, [])
            ]
            title = selected_channel
        else:
            programs = search_results
            title = f'Recherche "{search_query}"'

        if programs:
            program_pos = min(program_pos, len(programs) - 1)
        else:
            program_pos = 0

        addstr_safe(
            stdscr, 0, 0,
            "Captvty TUI",
            curses.A_BOLD
        )

        info = (
            f"{sum(len(v) for v in by_channel.values())} émissions"
        )
        if updated:
            info += f"  |  cache {updated}"

        addstr_safe(stdscr, 0, 15, info, curses.A_DIM)

        if search_results is None:
            addstr_safe(
                stdscr, 2, 0,
                "Chaînes",
                curses.A_BOLD |
                (curses.A_REVERSE if focus == 0 else 0)
            )

            addstr_safe(
                stdscr, 2, right_x,
                title,
                curses.A_BOLD |
                (curses.A_REVERSE if focus == 1 else 0)
            )
        else:
            addstr_safe(
                stdscr, 2, 0,
                f'{title} — {len(programs)} résultat(s)',
                curses.A_BOLD
            )

        visible = h - 6

        if search_results is None:
            c_start = max(
                0,
                channel_pos - visible + 1
                if channel_pos >= visible else 0
            )

            for row, i in enumerate(
                range(c_start, min(len(channels), c_start + visible)),
                start=3
            ):
                ch = channels[i]
                st = status.get(ch, "?")

                marker = ">" if i == channel_pos else " "
                attr = curses.A_REVERSE if (
                    focus == 0 and i == channel_pos
                ) else 0

                text = f"{marker} {ch}"
                if st != "ok":
                    text += f" [{st}]"

                addstr_safe(stdscr, row, 0, text, attr)

        p_start = max(
            0,
            program_pos - visible + 1
            if program_pos >= visible else 0
        )

        for row, i in enumerate(
            range(p_start, min(len(programs), p_start + visible)),
            start=3
        ):
            channel, p = programs[i]

            text = p["title"]

            if p["subtitle"]:
                text += " — " + p["subtitle"]

            if search_results is not None:
                text = f"[{channel}] {text}"

            attr = curses.A_REVERSE if (
                focus == 1 and i == program_pos
            ) else 0

            addstr_safe(stdscr, row, right_x, text, attr)

        if search_results is None:
            footer = (
                "↑↓ naviguer  ←→/Tab panneau  / rechercher  "
                "I info  Entrée détail  q quitter"
            )
        else:
            footer = (
                "↑↓ naviguer  I info  Entrée détail  "
                "Esc retour  / nouvelle recherche  q quitter"
            )

        addstr_safe(stdscr, h - 2, 0, footer, curses.A_DIM)

        if search_results is not None:
            addstr_safe(
                stdscr,
                h - 1,
                0,
                f"{len(search_results)} résultat(s)",
                curses.A_DIM
            )

        stdscr.refresh()

        k = stdscr.getch()

        if k in (ord("q"), ord("Q")):
            break

        elif k in (9, curses.KEY_RIGHT, curses.KEY_LEFT):
            if search_results is None:
                focus = 1 - focus

        elif k == curses.KEY_UP:
            if focus == 0:
                channel_pos = max(0, channel_pos - 1)
                program_pos = 0
                search_results = None
            else:
                program_pos = max(0, program_pos - 1)

        elif k == curses.KEY_DOWN:
            if focus == 0:
                channel_pos = min(
                    max(0, len(channels) - 1),
                    channel_pos + 1
                )
                program_pos = 0
                search_results = None
            else:
                program_pos = min(
                    max(0, len(programs) - 1),
                    program_pos + 1
                )

        elif k == ord("/"):
            q = prompt(stdscr, "Recherche : ").strip()

            if q:
                search_query = q
                search_results = search_programs(by_channel, q)
                program_pos = 0
                focus = 1

        elif k == 27:
            search_results = None
            search_query = ""
            program_pos = 0

        elif k in (ord("i"), ord("I")):
            if focus == 1 and programs:
                channel, p = programs[program_pos]

                stdscr.erase()
                addstr_safe(
                    stdscr,
                    0,
                    0,
                    f"Interrogation de Captvty : {channel} / {p['title']}",
                    curses.A_BOLD
                )
                addstr_safe(
                    stdscr,
                    2,
                    0,
                    "Veuillez patienter...",
                    curses.A_DIM
                )
                stdscr.refresh()

                info_title = p["title"]
                if p["subtitle"]:
                    info_title += " - " + p["subtitle"]

                lines = run_info(channel, info_title)
                qualities = parse_qualities(lines)

                if qualities:
                    quality = quality_popup(stdscr, qualities)

                    if quality is not None:
                        stdscr.erase()
                        addstr_safe(
                            stdscr,
                            0,
                            0,
                            f"Téléchargement : {channel} / {p['title']}",
                            curses.A_BOLD
                        )
                        addstr_safe(
                            stdscr,
                            2,
                            0,
                            f"Qualité {quality} — veuillez patienter...",
                            curses.A_DIM
                        )
                        stdscr.refresh()

                        get_lines = run_get(
                            channel,
                            p["title"],
                            p["subtitle"],
                            quality
                        )
                        popup(stdscr, get_lines)
                else:
                    popup(stdscr, lines)

        elif k in (10, 13):
            if focus == 0:
                focus = 1
                program_pos = 0

            elif programs:
                channel, p = programs[program_pos]

                popup(stdscr, [
                    f"Chaîne : {channel}",
                    "",
                    f"Titre : {p['title']}",
                    f"Sous-titre : {p['subtitle'] or '-'}",
                ])


if __name__ == "__main__":
    if not CACHE.exists():
        print(f"Cache introuvable : {CACHE}", file=sys.stderr)
        sys.exit(1)

    curses.wrapper(main)
