import sys, collections
from analyze import load, effects_dmg
p=sys.argv[1]
ops, actors, player = load(p)
pets={i for i,a in actors.items() if a['type']==0x202}
bosses={i:a for i,a in actors.items() if a['type']==0x205 and a['maxhp']>30000}
# fight windows by boss spawn->last hp
win=[]
for i,a in bosses.items():
    ts=[t for t,tag,o in ops if t and tag in('HP  ','ACT+','ACT-') and o['id']==i]
    win.append((min(ts),max(ts),a['name'],i))
win.sort()
name=lambda i: actors[i]['name'] if i in actors else hex(i)
for s,e,n,bid in win:
    dmg_src=collections.Counter(); pet_taken=collections.Counter(); pet_heal=collections.Counter(); ply_taken=0; ply_heal=0
    pet_first={}; pet_last={}; items=[]
    for t,tag,o in ops:
        if not t or t<s or t>e: continue
        if tag=='CST!':
            for tid,effs in o['targets']:
                d,h=effects_dmg(effs)
                src=o['id']; srcn='player' if src==player else (name(src) if src in pets else 'enemy')
                if actors.get(tid,{}).get('type')==0x205 and srcn!='enemy': dmg_src[srcn]+=d
                if tid in pets: pet_taken[name(tid)]+=d; pet_heal[name(tid)]+=h
                if tid==player: ply_taken+=d; ply_heal+=h
        if tag=='HP  ' and o['id'] in pets:
            n2=name(o['id']); pet_first.setdefault(n2,(o['hp'],o['maxhp'])); pet_last[n2]=(o['hp'],o['maxhp'])
        if tag=='ACT+' and o['id'] in pets:
            pet_first.setdefault(o['name'],(o['hp'],o['maxhp']))
        if tag=='INVT': items.append((o['item'],o['qty']))
    dur=(e-s).total_seconds()
    tot=sum(dmg_src.values())
    print(f"== {n} {s:%H:%M:%S}-{e:%H:%M:%S} ({dur:.0f}s) dmg dealt {tot} ({tot/dur:.0f}/s): "+', '.join(f'{k} {v}' for k,v in dmg_src.most_common()))
    print(f"   player took {ply_taken}, healed {ply_heal}; pets took {dict(pet_taken)}")
    print(f"   pet HP first seen {pet_first}  last {pet_last}")
