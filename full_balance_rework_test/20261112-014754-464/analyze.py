from pathlib import Path,PureWindowsPath
from collections import defaultdict,Counter
import csv,gzip,hashlib,json,math,statistics,sys,random
P=Path(sys.argv[1]) if len(sys.argv)>1 else Path('D:/git/ECHO-/Echogame/CardGame')
C=P/'Library/UNKNOWN-Systems-BuildProject'
R=Path(sys.argv[2]) if len(sys.argv)>2 else Path((C/'Library/FullRework-Location.txt').read_text(encoding='utf-8-sig').strip())
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
exp=read(R/'experiment.json');assert exp['status']=='completed',exp.get('error')
V=['DiamondHeartRework','FullStatsOnly','FullDiamondOnly','FullHeartMapOnly','FullBalanceRework']
maps=['Normal','Diamond','Spade','Heart','Club','JokerGame']
paths={c['version']:R.joinpath(*PureWindowsPath(c['directory']).parts[-2:]) for c in exp['cohorts']}
data={v:read(paths[v]/'balance_summary.json') for v in V};meta={v:read(paths[v]/'test_metadata.json') for v in V}
records={v:meta[v]['matches'] for v in V};cards={c['version']:{x['cardID']:x for x in c['cards']} for c in exp['cohorts']}
audit=[]
def check(ok,name):
 assert ok,name
 audit.append(name)
def ratio(a,b):return a/b if b else None
def mean(x):return statistics.mean(x) if x else None
def pct(x,q):
 if not x:return None
 x=sorted(x);i=(len(x)-1)*q;a=math.floor(i);return x[a]+(x[min(a+1,len(x)-1)]-x[a])*(i-a)
def save(name,obj):(R/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
def write(name,rows):
 if not rows:return
 with (R/name).open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
def merge(source,target):
 rows=[]
 for v in V:
  with (paths[v]/source).open(encoding='utf-8-sig',newline='') as f:rows.extend(dict(balanceVersion=v,**r) for r in csv.DictReader(f))
 write(target,rows)
allowed={'CardData.cs','MapDefinition.cs','BattleEngine.cs','SuitMapRules.cs','BattleProtocol.cs','BalanceTelemetry.cs'}
for name,h in read(P/'full_balance_rework_test/baseline_backup/BeforeManifest.json').items():
 if name not in {'Assets/UNKNOWN/Battle/Runtime/'+n for n in allowed}:check(hashlib.sha256((P/name).read_bytes()).hexdigest()==h,'Protected '+name)
keys=['conditionID','swapID','seed','map','firstAI','secondAI','firstDeckSeed','secondDeckSeed','firstPolicySeed','secondPolicySeed','initialFirstDeckHash','initialSecondDeckHash']
divergences={}
for v in V:
 check(len(records[v])==1000,'1000 games '+v);check(data[v]['errors']==0,'No game errors '+v)
 divergences[v]=Counter()
 for i,(a,b) in enumerate(zip(records[V[0]],records[v])):
  check(all(a[k]==b[k] for k in keys),'Paired conditions '+v+str(i))
  divergences[v]['combat']+=any(a[k]!=b[k] for k in ['randomCalls','randomFingerprint'])
  divergences[v]['policy']+=any(a[k]!=b[k] for k in ['firstPolicyRandomCalls','secondPolicyRandomCalls','firstPolicyRandomFingerprint','secondPolicyRandomFingerprint'])
oldexp=read(P/'diamond_heart_rework_test/20261111-133930-776/experiment.json')
co=next(c for c in oldexp['cohorts'] if c['version']==V[0])
oldmeta=read(P/'diamond_heart_rework_test/20261111-133930-776'/Path(*PureWindowsPath(co['directory']).parts[-2:])/'test_metadata.json')
for i,(a,b) in enumerate(zip(oldmeta['matches'],records[V[0]])):
 check(all(a[k]==b[k] for k in keys+['status','turns','winner','reason','actions','randomCalls','randomFingerprint','firstPolicyRandomCalls','secondPolicyRandomCalls','firstPolicyRandomFingerprint','secondPolicyRandomFingerprint']),'Exact historical reference '+str(i))
groups=defaultdict(list)
for r in records[V[0]]:groups[r['conditionID']].append(r)
check(len(groups)==500,'500 condition clusters')
for condition,g in groups.items():
 a,b=sorted(g,key=lambda r:r['swapID']);check(a['swapID']=='AB' and b['swapID']=='BA' and a['firstAI']==b['secondAI'] and a['secondAI']==b['firstAI'] and a['firstDeckSeed']==b['secondDeckSeed'] and a['firstPolicySeed']==b['secondPolicySeed'],'Swapped identities '+condition)
changes=[]
for id,b in cards[V[0]].items():
 row=dict(cardID=id,suit=b['suit'],type=b['type'],mana=b['mana'],beforeAttack=b['attack'],beforeHP=b['hp'],beforeEffects=json.dumps(b['effects'],ensure_ascii=False))
 for v in V:
  c=cards[v][id];check(c['mana']==b['mana'],'Mana unchanged '+v+id)
  if b['type']!='Normal':check(all(c[k]==b[k] for k in ['attack','hp','effects','description']),'Special unchanged '+v+id)
  if v in ['FullDiamondOnly','FullHeartMapOnly']:check(c['attack']==b['attack'] and c['hp']==b['hp'],'Body isolation '+v+id)
  if v in ['FullStatsOnly','FullHeartMapOnly'] or b['suit']!='Diamond':check(c['effects']==b['effects'],'Effect isolation '+v+id)
  row[v+'Attack']=c['attack'];row[v+'HP']=c['hp'];row[v+'Effects']=json.dumps(c['effects'],ensure_ascii=False)
 changes.append(row)
write('card_changes.csv',changes)
def battle(v,m='all'):
 rr=[r for r in records[v] if m=='all' or r['map']==m];done=[r for r in rr if r['status']=='completed'];t=[r['turns'] for r in done];same=[r for r in done if r['firstAI']==r['secondAI']]
 return dict(version=v,map=m,attempted=len(rr),completed=len(done),unresolved=sum(r['status']=='unresolved' for r in rr),errors=sum(r['status']=='error' for r in rr),meanTurns=mean(t),medianTurns=pct(t,.5),p90Turns=pct(t,.9),firstWins=sum(r['winner']==0 for r in done),firstWinRate=ratio(sum(r['winner']==0 for r in done),len(done)),secondWinRate=ratio(sum(r['winner']==1 for r in done),len(done)),sameAICompleted=len(same),sameAIFirstWinRate=ratio(sum(r['winner']==0 for r in same),len(same)),longGamesOver50=sum(x>50 for x in t),longGameRateOver50=ratio(sum(x>50 for x in t),len(t)))
battles=[battle(v,m) for v in V for m in ['all']+maps];write('battle_comparison.csv',battles)
early=[];heartmap=[];cardextras=[];weakrows=[];role=[];unknown=[];healrows=[];targetrows=[]
for v in V:
 stats={r['definition']['cardID']:r for r in data[v]['cards']};extra=defaultdict(Counter);heart=Counter();heals=defaultdict(Counter);target=defaultdict(Counter)
 current=None;state=None
 def newstate(i):return dict(index=i,ids={},owners={},maxhp={},summon={},dead={},deadBy={},last={},turns={},paid=[0,0],gained=[0,0],available=[3,0],availableSeen={1},firstunit=None,firstkills=[None,None],firsthero=[None,None],hero10=[0,0],zero={},weak={},weaklife=[],cloverAttacks=0,cloverGains=0,cloverOwnerGains=[0,0],cloverManaSpentAfterGain=0)
 def close(s):
  if s is None:return
  rr=records[v][s['index']];t=rr['turns'];first=s['firstunit'];alive=first is not None and (first not in s['dead'] or s['dead'][first]>2)
  turn5=s['turns'].get(5);turn10=s['turns'].get(10)
  early.append(dict(version=v,match=s['index'],map=rr['map'],firstAI=rr['firstAI'],secondAI=rr['secondAI'],turns=t,firstUnitPlaced=first is not None,firstUnitSurvivesReply=alive if first is not None else None,secondRemovedFirstUnit=first is not None and s['dead'].get(first)==2 and s['deadBy'].get(first)==1,
    secondFirstKillTurn=s['firstkills'][1],firstDirectDamageTurn=s['firsthero'][0],secondDirectDamageTurn=s['firsthero'][1],
    firstManaPaid=s['paid'][0],secondManaPaid=s['paid'][1],firstManaAvailable=s['available'][0]+s['gained'][0],secondManaAvailable=s['available'][1]+s['gained'][1],
    firstManaUseRatio=ratio(s['paid'][0],s['available'][0]+s['gained'][0]),secondManaUseRatio=ratio(s['paid'][1],s['available'][1]+s['gained'][1]),
    endTurn5UnitDifference=(turn5[0]-turn5[1]) if turn5 else None,first10PlayerDamage=s['hero10'][0],second10PlayerDamage=s['hero10'][1],first10DamageDifference=s['hero10'][0]-s['hero10'][1],
    zeroAttackUnitTurns=sum(s['zero'].values()),weakenedDeaths=len(s['weaklife']),meanTurnsWeakenToDeath=mean(s['weaklife']),cloverAttackOpportunitiesUsed=s['cloverAttacks'],cloverManaGains=s['cloverGains'],manaSpentAfterCloverGain=s['cloverManaSpentAfterGain']))
  for id,start in s['summon'].items():
   c=s['ids'][id];extra[c]['observedLifeTurns']+=s['dead'].get(id,t)-start;extra[c]['summoned']+=1;extra[c]['censored']+=id not in s['dead']
 with gzip.open(paths[v]/'effect_events.jsonl.gz','rt',encoding='utf-8-sig') as f:
  for line in f:
   e=json.loads(line);i=e['match']
   if current!=i:close(state);current=i;state=newstate(i) if records[v][i]['status']=='completed' else None
   if state is None:continue
   s=state;k=e['kind'];src=e['source'];dst=e['target'];turn=e['turn'];card=e['cardID'] or s['ids'].get(src,'');victim=e.get('targetCardID') or s['ids'].get(dst,'')
   if k=='Command':s['actor']=e['actor'];s['command']=e['effect']
   elif k=='Summon':
    s['ids'][src]=card;s['owners'][src]=s.get('actor',0);s['summon'][src]=turn;s['maxhp'][src]=cards[v][card]['hp'];extra[card]['maximumObservedHP']=max(extra[card]['maximumObservedHP'],s['maxhp'][src])
    if s['firstunit'] is None and s['owners'][src]==0 and turn==1:s['firstunit']=src
   elif k=='StateSample':
    # First snapshot of a new turn may already contain the preceding owner's end-turn. Infer starting owner from the immutable turn order.
    actor=(turn-1)%2
    if turn not in s['availableSeen']:s['availableSeen'].add(turn);s['available'][actor]+=e['firstMaxMana'] if actor==0 else e['secondMaxMana']
    units=[list(map(int,z.split(':'))) for z in (e.get('unitStates') or '').split('|') if z]
    # End-turn commands advance turn before this snapshot. Do not replace the prior turn's actual final board.
    if s.get('command')!='EndTurn':s['turns'][turn]=(e['firstUnits'],e['secondUnits'])
    else:s['turns'].setdefault(turn,(e['firstUnits'],e['secondUnits']))
    for id,owner,atk,hp in units:
     if atk==0:s['zero'][(id,turn)]=1
     if id in s['ids']:extra[s['ids'][id]]['maximumObservedAttack']=max(extra[s['ids'][id]]['maximumObservedAttack'],atk)
   elif k=='ManaPayment':
    actor=e['actor'];s['paid'][actor]+=e['amount']
    if s['cloverOwnerGains'][actor]>0:s['cloverManaSpentAfterGain']+=e['amount']
   elif k=='ManaGained':
    owner=s['owners'].get(src,(turn-1)%2);s['gained'][owner]+=e['amount']
    if card.startswith('Club_'):s['cloverGains']+=e['amount'];s['cloverOwnerGains'][owner]+=e['amount']
   elif k=='DamageAttempt':
    check(e['requested']>=e['amount']>=0,'Damage bounds '+v+str(i))
    if card:
     extra[card]['requestedDamage']+=e['requested'];extra[card]['overkill']+=e['requested']-e['amount'];extra[card]['damageAttempts']+=1
     extra[card]['requested'+e['effect']]+=e['requested'];extra[card]['actual'+e['effect']]+=e['amount']
    if dst<0 and e['amount']>0 and e['actor'] in [0,1]:
     owner=e['actor'];s['firsthero'][owner]=s['firsthero'][owner] or turn
     if turn<=10:s['hero10'][owner]+=e['amount']
   elif k in ['Damage','Attack']:
    if e['amount']>0 and dst>=0:s['last'][dst]=(src,card)
    if k=='Attack':
     if card.startswith('Club_'):s['cloverAttacks']+=1
     if e['secondaryAmount']>0:s['last'][src]=(dst,victim)
     if card.startswith('Diamond_') and victim.startswith('Heart_'):
      for scope in ['all',records[v][i]['map']]:target[(card,scope)]['attackDamageToHeart']+=e['amount']
     if victim.startswith('Diamond_') and card.startswith('Heart_'):
      for scope in ['all',records[v][i]['map']]:target[(victim,scope)]['counterDamageToHeart']+=e['secondaryAmount']
   elif k=='JokerFailure':s['last'].pop(dst,None)
   elif k=='Destroy':
    s['dead'][dst]=turn;killer,kcard=s['last'].pop(dst,(-1,''));owner=s['owners'].get(killer);s['deadBy'][dst]=owner
    if owner is not None and owner!=s['owners'].get(dst):s['firstkills'][owner]=s['firstkills'][owner] or turn
    if kcard.startswith('Diamond_') and victim.startswith('Heart_'):
     for scope in ['all',records[v][i]['map']]:target[(kcard,scope)]['heartKills']+=1
    if dst in s['weak']:s['weaklife'].append(turn-s['weak'][dst])
   elif k=='Heal' and src==-1 and records[v][i]['map']=='Heart':heart['mapRecoveryActivations']+=1;heart['effectiveMapHealing']+=e['amount']
   elif k=='HeroChanged' and records[v][i]['map']=='Heart':heart['periodicHeroHealing']+=max(0,e['amount'])
   elif k=='MaxHPChanged':
    if dst in s['maxhp']:
     s['maxhp'][dst]+=e['amount'];extra[victim]['maximumObservedHP']=max(extra[victim]['maximumObservedHP'],s['maxhp'][dst])
    if src==-1 and records[v][i]['map']=='Heart':heart['summonHPGranted']+=e['amount']
   elif k=='EffectMeasurement':
    effect=e['effect'];a=e['amount']
    if effect in ['ModifyAttack','ChainWeaken'] and a<0:s['weak'].setdefault(dst,turn)
    if card.startswith('Diamond_') and effect in ['Damage','ChainDamage','RandomDamage'] and victim.startswith('Heart_'):
     for scope in ['all',records[v][i]['map']]:target[(card,scope)]['effectDamageToHeart']+=a;target[(card,scope)]['effectApplicationsToHeart']+=e['outcome']=='resolved'
    if card.startswith('Heart_') and effect in ['Heal','FirstAttackHeal']:
     heals[card]['applications']+=1;heals[card]['actualHealing']+=a
     if e['outcome']=='resolved':heals[card]['resolved']+=1;heals[card]['resolvedZero']+=a==0
 forclose=state
 close(forclose)
 h=battle(v,'Heart');heartmap.append(dict(version=v,**{k:x for k,x in h.items() if k not in ['version','map']},**{k:heart[k] for k in ['mapRecoveryActivations','effectiveMapHealing','periodicHeroHealing','summonHPGranted']}))
 if v in ['FullHeartMapOnly','FullBalanceRework']:check(heart['periodicHeroHealing']==0,'No periodic heart heal '+v)
 for id,row in stats.items():
  st=row['statistics'];x=extra[id]
  check(x['summoned']==st['summons'],'Summon tracking '+v+id)
  check(x['observedLifeTurns']==st['survivalTurnSum'],'Survival reconciliation '+v+id)
  cardextras.append(dict(version=v,cardID=id,attack=row['definition']['attack'],hp=row['definition']['hp'],uses=st['uses'],directEffectDamagePerUse=ratio(st['effectDamage'],st['uses']),attackDamagePerUse=ratio(st['attackDamage'],st['uses']),kills=st['kills'],playerDamage=st['enemyHeroDamage'],damagePerMana=row['damagePerMana'],observedUsageWinRate=row['usageWinRate'],observedMeanLife=ratio(x['observedLifeTurns'],x['summoned']),lifePerBaseHP=ratio(x['observedLifeTurns'],x['summoned']*row['definition']['hp']),censoredUnits=x['censored'],maximumObservedHP=x['maximumObservedHP'],maximumObservedAttack=x['maximumObservedAttack'],requestedDamage=x['requestedDamage'],overkill=x['overkill'],overkillRatio=ratio(x['overkill'],x['requestedDamage'])))
  if id.startswith('Heart_'):
   z=heals[id];check(z['actualHealing']==st['healing'],'Heart healing '+v+id)
   healrows.append(dict(version=v,cardID=id,uses=st['uses'],healing=st['healing'],healingPerUse=ratio(st['healing'],st['uses']),extraHP=st['extraHP'],estimatedAbsorbed=st['extraHPAbsorbed'],resolvedApplications=z['resolved'],resolvedZeroRate=ratio(z['resolvedZero'],z['resolved']),observedMeanLife=row['averageSurvival']))
  if id.startswith('Diamond_'):
   for scope in ['all']+maps:
    scoped=st if scope=='all' else next(r['statistics'] for r in data[v]['maps'] if r['definition']['cardID']==id and r['statistics']['group']==scope)
    targetrows.append(dict(version=v,map=scope,cardID=id,uses=scoped['uses'],**{k:target[(id,scope)][k] for k in ['effectDamageToHeart','attackDamageToHeart','counterDamageToHeart','heartKills','effectApplicationsToHeart']},heartKillsPerUse=ratio(target[(id,scope)]['heartKills'],scoped['uses'])))
 for suit in ['Diamond','Spade','Heart','Club','Unknown','Joker']:
  rr=[r for r in stats.values() if (r['definition']['type']==suit if suit in ['Unknown','Joker'] else r['definition']['type']=='Normal' and r['definition']['suit']==suit)]
  sums={k:sum(r['statistics'][k] for r in rr) for k in ['uses','attackDamage','effectDamage','kills','healing','extraHP','extraHPAbsorbed','attackDecrease','debuffPreventedDamage','discardEffects','draws','manaGained','manaWasted','exchangeDraws','jokerSuccesses','jokerFailures','unknownCreated','survivalTurnSum','summons']}
  role.append(dict(version=v,role=suit,**sums,jokerSuccessRate=ratio(sums['jokerSuccesses'],sums['jokerSuccesses']+sums['jokerFailures']),jokerFailureRate=ratio(sums['jokerFailures'],sums['jokerSuccesses']+sums['jokerFailures']),damagePerUse=ratio(sums['attackDamage']+sums['effectDamage'],sums['uses']),observedMeanLife=ratio(sums['survivalTurnSum'],sums['summons'])))
 for m in ['all']+maps:
  rr=[r for r in records[v] if r['status']=='completed' and (m=='all' or r['map']==m)];t=[r[k] for r in rr for k in ['firstUnknownSummonTurn','secondUnknownSummonTurn'] if r[k]>=0];ct=[r[k] for r in rr for k in ['firstUnknownCreationTurn','secondUnknownCreationTurn'] if r[k]>=0]
  unknown.append(dict(version=v,map=m,completed=len(rr),firstCreationObserved=len(ct),firstCreationMedian=pct(ct,.5),firstSummonObserved=len(t),firstSummonMedian=pct(t,.5),unknownWins=sum(r['reason']=='UnknownComplete' for r in rr),jokerWins=sum(r['reason']=='JokerJackpot' for r in rr),hpWins=sum(r['reason']=='OpponentHPZero' for r in rr)))
 print(v+' log analyzed',flush=True)
write('early_game_match_metrics.csv',early);write('heart_map_comparison.csv',heartmap);write('card_combat_metrics.csv',cardextras);write('role_statistics.csv',role);write('heart_effect_statistics.csv',healrows);write('diamond_vs_heart.csv',targetrows);write('unknown_comparison.csv',unknown)
zero_maps=[]
for v in V:
 for m in maps:
  rr=[r for r in early if r['version']==v and r['map']==m]
  zero_maps.append(dict(version=v,map=m,completed=len(rr),zeroAttackUnitTurnsMean=mean([r['zeroAttackUnitTurns'] for r in rr]),matchesWithZeroAttack=sum(r['zeroAttackUnitTurns']>0 for r in rr),weakenedDeaths=sum(r['weakenedDeaths'] for r in rr),meanWeakenToDeath=mean([r['meanTurnsWeakenToDeath'] for r in rr if r['meanTurnsWeakenToDeath'] is not None])))
write('zero_attack_by_map.csv',zero_maps)
# Common completed paired contrasts. Cluster bootstrap retains swapped identities and seeds together.
common=[i for i in range(1000) if all(records[v][i]['status']=='completed' for v in V)];clusters=list(groups);rng=random.Random(7312026);contrasts={}
for metric in ['turns','firstWin','sameAIFirstWin']:
 cells=[]
 for condition in clusters:
  ii=[i for i in common if records[V[0]][i]['conditionID']==condition and (metric!='sameAIFirstWin' or records[V[0]][i]['firstAI']==records[V[0]][i]['secondAI'])]
  cells.append(([sum(records[v][i]['turns'] if metric=='turns' else records[v][i]['winner']==0 for i in ii) for v in V],len(ii)))
 den=sum(n for _,n in cells);values=[sum(x[j] for x,n in cells)/den for j in range(5)];boot=defaultdict(list)
 for _ in range(2000):
  sampled=[cells[rng.randrange(len(cells))] for _ in cells];n=sum(n for x,n in sampled)
  if n:
   val=[sum(x[j] for x,d in sampled)/n for j in range(5)]
   for j in range(1,5):boot[V[j]].append(val[j]-val[0])
 contrasts[metric]=dict(commonCompleted=den,values=dict(zip(V,values)),deltasFromReference={V[j]:dict(delta=values[j]-values[0],clusterBootstrap95=[pct(boot[V[j]],.025),pct(boot[V[j]],.975)]) for j in range(1,5)})
firstsummary=[]
for v in V:
 rr=[r for r in early if r['version']==v];placed=[r for r in rr if r['firstUnitPlaced']]
 out=dict(version=v,completed=len(rr),firstPlacementMatches=len(placed),firstUnitReplySurvivalRate=mean([r['firstUnitSurvivesReply'] for r in placed]),replyFirstUnitRemovalRate=mean([r['secondRemovedFirstUnit'] for r in placed]))
 for k in ['secondFirstKillTurn','firstDirectDamageTurn','secondDirectDamageTurn','firstManaUseRatio','secondManaUseRatio','endTurn5UnitDifference','first10DamageDifference','zeroAttackUnitTurns','meanTurnsWeakenToDeath','cloverAttackOpportunitiesUsed','manaSpentAfterCloverGain']:
  vals=[r[k] for r in rr if r[k] is not None];out[k+'Mean']=mean(vals);out[k+'Observed']=len(vals)
 firstsummary.append(out)
first=dict(summary=firstsummary,pairedContrasts=contrasts,definitions=['First unit survival: alive through global turn 2; reply removal means destroyed on turn 2, regardless of attribution.','First five turns: board difference at the last sampled state in global turn 5, conditional on reaching it.','Mana efficiency: paid / (turn-start max mana plus actual gained mana), includes capacity remaining at game end.','Zero attack exposure counts unit/global-turn observations; weakened-death lag is conditional on observed death.','Clover follow-up spend is descriptive after any clover gain, not causal attribution.'])
save('first_player_analysis.json',first)
unresolved_diagnostics=[]
for v in V:
 for m in maps:
  rr=[r for r in records[v] if r['status']=='unresolved' and r['map']==m]
  zero_final=0
  for r in rr:
   units=[z for field in ['firstBoard','secondBoard'] for z in r[field].split('|') if z]
   zero_final+=bool(units) and all(int(z.split('/')[1])==0 for z in units)
  unresolved_diagnostics.append(dict(version=v,map=m,unresolved=len(rr),allRemainingUnitsZeroAttack=zero_final,reasons=dict(Counter(r['error'] for r in rr))))
for source,target in [('card_statistics.csv','card_statistics.csv'),('card_map_statistics.csv','map_statistics.csv'),('card_ai_statistics.csv','ai_statistics.csv'),('card_effect_statistics.csv','card_effect_statistics.csv'),('match_results.csv','match_results.csv')]:merge(source,target)
summary=dict(schemaVersion='4.0',status='completed',versions=V,gamesPerVersion=1000,functionalChecks=len(exp['passed']),auditChecks=len(audit),referenceHistoricalExactGames=1000,battles=battles,heartMap=heartmap,roles=role,firstPlayer=first,unknown=unknown,zeroAttackByMap=zero_maps,rngPathDivergences=divergences,unresolved=[dict(r,balanceVersion=v) for v in V for r in records[v] if r['status']!='completed'],limitations=['Fixed policies, no human or online validation. No changes to AI, mana, victory, maps other than opt-in Heart.','Completed games only in aggregates. Unresolved and processing errors reported separately.','Survival includes terminal censored units. Extra-HP absorption and prevented damage are attribution estimates.','A-D arms isolate individual factors; E interactions are not fully separated by this five-arm design.','Requested-minus-actual is overkill, including self damage and hero/counter damage. All first damage timings refer to attack hits against hero.'])
save('balance_summary.json',summary);save('audit_checks.json',audit)
summary['unresolvedDiagnostics']=unresolved_diagnostics
save('balance_summary.json',summary)
(P/'full_balance_rework_test/baseline_backup/DiamondHeartRework-57Cards.json').write_text(json.dumps(exp['previousLiveCards'],ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps([b for b in battles if b['map']=='all'],indent=2));print('Audit checks',len(audit))
