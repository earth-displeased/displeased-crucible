import sys, datetime
from analyze import load, effects_dmg
p=sys.argv[1]; start=datetime.datetime.strptime(sys.argv[2],'%Y-%m-%d %H:%M:%S'); end=datetime.datetime.strptime(sys.argv[3],'%Y-%m-%d %H:%M:%S')
ops, actors, player = load(p)
pets={i for i,a in actors.items() if a['type']==0x202}
boss={i for i,a in actors.items() if a['oid'] in (0x4CD8,0x4CD9)}
stidx={}
hp={}
for t,tag,o in ops:
    if t is None or t<start or t>end: 
        if tag=='HP  ': hp[o['id']]=(o['hp'],o['maxhp'])
        continue
    ts=t.strftime('%M:%S.%f')[:-4]
    if tag=='TARG' and o['id'] in boss:
        tg='PLAYER' if o['target']==player else (actors[o['target']]['name'] if o['target'] in actors else hex(o['target']))
        print(ts, actors[o['id']]['name'],'targets',tg)
    elif tag=='ACT+' and o['id'] in pets: print(ts,'pet summoned',o['name'],f"{o['hp']}/{o['maxhp']}")
    elif tag=='ACT-' and o['id'] in pets: print(ts,'pet gone',actors[o['id']]['name'], hp.get(o['id']))
    elif tag=='HP  ' and o['id'] in pets:
        hp[o['id']]=(o['hp'],o['maxhp'])
    elif tag=='STA+' and o['id']==player and o['st']['id'] in (2413,): stidx[o['idx']]=2413; print(ts,'SNARL on (player status 2413)')
    elif tag=='STA-' and o['id']==player and stidx.get(o['idx'])==2413: stidx.pop(o['idx']); print(ts,'SNARL off')
    elif tag=='CLAR' and (o['action']&0xffffff) in (46750,46751,47018,47238): print(ts,'request',o['action']&0xffffff)
    elif tag=='CST!' and o['id'] in pets:
        a=o['action']&0xffffff
        d=sum(effects_dmg(e)[0] for _,e in o['targets'])
        if a not in (7,8): print(ts,'  pet',actors[o['id']]['name'],'action',a,'dmg',d)
