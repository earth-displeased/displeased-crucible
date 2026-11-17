import sys, collections, math
from analyze import load, effects_dmg, BORGNY_AID, PLAYER_NAME

path = sys.argv[1]
ops, actors, player = load(path)
pets = {i for i, a in actors.items() if a['type'] == 0x202}
BORGNY_OID = 0x4CD8
start = next(t for t, tag, o in ops if tag == 'ACT+' and o['oid'] == BORGNY_OID)
print('Borgny spawn', start)

hp = {}
pos = {}
maxhp = {}
dmg_by = collections.Counter(); hits_by = collections.Counter()
pet_dmg_by = collections.Counter()
timeline = []
last = None
oid = {i: a['oid'] for i, a in actors.items()}
for t, tag, o in ops:
    if t is None or t < start:
        if tag == 'ACT+': maxhp[o['id']] = o['maxhp']; hp[o['id']] = o['hp']; pos[o['id']] = o['pos']
        if tag == 'HP  ': hp[o['id']] = o['hp']; maxhp[o['id']] = o['maxhp']
        if tag == 'MOVE': pos[o['id']] = o['pos']
        continue
    last = t
    ts = t.strftime('%H:%M:%S.%f')[:-3]
    if tag == 'ACT+':
        maxhp[o['id']] = o['maxhp']; hp[o['id']] = o['hp']; pos[o['id']] = o['pos']; oid[o['id']] = o['oid']
        if o['oid'] in (0x4CD9,):
            timeline.append((ts, f"TOXIC MASS spawn at {o['pos'][0]:.1f},{o['pos'][2]:.1f}"))
    elif tag == 'MOVE': pos[o['id']] = o['pos']
    elif tag == 'HP  ':
        hp[o['id']] = o['hp']; maxhp[o['id']] = o['maxhp']
    elif tag == 'DIE+':
        if o['id'] == player: timeline.append((ts, '*** PLAYER DEAD'))
        elif oid.get(o['id']) == 0x4CD9: timeline.append((ts, 'Toxic Mass died'))
        elif o['id'] in pets: timeline.append((ts, f"pet {actors[o['id']]['name']} dead/despawn flag"))
    elif tag == 'DIE-' and o['id'] == player: timeline.append((ts, '*** player revived'))
    elif tag == 'CST+':
        a = o['action'] & 0xFFFFFF
        if a in BORGNY_AID:
            timeline.append((ts, f"cast {BORGNY_AID[a]} ({o['total']:.1f}s) target={'PLAYER' if o['target']==player else ('pet' if o['target'] in pets else hex(o['target']))}"))
    elif tag == 'ICON':
        timeline.append((ts, f"icon {o['icon']} on {'PLAYER' if o['id']==player else hex(o['id'])}"))
    elif tag == 'TETH':
        timeline.append((ts, f"tether {o['tid']} {hex(o['id'])}->{'PLAYER' if o['target']==player else hex(o['target'])}"))
    elif tag == 'STA+' and o['id'] == player:
        timeline.append((ts, f"player status +{o['st']['id']} extra={o['st']['extra']}"))
    elif tag == 'CLAR':
        a = o['action']; typ = a >> 24; i = a & 0xFFFFFF
        timeline.append((ts, f"  player request type{typ} id{i} -> {'self' if o['target']==player else hex(o['target'])}"))
    elif tag == 'INVT':
        timeline.append((ts, f"  inventory item {o['item']} qty {o['qty']}"))
    elif tag == 'CPET':
        timeline.append((ts, f"  active pet {hex(o['id'])} {o['a']} {o['b']}"))
    elif tag == 'CST!':
        a = o['action'] & 0xFFFFFF
        for tid, effs in o['targets']:
            d, h = effects_dmg(effs)
            src = actors.get(o['id'], {}).get('name', hex(o['id']))
            if tid == player and d:
                key = (src, BORGNY_AID.get(a, str(a)))
                dmg_by[key] += d; hits_by[key] += 1
                p = pos.get(player, (0, 0, 0))
                timeline.append((ts, f"  HIT {key[1]} from {src}: {d}  (player hp before {hp.get(player)}/{maxhp.get(player)}) at {p[0]:.1f},{p[2]:.1f}"))
            elif tid == player and h:
                timeline.append((ts, f"  heal {h} via {a} from {src}"))
            elif tid in pets and d:
                pet_dmg_by[(actors[tid]['name'], BORGNY_AID.get(a, str(a)))] += d

print('\nDamage to player by source/action:')
for k, v in dmg_by.most_common():
    print(f"  {v:7d} ({hits_by[k]} hits)  {k}")
print('Damage to pets:')
for k, v in pet_dmg_by.most_common(12): print(f"  {v:7d} {k}")
if '-t' in sys.argv:
    print('\nTimeline:')
    for ts, s in timeline: print(ts, s)
