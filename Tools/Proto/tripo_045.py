#!/usr/bin/env python3
"""Superseded (050, 2026-11-03): Tripo generation moved to the official asset pipeline with the same rules
(locks, resumable journal, cap + reservation, dry run of the frozen request, never resend on network exit, approvals).
Assets, budgets and approvals now live in docs/art/asset-catalog.json; the 045/046 journals are read from their old folders.
  /usr/bin/python3 Tools/Art/assets.py gen <freeze|dryrun|submit|fetch|status|reconcile> <asset>
  /usr/bin/python3 Tools/Art/assets.py gen anim <asset> <check|rig|retarget|attack|reset-<step>>
The last version of this file is in git history (commit 2184c85)."""
import sys
sys.exit(__doc__)
