"""3dkit: one shape for every refusal a tool makes, so an agent can act on the message alone.

    CODE:detail -> next: what to do now

CODE is stable (tests, replay cases and docs match on it); detail names the thing; next is the action that fixes it or the
person to ask. code_of() strips the hint for comparisons."""


def fail(code, detail='', next_action=''):
    msg = code + (f':{detail}' if detail != '' else '')
    if next_action:
        msg += f' -> next: {next_action}'
    raise SystemExit(msg)


def code_of(msg):
    """'CODE:detail -> next: ...' -> 'CODE:detail' (what tests compare)"""
    return str(msg).split(' -> next:')[0]


def need(args, n, usage):
    """positional arguments of a sub-command: refuse with the usage line instead of an IndexError"""
    if len(args) < n:
        fail('USAGE', usage, f'give {n} argument(s); got {len(args)}')
    return args[:n]
