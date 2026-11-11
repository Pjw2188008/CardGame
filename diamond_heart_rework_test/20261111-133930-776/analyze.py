from pathlib import Path, PureWindowsPath
from collections import defaultdict, Counter
import csv
import gzip
import hashlib
import json
import math
import random
import statistics
import sys

PROJECT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('D:/git/ECHO-/Echogame/CardGame')
CLONE = PROJECT / 'Library/UNKNOWN-Systems-BuildProject'
RUN = Path(sys.argv[2]) if len(sys.argv) > 2 else Path((CLONE / 'Library/DiamondHeartRework-Location.txt').read_text(encoding='utf-8-sig').strip())
read = lambda p: json.loads(Path(p).read_text(encoding='utf-8-sig'))
exp = read(RUN / 'experiment.json')
assert exp['status'] == 'completed', exp.get('error')
VERSIONS = ['Revised','DiamondReworkOnly','HeartReworkOnly','DiamondHeartRework']
MAPS = ['Normal','Diamond','Spade','Heart','Club','JokerGame']
paths = {c['version']: RUN.joinpath(*PureWindowsPath(c['directory']).parts[-2:]) for c in exp['cohorts']}
data = {v: read(paths[v] / 'balance_summary.json') for v in VERSIONS}
meta = {v: read(paths[v] / 'test_metadata.json') for v in VERSIONS}
cards = {c['version']: {d['cardID']: d for d in c['cards']} for c in exp['cohorts']}
before_live = {d['cardID']: d for d in exp['previousLiveCards']}
audit = []
def check(condition, name):
    assert condition, name
    audit.append(name)
def ratio(a, b):
    return a/b if b else None
def average(values):
    return statistics.mean(values) if values else None
def percentile(values, q):
    if not values: return None
    values = sorted(values)
    index = (len(values)-1)*q
    a = math.floor(index)
    return values[a] + (values[min(a+1,len(values)-1)]-values[a])*(index-a)
def wilson(wins, count):
    if not count: return None, None
    z=1.95996398454005; p=wins/count; den=1+z*z/count
    center=(p+z*z/(2*count))/den; margin=z*math.sqrt(p*(1-p)/count+z*z/(4*count*count))/den
    return center-margin, center+margin
def write_csv(name, rows):
    with (RUN/name).open('w',encoding='utf-8-sig',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(rows[0]))
        writer.writeheader(); writer.writerows(rows)
def merge_csv(source, target):
    rows=[]
    for version in VERSIONS:
        with (paths[version]/source).open(encoding='utf-8-sig',newline='') as stream:
            rows.extend(dict(balanceVersion=version,**row) for row in csv.DictReader(stream))
    write_csv(target,rows)

protected=read(PROJECT/'diamond_heart_rework_test/baseline_backup/BeforeManifest.json')
allowed={'Assets/UNKNOWN/Battle/Runtime/'+n for n in ['CardData.cs','DiamondBuffChecks.cs']}
for name, value in protected.items():
    if name not in allowed:
        check(hashlib.sha256((PROJECT/name).read_bytes()).hexdigest()==value,'Protected '+name)
for key in ['aiCodeSHA256','mapDataSHA256','ruleDataSHA256','rulesCodeSHA256','testerCodeSHA256']:
    check(len({meta[v][key] for v in VERSIONS})==1,'Shared '+key)
body=['cardID','name','suit','rank','type','mana','attack','hp']
changes=[]
for card, old in cards['Revised'].items():
    new=cards['DiamondHeartRework'][card]
    for version in VERSIONS[1:]:
        row=cards[version][card]
        check(all(old[k]==row[k] for k in body),'Preserved body '+version+'/'+card)
        enabled=old['suit']=='Diamond' and version!='HeartReworkOnly' or old['suit']=='Heart' and version!='DiamondReworkOnly'
        if not enabled:
            check(old['effects']==row['effects'] and old['description']==row['description'],'Untouched '+version+'/'+card)
        else:
            check(row['effects']==new['effects'],'Factorial data '+version+'/'+card)
    check(all(old[k]==before_live[card][k] for k in body),'Actual before body '+card)
    if old['suit'] not in ['Diamond','Heart']:
        check(new['effects']==before_live[card]['effects'] and new['description']==before_live[card]['description'],'Outside 26 unchanged '+card)
    changes.append(dict(cardID=card,suit=old['suit'],mana=old['mana'],attack=old['attack'],hp=old['hp'],
        revisedEffects=json.dumps(old['effects'],ensure_ascii=False),previousLiveEffects=json.dumps(before_live[card]['effects'],ensure_ascii=False),
        newEffects=json.dumps(new['effects'],ensure_ascii=False),revisedDescription=old['description'],
        previousLiveDescription=before_live[card]['description'],newDescription=new['description'],
        differsFromRevised=old['effects']!=new['effects'],differsFromPreviousLive=before_live[card]['effects']!=new['effects']))
records={v:meta[v]['matches'] for v in VERSIONS}
keys=['conditionID','swapID','seed','map','firstAI','secondAI','firstDeckSeed','secondDeckSeed','firstPolicySeed','secondPolicySeed','initialFirstDeckHash','initialSecondDeckHash']
divergences={}
for version in VERSIONS:
    check(len(records[version])==1000,'1000 matches '+version)
    check(data[version]['errors']==0,'Zero processing errors '+version)
    divergences[version]=dict(combat=0,policy=0)
    for i,(a,b) in enumerate(zip(records['Revised'],records[version])):
        check(all(a[k]==b[k] for k in keys),'Initial pair '+version+'/'+str(i))
        divergences[version]['combat']+=(a['randomCalls'],a['randomFingerprint'])!=(b['randomCalls'],b['randomFingerprint'])
        divergences[version]['policy']+=any(a[k]!=b[k] for k in ['firstPolicyRandomCalls','secondPolicyRandomCalls','firstPolicyRandomFingerprint','secondPolicyRandomFingerprint'])
groups=defaultdict(list)
for r in records['Revised']: groups[r['conditionID']].append(r)
check(len(groups)==500 and all(len(g)==2 and {r['swapID'] for r in g}=={'AB','BA'} for g in groups.values()),'500 exact swapped conditions including Random stress')
for group in groups.values():
    a,b=sorted(group,key=lambda r:r['swapID'])
    check(a['firstAI']==b['secondAI'] and a['secondAI']==b['firstAI'] and a['firstDeckSeed']==b['secondDeckSeed']
          and a['secondDeckSeed']==b['firstDeckSeed'] and a['firstPolicySeed']==b['secondPolicySeed']
          and a['secondPolicySeed']==b['firstPolicySeed'] and a['seed']==b['seed'],'Identity seat swap '+a['conditionID'])
previous=PROJECT/'diamond_buff_test/20261111-131451-375/experiment.json'
prev=read(previous)
prev_dir=previous.parent.joinpath(*PureWindowsPath(prev['revisedDirectory']).parts[-2:])
old_records=read(prev_dir/'test_metadata.json')['matches']
old_by_key={(r['conditionID'],r['swapID']):r for r in old_records}
legacy_count=0
for r in records['Revised']:
    old=old_by_key.get((r['conditionID'],r['swapID']))
    if old is None: continue
    check(all(old[k]==r[k] for k in keys),'Historical initial match '+str(r['match']))
    check(all(old[k]==r[k] for k in ['status','turns','winner','reason','actions','randomCalls','randomFingerprint',
        'firstPolicyRandomCalls','secondPolicyRandomCalls','firstPolicyRandomFingerprint','secondPolicyRandomFingerprint']),
        'Historical outcome/RNG '+str(r['match']))
    legacy_count+=1
check(legacy_count==980,'980 unchanged legacy legs reproduce; final 20 stress legs now swapped')

def battle(version,map_name='all',main=False):
    selected=[r for r in records[version] if (map_name=='all' or r['map']==map_name) and (not main or 'stress-' not in r['conditionID'])]
    done=[r for r in selected if r['status']=='completed']; same=[r for r in done if r['firstAI']==r['secondAI']]
    turns=[r['turns'] for r in done]; first=sum(r['winner']==0 for r in done); same_first=sum(r['winner']==0 for r in same)
    low,high=wilson(same_first,len(same))
    return dict(version=version,map=map_name,scope='main' if main else 'all',attempted=len(selected),completed=len(done),
        unresolved=sum(r['status']=='unresolved' for r in selected),errors=sum(r['status']=='error' for r in selected),
        meanTurns=average(turns),medianTurns=statistics.median(turns) if turns else None,p90Turns=percentile(turns,.9),
        firstWins=first,firstWinRate=ratio(first,len(done)),secondWinRate=ratio(len(done)-first,len(done)),
        sameAICompleted=len(same),sameAIFirstWins=same_first,sameAIFirstWinRate=ratio(same_first,len(same)),
        sameAIWilsonLow=low,sameAIWilsonHigh=high)
battles=[battle(v,m) for v in VERSIONS for m in ['all']+MAPS]+[battle(v,main=True) for v in VERSIONS]
timings=[]; role_rows=[]; heart_rows=[]; target_rows=[]; spade_rows=[]
for version in VERSIONS:
    done=[r for r in records[version] if r['status']=='completed']
    for map_name in ['all']+MAPS:
        subset=[r for r in done if map_name=='all' or r['map']==map_name]
        creation=[r[k] for r in subset for k in ['firstUnknownCreationTurn','secondUnknownCreationTurn'] if r[k]>=0]
        summon=[r[k] for r in subset for k in ['firstUnknownSummonTurn','secondUnknownSummonTurn'] if r[k]>=0]
        timings.append(dict(version=version,map=map_name,completed=len(subset),playerGames=len(subset)*2,
            creationObserved=len(creation),creationMedian=statistics.median(creation) if creation else None,
            firstSummonObserved=len(summon),firstSummonMedian=statistics.median(summon) if summon else None,
            unknownVictoryCount=sum(r['reason']=='UnknownComplete' for r in subset),
            jokerVictoryCount=sum(r['reason']=='JokerJackpot' for r in subset)))
    stats={r['definition']['cardID']:r for r in data[version]['cards']}
    for suit in ['Diamond','Heart','Spade','Club','Unknown','Joker']:
        selected=[r for r in stats.values() if (r['definition']['type']==suit if suit in ['Unknown','Joker'] else r['definition']['suit']==suit and r['definition']['type']=='Normal')]
        sums={k:sum(r['statistics'][k] for r in selected) for k in ['uses','attackDamage','effectDamage','healing','extraHP','extraHPAbsorbed',
            'attackDecrease','debuffPreventedDamage','draws','manaGained','manaWasted','exchangeDraws','overflowDraws','kills','jokerSuccesses','jokerFailures','jokerPaidUses','jokerFreeUses']}
        role_rows.append(dict(version=version,role=suit,**sums,damagePerUse=ratio(sums['attackDamage']+sums['effectDamage'],sums['uses']),
            healingPerUse=ratio(sums['healing'],sums['uses']),drawsPerUse=ratio(sums['draws'],sums['uses'])))
    target=defaultdict(Counter); healing=defaultdict(Counter); weaken=defaultdict(Counter)
    effect_totals=Counter(); kill_totals=Counter(); current=None; unit_ids={}; last_hit={}
    with gzip.open(paths[version]/'effect_events.jsonl.gz','rt',encoding='utf-8-sig') as stream:
        for line in stream:
            if not any(f'"kind":"{kind}"' in line for kind in ['Summon','Damage','Attack','Destroy','JokerFailure','EffectMeasurement']): continue
            e=json.loads(line); index=e['match']
            if current!=index: current=index;unit_ids.clear();last_hit.clear()
            if records[version][index]['status']!='completed': continue
            kind=e['kind']; src=e['source']; dst=e['target']; card=e['cardID'] or unit_ids.get(src,'')
            victim=e.get('targetCardID') or unit_ids.get(dst,''); map_name=records[version][index]['map']
            def add(table,key,field,value=1):
                table[(key,'all')][field]+=value;table[(key,map_name)][field]+=value
            if kind=='Summon':unit_ids[src]=card
            elif kind=='JokerFailure':last_hit.pop(dst,None)
            elif kind in ['Damage','Attack']:
                if e['amount']>0 and dst>=0:
                    if card:last_hit[dst]=(src,card)
                    else:last_hit.pop(dst,None)
                if kind=='Attack':
                    if card.startswith('Diamond_') and victim.startswith('Heart_'):add(target,card,'attackDamageToHeart',e['amount'])
                    if e['secondaryAmount']>0:
                        last_hit[src]=(dst,victim)
                        if victim.startswith('Diamond_') and card.startswith('Heart_'):add(target,victim,'counterDamageToHeart',e['secondaryAmount'])
            elif kind=='Destroy':
                killer_id,killer=last_hit.pop(dst,(-1,''))
                if killer_id!=dst and killer.startswith('Diamond_'):
                    kill_totals[killer]+=1
                    if victim.startswith('Heart_'):add(target,killer,'heartKills')
            elif kind=='EffectMeasurement':
                effect=e['effect']; amount=e['amount']
                if card.startswith('Diamond_') and effect in ['Damage','RandomDamage','ChainDamage']:
                    effect_totals[card]+=amount
                    if e['outcome']=='resolved':
                        check(bool(victim),'Damage target '+version+'/'+str(index)+'/'+str(dst))
                        if victim.startswith('Heart_'):
                            add(target,card,'effectApplicationsToHeart');add(target,card,'effectDamageToHeart',amount)
                            if amount>0 and e['before']>0 and e['after']==0:add(target,card,'effectHeartKills')
                if card.startswith('Heart_') and effect in ['Heal','FirstAttackHeal']:
                    add(healing,card,'allApplications');add(healing,card,'allZeroResults',int(amount==0))
                    if e['outcome']=='resolved':
                        add(healing,card,'resolvedApplications');add(healing,card,'resolvedZeroResults',int(amount==0));add(healing,card,'actualHealing',amount)
                    if e['outcome']=='source_destroyed':add(healing,card,'sourceDestroyed')
                if card.startswith('Spade_') and effect in ['ModifyAttack','ChainWeaken'] and victim.startswith('Diamond_'):
                    add(weaken,card,'diamondTargetApplications');add(weaken,card,'attackRemovedFromDiamond',max(0,-amount))
    for card,row in stats.items():
        s=row['statistics']
        if card.startswith('Diamond_'):
            check(effect_totals[card]==s['effectDamage'],'Effect total '+version+'/'+card)
            check(kill_totals[card]==s['kills'],'Kill total '+version+'/'+card)
            for scope in ['all']+MAPS:
                scoped=s if scope=='all' else next(r['statistics'] for r in data[version]['maps'] if r['definition']['cardID']==card and r['statistics']['group']==scope)
                measured=target[(card,scope)]
                target_rows.append(dict(version=version,map=scope,cardID=card,uses=scoped['uses'],
                    **{k:measured[k] for k in ['effectDamageToHeart','attackDamageToHeart','counterDamageToHeart','heartKills','effectHeartKills','effectApplicationsToHeart']},
                    heartKillsPerUse=ratio(measured['heartKills'],scoped['uses'])))
        if card.startswith('Heart_'):
            measured=healing[(card,'all')]
            check(measured['actualHealing']==s['healing'],'Healing total '+version+'/'+card)
            effects=[r for r in data[version]['effects'] if r['cardID']==card and r['kind'] in ['Heal','FirstAttackHeal']]
            heart_rows.append(dict(version=version,cardID=card,uses=s['uses'],actualHealing=s['healing'],healingPerUse=ratio(s['healing'],s['uses']),
                effectActivations=sum(r['activations'] for r in effects),extraHP=s['extraHP'],extraHPAbsorbed=s['extraHPAbsorbed'],
                observedMeanSurvival=row['averageSurvival'],censoredUnits=s['censoredUnits'],
                resolvedHealApplications=measured['resolvedApplications'],resolvedZeroResults=measured['resolvedZeroResults'],
                resolvedZeroRate=ratio(measured['resolvedZeroResults'],measured['resolvedApplications']),
                allApplications=measured['allApplications'],allZeroRate=ratio(measured['allZeroResults'],measured['allApplications']),sourceDestroyed=measured['sourceDestroyed'],
                healingStatus='applicable' if effects else 'not_applicable_no_heal_effect'))
        if card.startswith('Spade_'):
            measured=weaken[(card,'all')]
            spade_rows.append(dict(version=version,cardID=card,uses=s['uses'],diamondTargetApplications=measured['diamondTargetApplications'],
                attackRemovedFromDiamond=measured['attackRemovedFromDiamond'],allAttackRemoved=s['attackDecrease'],estimatedPreventedDamage=s['debuffPreventedDamage']))

# Fixed-condition factorial contrasts on game legs completed in every variant.
common=[i for i in range(1000) if all(records[v][i]['status']=='completed' for v in VERSIONS)]
cluster_ids=list(groups)
clusters={name:[] for name in cluster_ids}
for i in common:clusters[records['Revised'][i]['conditionID']].append(i)
def contrast(values):
    b,d,h,both=values
    return dict(diamondAtOriginalHeart=d-b,heartAtOriginalDiamond=h-b,
        interaction=both-d-h+b,diamondMarginal=((d-b)+(both-h))/2,
        heartMarginal=((h-b)+(both-d))/2,combinedChange=both-b)
factorial={}
rng=random.Random(7312026)
for metric_name in ['meanTurns','firstWinRate','sameAIFirstWinRate']:
    per_cluster=[]
    for condition in cluster_ids:
        indices=[i for i in clusters[condition] if metric_name!='sameAIFirstWinRate' or records['Revised'][i]['firstAI']==records['Revised'][i]['secondAI']]
        sums=[sum(records[v][i]['turns'] if metric_name=='meanTurns' else int(records[v][i]['winner']==0) for i in indices) for v in VERSIONS]
        per_cluster.append((sums,len(indices)))
    count=sum(n for _,n in per_cluster)
    values=[sum(s[j] for s,_ in per_cluster)/count for j in range(4)]
    point=contrast(values); boot=defaultdict(list)
    for iteration in range(2000):
        totals=[0,0,0,0];den=0
        for draw in range(len(per_cluster)):
            sums,n=per_cluster[rng.randrange(len(per_cluster))];den+=n
            for j in range(4):totals[j]+=sums[j]
        if den:
            for key,value in contrast([n/den for n in totals]).items():boot[key].append(value)
    factorial[metric_name]=dict(commonCompletedLegs=count,values=dict(zip(VERSIONS,values)),contrasts=point,
        clusterBootstrap95={key:[percentile(series,.025),percentile(series,.975)] for key,series in boot.items()})

unresolved=[dict(r,balanceVersion=v) for v in VERSIONS for r in records[v] if r['status']!='completed']
result=dict(schemaVersion='3.0',status='completed',gamesPerVersion=1000,versions=VERSIONS,executionDirectory=str(RUN),
    previousLiveVersion=exp['previousLiveVersion'],functionalChecks=len(exp['passed']),auditChecks=len(audit),
    historicalRevisedMatchedLegs=legacy_count,rngPathDivergences=divergences,battles=battles,
    changedCardsVsRevised=sum(r['differsFromRevised'] for r in changes),changedCardsVsPreviousLive=sum(r['differsFromPreviousLive'] for r in changes),
    roleStatistics=role_rows,unknownTiming=timings,heartStatistics=heart_rows,diamondVsHeart=target_rows,
    spadeInteractions=spade_rows,factorial=factorial,unresolved=unresolved,
    interpretations=['All aggregates exclude unresolved/error legs; the complete-case factorial uses a common subset across all four versions.',
        'Quantiles use linear interpolation with index (n-1)*q. Survival includes censored terminal units.',
        'Resolved-zero healing excludes absent targets and counter-death failures; no-heal cards are not_applicable.',
        'Usage win rates are per player-game exposure. Attributed prevented/absorbed damage estimates are not additive causal effects.',
        'Bootstrap resamples 500 initial-condition clusters with both swapped legs together, 2000 draws. Contrasts are under fixed AI policies.',
        'A positive first-win contrast increases first-player advantage. Combat RNG streams are not fully coupled.'])
(RUN/'balance_summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
(RUN/'first_player_attribution.json').write_text(json.dumps(factorial,indent=2),encoding='utf-8')
(RUN/'audit_checks.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
write_csv('card_changes.csv',changes);write_csv('battle_comparison.csv',battles)
write_csv('heart_effect_statistics.csv',heart_rows);write_csv('diamond_vs_heart.csv',target_rows)
write_csv('role_interactions.csv',role_rows);write_csv('spade_interactions.csv',spade_rows);write_csv('unknown_timing.csv',timings)
for source,target in [('card_statistics.csv','card_statistics.csv'),('card_map_statistics.csv','map_statistics.csv'),
                      ('card_ai_statistics.csv','ai_statistics.csv'),('card_effect_statistics.csv','card_effect_statistics.csv'),('match_results.csv','match_results.csv')]:
    merge_csv(source,target)
backup=PROJECT/'diamond_heart_rework_test/baseline_backup'
(backup/'PreviousLive-57Cards.json').write_text(json.dumps(exp['previousLiveCards'],ensure_ascii=False,indent=2),encoding='utf-8')
(backup/'Revised-57Cards.json').write_text(json.dumps(list(cards['Revised'].values()),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps([r for r in battles if r['map']=='all' and r['scope']=='all'],indent=2))
print(json.dumps(dict(functionalChecks=len(exp['passed']),auditChecks=len(audit),commonCompleted=len(common),
                      changedVsRevised=result['changedCardsVsRevised'],changedVsLive=result['changedCardsVsPreviousLive'])))
print(RUN)
