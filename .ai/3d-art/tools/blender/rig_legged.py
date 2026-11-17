"""3dkit: a rig from data, for creatures with legs. rig_build.py runs this file for a .json shape; config is that JSON.

All positions are metres at game scale in Blender axes: +X front, +Y its left, +Z up, feet at z 0. Walks go +X.

  torso    [[name, head, tail], ...] from the pelvis up, the last one is the head. Parent chain root -> first -> ...
  legs     {side: {hip, knee, ankle, toe, radius, phase}}   side "R" may be {"mirror": "L"} (y flipped, phase + 0.5)
           optional thigh_radius, shin_radius replace radius for that part
  arms     {side: {shoulder, elbow, wrist, tip, radius}}    optional, same mirror. Parent: "arm_parent" (default the torso bone below the head)
           optional upperarm_radius, lowerarm_radius, hand_radius replace radius for that part (a thin shoulder, a big fist)
           optional walk: {arm_swing, arm_out, elbow, arm_forward} replaces the walk values for this arm (an arm that holds something)
  extras   [{name, head, tail, parent, above_z}]            rigid parts that follow late (a leaf, a pad): everything above above_z
  cape     a cloth that hangs from the body: bone chains that follow late, baked into every clip, no physics. All optional keys have defaults.
           parent, columns {name: [point, ... top to bottom]} (same number of points and heights in each; {"mirror": "L"} flips y),
           select {channel, over, min}: its vertices by albedo (that channel > over x each other channel and > min), top_z, blend: above top_z the
           cape stays rigid on parent (a collar), blend = metres over which it starts to move. lag_frames: each chain bone reacts this much later.
           speed_deg, fall_deg: degrees per m/s at the first free bone, away from the way the body moves. splay_deg: side columns open per m/s of fall.
           scale [per bone]: share of the motion per chain bone (a top fused to the back moves less). min_flare, max_flare: limits of the total pitch, so it
           does not enter the legs. walk_flare, walk_flutter, idle_flutter: degrees; in-place clips have no real speed, so a walk gives its flare here.
  torso_blend    metres over which two torso bones share vertices, default 0.06. A big body that bends at the waist needs more
  foot_below_z   a leg's vertices under this height stay rigid on the foot, so a leaning shin cannot push the sole into the ground
  walk     frames, distance, duty, lift, toe_deg, drop, bounce, roll, yaw, pitch, twist, head_steady, arm_swing, arm_out, elbow, sway
           distance = metres per loop (speed = distance * 30 / frames). duty = stance share of a foot's cycle. drop = pelvis
           lowered so the legs can bend. yaw = pelvis turn with the forward leg, twist = chest turn the other way. arm_swing =
           degrees, each arm opposite its leg. arm_forward = degrees the swing is centred ahead of the body. elbow = forward bend.
           sway = extras' lag. bounce_peak = where the pelvis is highest: "stance" (default, mid-stance: a walk) or "air" (both feet off the
           ground: a run, the pelvis is then lowest at mid-stance).
  idle     frames, drop, breath, sway_deg, arm_hang
  clips    {name: {frames, keys: [{t, bones: {bone: [rx, ry, rz]}, hips: [x, y, z], feet: {side: [x, y, z]}}]}}
           one-shot motions as key poses. Degrees relative to the parent, t from 0 to 1, smooth between keys, feet by IK. End on the
           pose you started from: a clip's last frame equals its first.
"""


def _sides(block):
    """expand {"R": {"mirror": "L"}}: y flipped, phase + 0.5"""
    out = {}
    for name, v in block.items():
        if 'mirror' in v:
            v = {k: ([p[0], -p[1], p[2]] if isinstance(p, list) else p) for k, p in block[v['mirror']].items()}
            if 'phase' in v:
                v['phase'] = (v['phase'] + 0.5) % 1.0
        out[name] = v
    return out


LEGS = _sides(config['legs'])
ARMS = _sides(config.get('arms') or {})
TORSO_NAMES = [t[0] for t in config['torso']]
ARM_PARENT = config.get('arm_parent') or (TORSO_NAMES[-2] if len(TORSO_NAMES) > 1 else TORSO_NAMES[-1])
EXTRAS = config.get('extras') or []
CAPE = config.get('cape')
CAPE_COLUMNS = {c: ([[p[0], -p[1], p[2]] for p in CAPE['columns'][v['mirror']]] if isinstance(v, dict) else v)
                for c, v in CAPE['columns'].items()} if CAPE else {}
JOINT_SOFTNESS = 0.15     # how far a vertex shares with the neighbour limb bone (share of its radius)
FOOT_BLEND = 0.02


def _radius(limb, part):
    return limb.get(part + '_radius', limb['radius'])


def _limb_bones():
    """limb segments that get capsule weights: name, radius, side sign of y, side sign of x (four-legged corners only)"""
    four = len(LEGS) > 2
    out = []
    for s, v in LEGS.items():
        sx = (1 if v['hip'][0] > 0 else -1) if four else 0
        sy = 1 if v['hip'][1] > 0 else -1
        out += [(f'thigh_{s}', _radius(v, 'thigh'), sy, sx), (f'shin_{s}', _radius(v, 'shin'), sy, sx)]
    for s, v in ARMS.items():
        sy = 1 if v['shoulder'][1] > 0 else -1
        out += [(f'{part}_{s}', _radius(v, part), sy, 0) for part in ('upperarm', 'lowerarm', 'hand')]
    return out


def define():
    bones['root'] = ((0, 0, 0), (0, 0, 0.06), None, False)
    parent = 'root'
    for k, (name, head, tail) in enumerate(config['torso']):
        bones[name] = (tuple(head), tuple(tail), parent, k > 0)
        parent = name
    for s, v in LEGS.items():
        bones[f'thigh_{s}'] = (tuple(v['hip']), tuple(v['knee']), TORSO_NAMES[0], False)
        bones[f'shin_{s}'] = (tuple(v['knee']), tuple(v['ankle']), f'thigh_{s}', True)
        bones[f'foot_{s}'] = (tuple(v['ankle']), tuple(v['toe']), f'shin_{s}', True)
    for s, v in ARMS.items():
        bones[f'upperarm_{s}'] = (tuple(v['shoulder']), tuple(v['elbow']), ARM_PARENT, False)
        bones[f'lowerarm_{s}'] = (tuple(v['elbow']), tuple(v['wrist']), f'upperarm_{s}', True)
        bones[f'hand_{s}'] = (tuple(v['wrist']), tuple(v['tip']), f'lowerarm_{s}', True)
    for e in EXTRAS:
        bones[e['name']] = (tuple(e['head']), tuple(e['tail']), e['parent'], False)
    cape_bones = []
    for c, pts in CAPE_COLUMNS.items():
        for k, (a, b) in enumerate(zip(pts, pts[1:])):
            bones[f'cape_{c}_{k}'] = (tuple(a), tuple(b), f'cape_{c}_{k - 1}' if k else CAPE['parent'], k > 0)
            cape_bones.append(f'cape_{c}_{k}')

    # limbs: nearest capsule (distance / radius), sharing only with chain neighbours; torso takes what no capsule reaches
    limb = _limb_bones()
    names = [b[0] for b in limb]
    score = np.stack([seg_dist(g, bones[b][0], bones[b][1]) / r for b, r, _, _ in limb], 1)
    for j, (_, _, sy, sx) in enumerate(limb):
        wrong = (np.sign(y) != sy) | ((np.sign(x) != sx) if sx else False)
        score[wrong, j] = 1e6
    prim = score.argmin(1)
    smin = score.min(1)
    neigh = {b: {b, bones[b][2]} | {k for k in names if bones[k][2] == b} for b in names}
    share = np.zeros((N_V, len(names)))
    for j, b in enumerate(names):
        ok = np.array([b in neigh[names[p]] for p in prim])
        share[:, j] = np.where(ok, np.exp(-(score[:, j] - smin) / JOINT_SOFTNESS), 0.0)
    share /= share.sum(1, keepdims=True)
    inside = 1 - sstep(smin, 1.0, 1.2)
    cape_w = np.zeros(N_V)
    if CAPE:
        sel = CAPE['select']
        ch, others = col[:, sel['channel']], np.delete(col, sel['channel'], 1)
        is_cape = (ch > sel['over'] * others.max(1)) & (ch > sel['min'])
        inside = inside * (~is_cape)                     # an arm next to the cloth does not take its vertices
        cape_w = is_cape * (1 - sstep(z, CAPE['top_z'] - CAPE.get('blend', 0.2), CAPE['top_z']))
        info['cape_vertices'] = int(is_cape.sum())

    # optional arm 'zone': {"outer_y": m, "z": [low, high]}. On a fused mesh the inner side of a hanging arm falls outside its capsule and stays on the
    # torso, so lifting the arm stretches it into a blade. Everything that far out on the arm's side and in that height range belongs to the arm.
    for v in ARMS.values():
        zone = v.get('zone')
        if zone:
            sy = 1 if v['shoulder'][1] > 0 else -1
            in_zone = (np.sign(y) == sy) & (np.abs(y) >= zone['outer_y']) & (z >= zone['z'][0]) & (z <= zone['z'][1])
            if CAPE:
                in_zone = in_zone & ~is_cape
            inside = np.where(in_zone, 1.0, inside)

    # foot blocks stay rigid: everything this low on a leg's side belongs to that foot
    below = 1 - sstep(z, config['foot_below_z'] - FOOT_BLEND / 2, config['foot_below_z'] + FOOT_BLEND / 2)
    limbsum = np.zeros(N_V)
    for j, (b, _, _, _) in enumerate(limb):
        W8[b] = inside * share[:, j]
    foot_total = {s: np.zeros(N_V) for s in LEGS}
    for s, v in LEGS.items():
        d = np.hypot(x - v['ankle'][0], y - v['ankle'][1])
        others = [np.hypot(x - o['ankle'][0], y - o['ankle'][1]) for k, o in LEGS.items() if k != s]
        nearest = np.all([d <= q for q in others], axis=0) if others else np.ones(N_V, bool)
        foot_total[s] = below * nearest
        for b in (f'thigh_{s}', f'shin_{s}'):
            W8[b] = W8[b] * (1 - foot_total[s])
        W8[f'foot_{s}'] = foot_total[s]
        limbsum += foot_total[s] + W8[f'thigh_{s}'] + W8[f'shin_{s}']
    for s in ARMS:
        limbsum += W8[f'upperarm_{s}'] + W8[f'lowerarm_{s}'] + W8[f'hand_{s}']
    limbsum = np.minimum(limbsum, 1.0)

    # extras above their height, the rest splits along the torso by height
    extra_sum = np.zeros(N_V)
    for e in EXTRAS:
        w = (1 - limbsum) * sstep(z, e['above_z'] - 0.005, e['above_z'] + 0.005)
        W8[e['name']] = w
        extra_sum += w
    cuts = [t[1][2] for t in config['torso'][1:]]
    parts = split(z, cuts, config.get('torso_blend', 0.06)) if cuts else [np.ones(N_V)]
    rest = 1 - limbsum - extra_sum - cape_w
    if CAPE:
        cols = sorted(CAPE_COLUMNS, key=lambda c: CAPE_COLUMNS[c][0][1])         # low y to high y
        ys = np.array([CAPE_COLUMNS[c][-1][1] for c in cols])
        first = CAPE_COLUMNS[cols[0]]
        chain = split(z, [p[2] for p in first[-2:0:-1]], 0.12)[::-1]            # split runs low to high; the chain runs top to bottom
        for j, c in enumerate(cols):
            col_w = np.interp(y, ys, np.eye(len(cols))[j]) if len(cols) > 1 else np.ones(N_V)
            for k, part in enumerate(chain):
                W8[f'cape_{c}_{k}'] = cape_w * col_w * part
    for t, p in zip(config['torso'], parts):
        W8[t[0]] = rest * p
    return dict(torso=TORSO_NAMES, accessory=[e['name'] for e in EXTRAS] + cape_bones, mesh_name=config.get('mesh_name'))


def _euler(r):
    return qaxis((0, 0, 1), r[2]) @ qaxis((1, 0, 0), r[0]) @ qaxis((0, 1, 0), r[1])


def _swing_axis(s):
    """axis that turns this arm's rest direction toward the back (-X)"""
    v = Vector(ARMS[s]['wrist']) - Vector(ARMS[s]['shoulder'])
    a = v.cross(Vector((-1, 0, 0)))
    return a.normalized() if a.length > 1e-6 else Vector((0, 1, 0))


def _pose(b, D):
    set_world_delta(b, D)
    upd()


def motions():
    POLE = {s: rest_pole(f'thigh_{s}', f'shin_{s}') for s in LEGS}
    ANKLE0 = {s: rest_tail(f'shin_{s}') for s in LEGS}
    ARM_AXIS = {s: _swing_axis(s) for s in ARMS}
    n_torso = len(TORSO_NAMES)

    def legs(target_fn):
        err = 0.0
        for s in LEGS:
            tgt, fq = target_fn(s)
            err = max(err, solve_leg(f'thigh_{s}', f'shin_{s}', tgt, POLE[s], f'foot_{s}', fq))
        return err

    def extras_follow(a, b):
        for e in EXTRAS:
            _pose(e['name'], qaxis((1, 0, 0), a) @ qaxis((0, 1, 0), b))

    w = config['walk']
    N, D, BETA, LIFT = w['frames'], w['distance'], w['duty'], w['lift']
    STANCE = D * BETA
    PEAKS = {'stance': BETA / 2, 'air': (BETA + 0.5) / 2}
    if w.get('bounce_peak', 'stance') not in PEAKS:
        raise SystemExit(f"BAD_BOUNCE_PEAK:{w['bounce_peak']} (stance or air)")
    PEAK = PEAKS[w.get('bounce_peak', 'stance')]

    def foot(s, t):
        p = (t - LEGS[s]['phase']) % 1.0
        a0 = ANKLE0[s]
        if p < BETA:
            return Vector((a0.x + STANCE / 2 - STANCE * (p / BETA), a0.y, a0.z)), Quaternion()
        q = (p - BETA) / (1 - BETA)
        dx = -STANCE / 2 + STANCE * (1 - math.cos(math.pi * q)) / 2
        return Vector((a0.x + dx, a0.y, a0.z + LIFT * math.sin(math.pi * q))), qaxis((0, 1, 0), -w.get('toe_deg', 0) * math.sin(math.pi * q))

    def arms_walk(t, bounce):
        Dp = wd(ARM_PARENT)
        for s, v in ARMS.items():
            sg = 1 if v['shoulder'][1] > 0 else -1
            own = {**w, **v.get('walk', {})}
            back = own.get('arm_swing', 0) * sg * math.cos(TAU * t) - own.get('arm_forward', 0)         # at t 0 the left leg is forward: the left arm goes back
            out = sg * (own.get('arm_out', 0) + 1.5 * bounce)
            ax = ARM_AXIS[s]
            base = Dp @ qaxis((1, 0, 0), out)
            elbow = own.get('elbow', 0)
            _pose(f'upperarm_{s}', base @ Quaternion(ax, math.radians(back)))
            _pose(f'lowerarm_{s}', base @ Quaternion(ax, math.radians(back * 1.1 - elbow)))
            _pose(f'hand_{s}', base @ Quaternion(ax, math.radians(back * 1.2 - elbow)))

    def walk(t):
        bounce = math.cos(TAU * 2 * (t - PEAK))         # default: high at mid-stance, low at the hand-over
        roll = -w.get('roll', 0) * math.sin(TAU * (t + 0.05))
        yaw = -w.get('yaw', 0) * math.cos(TAU * t)
        pitch = w.get('pitch', 0)
        set_world_offset(TORSO_NAMES[0], (0, 0, w['drop'] + w.get('bounce', 0) * bounce))
        for i, b in enumerate(TORSO_NAMES):
            u = i / max(n_torso - 1, 1)                 # 0 pelvis .. 1 head
            is_head = i == n_torso - 1 and n_torso > 1
            turn = yaw * (1 - u) + (w.get('twist', 0) * math.cos(TAU * t)) * u
            lean = roll * (1 - 0.5 * u)
            if is_head:
                turn, lean = turn * (1 - w.get('head_steady', 0)), lean * (1 - w.get('head_steady', 0))
            _pose(b, qaxis((0, 0, 1), turn) @ qaxis((1, 0, 0), lean) @ qaxis((0, 1, 0), pitch + (1.5 * bounce if u > 0 else 0)))
        extras_follow(roll * 0.3 + w.get('sway', 0) * math.sin(TAU * (t + 0.05) - 0.9), pitch + w.get('sway', 0) * 0.8 * math.cos(TAU * 2 * (t - PEAK) - 1.0))
        arms_walk(t, bounce)
        return legs(lambda s: foot(s, t))

    i_ = config['idle']

    def idle(t):
        b = math.sin(TAU * t)
        sway = i_.get('sway_deg', 0) * math.sin(TAU * t + 0.6)
        set_world_offset(TORSO_NAMES[0], (0, 0, i_['drop'] - 0.004 * (1 - b) / 2))
        for k, name in enumerate(TORSO_NAMES):
            is_chest = name == ARM_PARENT
            if is_chest:
                s_ = 1.0 + i_.get('breath', 0) * (1 + b) / 2
                arm.pose.bones[name].scale = Vector((s_, s_, s_))
            _pose(name, qaxis((1, 0, 0), sway) @ qaxis((0, 1, 0), 1.0 * b if k else 0.0))
        extras_follow(i_.get('sway_deg', 0) * 3 * math.sin(TAU * t - 0.9), i_.get('sway_deg', 0) * 1.5 * math.sin(TAU * 2 * t - 1.3))
        Dp = wd(ARM_PARENT)
        for s, v in ARMS.items():
            sg = 1 if v['shoulder'][1] > 0 else -1
            ang = i_.get('arm_hang', 0) * math.sin(TAU * t + (0 if sg > 0 else 1.0))
            for b_ in (f'upperarm_{s}', f'lowerarm_{s}', f'hand_{s}'):
                _pose(b_, Dp @ Quaternion(ARM_AXIS[s], math.radians(ang)))
        return legs(lambda s: (ANKLE0[s], Quaternion()))

    def clip_pose(keys):
        keys = sorted(keys, key=lambda k: k['t'])

        def channel(t, read, zero):
            """value of one channel at t: smooth between the two keys around t; a key that does not name it holds zero"""
            if t <= keys[0]['t']:
                return read(keys[0]) or zero
            for k0, k1 in zip(keys, keys[1:]):
                if t <= k1['t']:
                    u = (t - k0['t']) / max(k1['t'] - k0['t'], 1e-6)
                    u = u * u * (3 - 2 * u)
                    a, b = read(k0) or zero, read(k1) or zero
                    return [p + (q - p) * u for p, q in zip(a, b)]
            return read(keys[-1]) or zero

        def pose(t):
            set_world_offset(TORSO_NAMES[0], tuple(channel(t, lambda k: k.get('hips'), [0, 0, 0])))
            for b in bones:      # parents first: bones was filled top down
                if b == 'root' or b.startswith(('thigh_', 'shin_', 'foot_')):
                    continue
                r = channel(t, lambda k: (k.get('bones') or {}).get(b), [0, 0, 0])
                parent = bones[b][2]
                _pose(b, wd(parent) @ _euler(r))

            def target(s):
                off = channel(t, lambda k: (k.get('feet') or {}).get(s), [0, 0, 0])
                return ANKLE0[s] + Vector(off), Quaternion()
            return legs(target)
        return pose

    def with_cape(body_pose, frames, loop, flare=0.0, flutter=0.0, cycles=1):
        """the clip's pose plus the cape: first play the body once to read how its parent bone moves, then every chain bone takes that motion
        later than the bone above it. A one-shot lets the lag fade at its end, so the last frame equals the first."""
        if not CAPE:
            return body_pose
        par = CAPE['parent']
        trace = []
        for f in range(frames):
            reset_pose(); upd()
            body_pose(f / frames); upd()
            trace.append((arm.pose.bones[par].head.copy(), wd(par)))
        lag = CAPE.get('lag_frames', 2)
        fade = CAPE.get('fade_frames', 8)
        scale = CAPE.get('scale') or []

        def at(i):
            return i % frames if loop else max(i, 0)

        def velocity(i):
            if not loop and i <= 0:
                return Vector()
            return (trace[at(i)][0] - trace[at(i - 1)][0]) * FPS

        def pose(t):
            err = body_pose(t); upd()
            f = int(round(t * frames)) % frames
            env = 1.0 if loop else min(1.0, (frames - f) / max(fade, 1))
            for c, pts in CAPE_COLUMNS.items():
                out = 0 if abs(pts[-1][1]) < 1e-6 else (1 if pts[-1][1] > 0 else -1)
                pitch, roll = flare, 0.0
                for k in range(len(pts) - 1):
                    i = f - lag * (k + 1)
                    v = velocity(i)
                    share = scale[k] if k < len(scale) else 1.0
                    pitch += share * env * (CAPE.get('speed_deg', 0) * v.x - CAPE.get('fall_deg', 0) * v.z
                                            + flutter * math.sin(TAU * (cycles * t - 0.12 * k)))
                    roll += share * env * (-CAPE.get('speed_deg', 0) * v.y + out * CAPE.get('splay_deg', 0) * max(0.0, -v.z))
                    total = max(CAPE.get('min_flare', -5.0), min(CAPE.get('max_flare', 70.0), pitch))
                    late = trace[f][1].slerp(trace[at(i)][1], env)
                    _pose(f'cape_{c}_{k}', late @ qaxis((1, 0, 0), roll) @ qaxis((0, 1, 0), total))
            return err
        return pose

    acts = [make_action('idle', i_['frames'], with_cape(idle, i_['frames'], True, flutter=CAPE.get('idle_flutter', 0) if CAPE else 0)),
            make_action('walk', N, with_cape(walk, N, True, CAPE.get('walk_flare', 0) if CAPE else 0, CAPE.get('walk_flutter', 0) if CAPE else 0, 2))]
    for name, c in (config.get('clips') or {}).items():
        acts.append(make_action(name, c['frames'], with_cape(clip_pose(c['keys']), c['frames'], False)))
    info['walk_design'] = dict(frames=N, distance_per_loop_m=D, speed_mps=round(D * FPS / N, 4), duty=BETA, stance_travel_m=STANCE, lift_m=LIFT, axis='+X (front)')
    return acts
