"""Cross-table validation for typed body sockets and reusable joint-pair motions."""
def validate_body(tables):
    names=('BodyParts','BodyPorts','BodyNodes','BodyMotions')
    if not any(n in tables for n in names): return
    if not all(n in tables for n in names): raise ValueError('All four body tables are required')
    def records(name): return [dict(zip(tables[name].fields,row)) for row in tables[name].rows]
    parts={r['Id']:r for r in records('BodyParts')}; ports={}
    for p in parts.values():
        if min(p['Id'],p['Role'],p['ArtId'],p['Width'],p['Height'])<=0: raise ValueError('Invalid body part')
    for p in records('BodyPorts'):
        key=(p['Part'],p['Port'])
        if p['Part'] not in parts or key in ports or p['Port']<0 or min(p['Type'],p['Accepts'])<=0: raise ValueError('Invalid/duplicate body port')
        ports[key]=p
    nodes={r['Id']:r for r in records('BodyNodes')}; occupied=set(); owners={}
    for n in nodes.values():
        if n['Part'] not in parts or n['Group']<0 or n['Id']<0: raise ValueError('Invalid body node')
        owners.setdefault(n['Owner'],[]).append(n)
        if n['Parent']<0: continue
        parent=nodes.get(n['Parent'])
        if parent is None or parent['Owner']!=n['Owner']: raise ValueError('Missing/cross-owner body parent')
        a=ports.get((parent['Part'],n['ParentPort'])); b=ports.get((n['Part'],n['Port']))
        if a is None or b is None or a['Type']!=b['Accepts'] or b['Type']!=a['Accepts']: raise ValueError('Incompatible body sockets')
        for endpoint in ((parent['Id'],n['ParentPort']),(n['Id'],n['Port'])):
            if endpoint in occupied: raise ValueError('Body socket occupied twice')
            occupied.add(endpoint)
        seen={n['Id']}; cursor=parent
        while cursor is not None:
            if cursor['Id'] in seen: raise ValueError('Body assembly cycle')
            seen.add(cursor['Id']); cursor=nodes.get(cursor['Parent'])
    for owner,items in owners.items():
        if len(items)>512 or sum(n['Parent']<0 for n in items)!=1: raise ValueError('Body assembly requires one root and <=512 nodes')
        if len({n['Bone'] for n in items})!=len(items): raise ValueError('Bone names must be unique per actor')
    keys=set()
    for m in records('BodyMotions'):
        key=(m['Motion'],m['ParentRole'],m['ChildRole'])
        if key in keys or not 0<=m['Motion']<=9 or not .01<=m['Duration']<=30 or not 0<m['Peak']<1: raise ValueError('Invalid/duplicate pair motion')
        keys.add(key)
