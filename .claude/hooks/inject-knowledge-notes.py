#!/usr/bin/env python3
"""UserPromptSubmit: point Claude to knowledge notes (past errors, setup records, decisions) related to the prompt.

The notes folder is set by KNOWLEDGE_DIR=<path> in .ai/knowledge.conf (not committed). Without it, nothing happens.
Extra keyword lines can be added in .ai/knowledge.map, one per line: <keyword regex>:<search terms separated by spaces>
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hooklib  # noqa: E402

# (keyword regex matched against the lower-cased prompt, search terms grepped in note contents)
KEYWORD_MAP = [
    (r"admob|광고|리워드|\brewarded\b|interstitial|mediation|\bssv\b", "ads ssv mediation"),
    (r"firebase|\bauth\b|로그인|\blogin\b|apple sign|\bgpgs\b|계정|\baccount\b|세션|\bsession\b|게스트|\bguest\b", "auth login session"),
    (r"\bconsent\b|\bgdpr\b|\bump\b|\bccpa\b|동의|개인정보", "consent gdpr"),
    (r"\batt\b|skadnetwork|어트리뷰션|attribution|심사|app review", "app-review attribution"),
    (r"\bwebgl\b|jslib|\bwasm\b|미니게임|minigame", "webgl"),
    (r"webhook|웹훅", "webhook"),
    (r"addressables|link\.xml|stripping|스트리핑", "addressables build"),
    (r"notification|알림|푸시|\bpush\b", "notifications"),
    (r"safearea|safe area|노치", "safearea"),
    (r"\biap\b|결제|구매|purchase|billing|영수증|receipt|환불|refund", "iap receipt"),
    (r"배포|\bdeploy|시크릿|\bsecret|prod 환경|dev 환경", "deploy secret"),
    (r"\bcsv\b|마스터데이터|\bsync\b|동기화", "csv sync"),
    (r"암호화|encrypt|payload|복호화|decrypt", "encryption payload"),
    (r"멱등|idempoten|중복 처리|재시도|\bretry", "idempotency"),
    (r"재화|\bgold\b|골드|currency|인벤토리|inventory", "currency inventory"),
    (r"브랜치|\bbranch|\bepic\b|머지|\bmerge|worktree|워크트리", "branch merge"),
]


def knowledge_dir():
    conf = os.path.join(hooklib.PROJECT_DIR, ".ai", "knowledge.conf")
    if not os.path.isfile(conf):
        return None
    with open(conf, encoding="utf-8") as f:
        for line in f:
            if line.startswith("KNOWLEDGE_DIR="):
                path = os.path.expanduser(line.split("=", 1)[1].strip())
                return path if os.path.isdir(path) else None
    return None


def main():
    prompt = hooklib.read_input().get("prompt") or ""
    root = knowledge_dir()
    if not prompt or not root:
        return
    table = list(KEYWORD_MAP)
    extra = os.path.join(hooklib.PROJECT_DIR, ".ai", "knowledge.map")
    if os.path.isfile(extra):
        with open(extra, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#") and ":" in line:
                    k, terms = line.rsplit(":", 1)
                    table.append((k, terms))
    lower = prompt.lower()
    terms = set()
    for pattern, words in table:
        try:
            if re.search(pattern, lower):
                terms.update(words.split())
        except re.error:
            continue
    if not terms:
        return
    rx = re.compile("|".join(re.escape(t) for t in sorted(terms)), re.I)
    hits = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if not d.startswith(".")]
        for name in filenames:
            if not name.endswith(".md"):
                continue
            path = os.path.join(dirpath, name)
            try:
                with open(path, encoding="utf-8", errors="ignore") as f:
                    if rx.search(name) or rx.search(f.read()):
                        hits.append(path)
            except OSError:
                continue
            if len(hits) >= 15:
                break
    if hits:
        hooklib.add_context("UserPromptSubmit",
                            "[knowledge] 이 작업과 관련된 과거 에러·셋업 기록이 있습니다. 구현 전에 아래 노트를 읽고, "
                            "각 노트의 원인과 교훈을 반영해 같은 문제를 반복하지 마세요:\n"
                            + "\n".join(f"- {h}" for h in sorted(hits)))


if __name__ == "__main__":
    main()
