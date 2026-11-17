import sys, collections, math
PLAYER_NAME = 'Tarron Berrick'
from bmrreplay import parse

BORGNY_AID = {49680: 'AutoAttack', 48806: 'Teleport', 48807: 'ToxicBreathBoss', 48808: 'ToxicBreath', 48809: 'ToxicVomitBoss', 48810: 'ToxicVomit',
              48813: 'CauterizeBoss', 48814: 'Cauterize', 48815: 'TouchdownBoss', 48816: 'TouchdownKnockback', 48828: 'TouchdownAOE',
              48811: 'FumingVomitBoss', 48812: 'FumingVomit', 48817: 'WrigglingPhlegmBoss', 48818: 'WrigglingPhlegmTeleport', 48819: 'WrigglingPhlegm',
              48821: 'NoxiousExplosion', 50543: 'Unknown(moving aoe?)', 48822: 'SalivousSnap'}


def aid(a):
    t = a >> 24; i = a & 0xFFFFFF
    return t, i


def effects_dmg(effs):
    """returns (damage, heal) from ActionEffects list"""
    dmg = heal = 0
    for e in effs:
        typ = e & 0xFF
        if typ == 0: continue
        p3 = (e >> 32) & 0xFF; flags = (e >> 40) & 0xFF; val = (e >> 48) & 0xFFFF
        if flags & 0x40: val += p3 << 16
        if typ in (3, 5, 6): dmg += val
        elif typ == 4: heal += val
    return dmg, heal


def load(path):
    ops = parse(path)
    actors = {}
    player = None
    for t, tag, o in ops:
        if tag == 'ACT+':
            actors[o['id']] = o
            if o['type'] == 0x104 and o['name'] == PLAYER_NAME: player = o['id']
    return ops, actors, player


def name(actors, i):
    a = actors.get(i)
    return f"{a['name']}({a['oid']:X})" if a else f"{i:X}"


if __name__ == '__main__':
    path = sys.argv[1]
    ops, actors, player = load(path)
    print('player', name(actors, player))
    # list combat segments by tracked big enemies (create of named Piece / Borgny)
    for t, tag, o in ops:
        if tag == 'ACT+' and o['type'] == 2 and o['maxhp'] > 20000:
            print(t.strftime('%H:%M:%S'), 'spawn', o['name'], hex(o['oid']), o['maxhp'])
        if tag == 'DIE+' and o['id'] == player:
            print(t.strftime('%H:%M:%S.%f')[:-3], 'PLAYER DEAD')
        if tag == 'DIE-' and o['id'] == player:
            print(t.strftime('%H:%M:%S.%f')[:-3], 'player revived')
