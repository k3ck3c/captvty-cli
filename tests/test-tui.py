#!/usr/bin/env python3

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

spec = importlib.util.spec_from_file_location(
    "captvty_tui", ROOT / "captvty-tui.py"
)
tui = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tui)


class TestTUI(unittest.TestCase):

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

        old_cache = tui.CACHE
        self.addCleanup(setattr, tui, "CACHE", old_cache)

        tui.CACHE = Path(self.tmp.name) / "cache.json"

        programs = []

        for episode in ("S02E01", "S01E100", "S01E10",
                        "S01E04", "S01E02", "S01E01"):
            programs.append({
                "channel": "France 3",
                "title": "Saint-Pierre",
                "subtitle": "Épisode " + episode,
                "episode_id": episode + " Sous-titre"
            })

        for subtitle in ("Zulu", "Alpha"):
            programs.append({
                "channel": "France 3",
                "title": "Journal",
                "subtitle": subtitle
            })

        programs.append({
            "channel": "France 2",
            "title": "Astrid et Raphaëlle",
            "subtitle": "Le livre de Thot",
            "episode_id": "S05E01 Le livre de Thot"
        })

        tui.CACHE.write_text(
            json.dumps({"programs": programs}),
            encoding="utf-8"
        )

        self.channels, _, _ = tui.load_cache()

    def test_load_cache(self):
        self.assertEqual(len(self.channels), 2)
        self.assertEqual(len(self.channels["France 3"]), 8)

    def test_episode_extraction(self):
        results = tui.search_programs(
            self.channels, "Saint-Pierre S01E04"
        )
        self.assertEqual(len(results), 1)

        channel, p = results[0]

        self.assertEqual(channel, "France 3")
        self.assertEqual(p["episode"], "S01E04")
        self.assertEqual(p["season_num"], 1)
        self.assertEqual(p["episode_num"], 4)

    def test_episode_numeric_sort(self):
        episodes = [
            p["episode"]
            for p in self.channels["France 3"]
            if p["title"] == "Saint-Pierre"
        ]

        self.assertEqual(episodes, [
            "S01E01",
            "S01E02",
            "S01E04",
            "S01E10",
            "S01E100",
            "S02E01"
        ])

    def test_without_episode(self):
        programs = [
            p for p in self.channels["France 3"]
            if p["title"] == "Journal"
        ]

        self.assertEqual(
            [p["subtitle"] for p in programs],
            ["Alpha", "Zulu"]
        )

        self.assertTrue(
            all(p["episode"] == "" for p in programs)
        )

    def test_search_all_episodes(self):
        results = tui.search_programs(
            self.channels, "Saint-Pierre"
        )
        self.assertEqual(len(results), 6)

    def test_search_normalization(self):
        results = tui.search_programs(
            self.channels, "ASTRID RAPHAELLE"
        )
        self.assertEqual(len(results), 1)

    def test_search_channel(self):
        results = tui.search_programs(
            self.channels, "France 3 Journal"
        )
        self.assertEqual(len(results), 2)
        self.assertTrue(
            all(channel == "France 3" for channel, _ in results)
        )

        results = tui.search_programs(
            self.channels, "France 2 Journal"
        )
        self.assertEqual(results, [])

    def test_search_no_result(self):
        self.assertEqual(
            tui.search_programs(self.channels, "turlututu"),
            []
        )


if __name__ == "__main__":
    unittest.main(verbosity=2)
