#!/usr/bin/env python3
"""Superseded (050, then 051): Tripo generation lives in the 3dkit asset pipeline with the same rules (locks, resumable
journal, cap + reservation, dry run of the frozen request, never resend on network exit, approvals; project anim steps
such as attack need their own budget step approval). Assets, budgets and approvals: docs/art/asset-catalog.json.
  /usr/bin/python3 3dkit/tools/assets.py gen <freeze|dryrun|submit|fetch|status|reconcile> <asset>
  /usr/bin/python3 3dkit/tools/assets.py gen anim <asset> <check|rig|retarget|attack|reset-<step>>
The 045 version of this file is in git history (commit 2184c85)."""
import sys
sys.exit(__doc__)
