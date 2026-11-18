"""3dkit: one user of a project's Unity Editor at a time (W4, W23).

An OS file lock (flock; msvcrt on Windows) on output/hook-state/unity-lock.flock. The operating system releases it when the
holder exits, crashed or not, so there is no stale lock to take over and no window where two runs both think they hold it
(B46: the old directory lock could be deleted between mkdir and writing owner.json). The owner line in the file is only
for the message. Nothing waits forever: a held lock is an error that names the owner, so the caller can report it."""
import json
import os
import time
from pathlib import Path

try:
    import fcntl

    def _try(fd):
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            return True
        except BlockingIOError:
            return False

    def _release(fd):
        fcntl.flock(fd, fcntl.LOCK_UN)
except ImportError:            # Windows
    import msvcrt

    def _try(fd):
        try:
            os.lseek(fd, 0, 0)
            msvcrt.locking(fd, msvcrt.LK_NBLCK, 1)
            return True
        except OSError:
            return False

    def _release(fd):
        os.lseek(fd, 0, 0)
        msvcrt.locking(fd, msvcrt.LK_UNLCK, 1)


class Locked(Exception):
    pass


def lock_path(root):
    return Path(root) / 'output/hook-state/unity-lock.flock'


def owner(root):
    """who holds it, as written by the holder (may be empty while it is being taken)"""
    try:
        return json.loads(lock_path(root).read_text() or '{}')
    except (OSError, ValueError):
        return {}


class EditorLock:
    def __init__(self, root, what, wait=0):
        self.path = lock_path(root)
        self.what, self.wait, self.fd = what, wait, None

    def __enter__(self):
        # nested use in one process (play_check -> kitlib.unity) shares the outer lock
        if os.environ.get('KIT_EDITOR_LOCK') == str(self.path):
            return self
        self.path.parent.mkdir(parents=True, exist_ok=True)
        fd = os.open(self.path, os.O_RDWR | os.O_CREAT, 0o644)
        end = time.time() + self.wait
        while not _try(fd):
            if time.time() >= end:
                os.close(fd)
                o = owner(self.path.parents[2])
                raise Locked(f"EDITOR_LOCKED by pid {o.get('pid', '?')} ({o.get('what', 'being taken')}, since {o.get('at', '?')}) "
                             '-> next: wait for that run to finish (the lock frees itself when it exits), then run again')
            time.sleep(0.5)
        os.ftruncate(fd, 0)
        os.lseek(fd, 0, 0)
        os.write(fd, json.dumps(dict(pid=os.getpid(), what=self.what, at=time.strftime('%F %T')), ensure_ascii=False).encode())
        self.fd = fd
        os.environ['KIT_EDITOR_LOCK'] = str(self.path)
        return self

    def __exit__(self, *exc):
        if self.fd is not None:
            try:
                os.ftruncate(self.fd, 0)
                _release(self.fd)
            finally:
                os.close(self.fd)
                self.fd = None
                os.environ.pop('KIT_EDITOR_LOCK', None)


def selftest():
    """two holders in one process conflict (separate open file descriptions), release frees it, a dead owner line is ignored"""
    import tempfile
    import shutil
    d = Path(tempfile.mkdtemp(prefix='editor-lock-'))
    try:
        os.environ.pop('KIT_EDITOR_LOCK', None)
        a = EditorLock(d, 'first').__enter__()
        os.environ.pop('KIT_EDITOR_LOCK', None)      # pretend to be another process
        try:
            EditorLock(d, 'second').__enter__()
            got = 'taken twice'
        except Locked as e:
            got = 'first' in str(e)
        os.environ['KIT_EDITOR_LOCK'] = str(a.path)
        a.__exit__(None, None, None)
        b = EditorLock(d, 'after release').__enter__(); b.__exit__(None, None, None)
        lock_path(d).write_text(json.dumps(dict(pid=999999, what='dead', at='then')))
        c = EditorLock(d, 'over a dead owner line').__enter__(); c.__exit__(None, None, None)
        assert got is True, got
        return 'editor_lock OK (conflict named, release, dead owner line ignored)'
    finally:
        os.environ.pop('KIT_EDITOR_LOCK', None)
        shutil.rmtree(d, ignore_errors=True)
