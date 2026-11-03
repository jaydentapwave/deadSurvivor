#!/usr/bin/python3
"""Superseded (051, 2026-11-03): the asset pipeline moved to 3dkit with the same results (regression in
docs/reviews/3dkit-asset-pipeline-051.md: check, prep hashes, Unity build git-identical, captures pixel-identical).
  /usr/bin/python3 3dkit/tools/assets.py <spec|profile|list|gen|palette|prep|unity|check|capture|migrate|selftest>
  (palette-char -> palette char). Settings: 3dkit.config.json (assets.profile), catalog: docs/art/asset-catalog.json (v2).
Unity side: package com.studiokit.kit3d (3dkit/unity) + Assets/DeadSurvivor/Editor/DSKit3DProjectHooks.cs.
The last version of this file and of AssetBuild.cs / asset_tripo.py / asset_texture.py / selftest_asset_tripo.py is in
git history (commit 2198c62)."""
import sys
sys.exit(__doc__)
