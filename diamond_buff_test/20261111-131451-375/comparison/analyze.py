from pathlib import Path, PureWindowsPath
from collections import defaultdict, Counter
import csv
import gzip
import hashlib
import json
import math
import statistics
import sys

PROJECT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('D:/git/ECHO-/Echogame/CardGame')
CLONE = PROJECT / 'Library/UNKNOWN-Systems-BuildProject'
RUN = Path(sys.argv[2]) if len(sys.argv) > 2 else Path((CLONE / 'Library/DiamondBuff-Location.txt').read_text(encoding='utf-8-sig').strip())
OUT = RUN / 'comparison'
OUT.mkdir(exist_ok=True)
read = lambda p: json.loads(Path(p).read_text(encoding='utf-8-sig'))
exp = read(RUN / 'experiment.json')
assert exp['status'] == 'completed', exp.get('error')
paths = {version: RUN.joinpath(*PureWindowsPath(exp[field]).parts[-2:])
         for version, field in [('Revised', 'revisedDirectory'), ('DiamondBuff', 'diamondBuffDirectory')]}
data = {v: read(p / 'balance_summary.json') for v, p in paths.items()}
meta = {v: read(p / 'test_metadata.json') for v, p in paths.items()}
MAPS = ['Normal', 'Diamond', 'Spade', 'Heart', 'Club', 'JokerGame']
CHANGED = ['Diamond_4', 'Diamond_7', 'Diamond_10']
audit = []

def check(condition, message):
    assert condition, message
    audit.append(message)

def ratio(a, b):
    return a / b if b else None

def mean(values):
    return statistics.mean(values) if values else None

def wilson(wins, count):
    if not count:
        return None, None
    z = 1.95996398454005
    p = wins / count
    den = 1 + z*z/count
    center = (p + z*z/(2*count))/den
    margin = z*math.sqrt(p*(1-p)/count + z*z/(4*count*count))/den
    return center-margin, center+margin

def write_csv(name, rows):
    with (OUT / name).open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)

def metrics(version, map_name='all', main_only=False):
    records = [r for r in meta[version]['matches'] if (map_name == 'all' or r['map'] == map_name)
               and (not main_only or 'stress-' not in r['conditionID'])]
    done = [r for r in records if r['status'] == 'completed']
    same = [r for r in done if r['firstAI'] == r['secondAI']]
    wins = sum(r['winner'] == 0 for r in done)
    same_wins = sum(r['winner'] == 0 for r in same)
    low, high = wilson(same_wins, len(same))
    return dict(version=version, map=map_name, scope='main' if main_only else 'all',
                attempted=len(records), completed=len(done), unresolved=sum(r['status'] == 'unresolved' for r in records),
                errors=sum(r['status'] == 'error' for r in records), meanTurns=mean([r['turns'] for r in done]),
                firstWins=wins, firstWinRate=ratio(wins, len(done)), secondWinRate=ratio(len(done)-wins, len(done)),
                sameAICompleted=len(same), sameAIFirstWins=same_wins, sameAIFirstWinRate=ratio(same_wins, len(same)),
                sameAIWilsonLow=low, sameAIWilsonHigh=high)

for field in ['aiCodeSHA256', 'mapDataSHA256', 'ruleDataSHA256', 'rulesCodeSHA256']:
    check(meta['Revised'][field] == meta['DiamondBuff'][field], 'Shared ' + field)
check(len(exp['revisedCards']) == len(exp['diamondBuffCards']) == 57, 'Full 57-card catalogs')
diffs = []
for before, after in zip(exp['revisedCards'], exp['diamondBuffCards']):
    card = before['cardID']
    for key in ['cardID', 'name', 'suit', 'rank', 'type', 'mana', 'attack', 'hp']:
        check(before[key] == after[key], f'Unchanged {card}/{key}')
    check(len(before['effects']) == len(after['effects']), 'Unchanged effect count ' + card)
    for i, (a, b) in enumerate(zip(before['effects'], after['effects'])):
        check(all(a[k] == b[k] for k in ['kind', 'target', 'freshTarget']), f'Preserved effect rule {card}/{i}')
        if a['amount'] != b['amount']:
            diffs.append(dict(cardID=card, effectIndex=i, kind=a['kind'], before=a['amount'], after=b['amount']))
    if card not in CHANGED:
        check(before['description'] == after['description'], 'Preserved text ' + card)
check(sorted((r['cardID'], r['before'], r['after']) for r in diffs)
      == [('Diamond_10', 5, 6), ('Diamond_4', 3, 4), ('Diamond_7', 2, 3)], 'Exactly three authorized damage changes')
protected = read(PROJECT / 'diamond_buff_test/baseline_backup/BeforeManifest.json')
allowed = {'Assets/UNKNOWN/Battle/Runtime/' + n for n in ['CardData.cs', 'HeartNerfChecks.cs', 'BalanceTelemetry.cs']}
for name, old_hash in protected.items():
    if name not in allowed:
        check(hashlib.sha256((PROJECT / name).read_bytes()).hexdigest() == old_hash, 'Protected source/settings ' + name)
original_data = (PROJECT / 'diamond_buff_test/baseline_backup/CardData.cs').read_text(encoding='utf-8-sig')
current_data = (PROJECT / 'Assets/UNKNOWN/Battle/Runtime/CardData.cs').read_text(encoding='utf-8-sig')
anchor = '        public static string Symbol'
check(original_data[original_data.index(anchor):] == current_data[current_data.index(anchor):],
      'Original baseline card construction remains byte-for-byte identical after newline normalization')
original_telemetry = (PROJECT / 'diamond_buff_test/baseline_backup/BalanceTelemetry.cs').read_text(encoding='utf-8-sig')
telemetry_replacements = {
    'public int requested,before,after;': 'public int requested,before,after;\n        public int secondaryAmount;',
    'void Log(string kind,int source,int target,int amount,string effect="",string outcome="")':
        'void Log(string kind,int source,int target,int amount,string effect="",string outcome="",int secondary=0)',
    'amount=amount,cardID=Find(source)?.card.Definition.CardID ?? "",effect=effect,outcome=outcome':
        'amount=amount,secondaryAmount=secondary,cardID=Find(source)?.card.Definition.CardID ?? "",targetCardID=Find(target)?.card.Definition.CardID ?? "",targetCardType=Find(target)?.card.Definition.CardType.ToString() ?? "",effect=effect,outcome=outcome',
    'Log(e.Kind.ToString(),mapMarker ? -1 : e.SourceID,e.TargetID,e.Amount);':
        'Log(e.Kind.ToString(),mapMarker ? -1 : e.SourceID,e.TargetID,e.Amount,secondary:e.Secondary);',
}
for before, after in telemetry_replacements.items():
    original_telemetry = original_telemetry.replace(before, after)
check(original_telemetry == (PROJECT / 'Assets/UNKNOWN/Battle/Runtime/BalanceTelemetry.cs').read_text(encoding='utf-8-sig'),
      'Telemetry changes only add optional observation fields')

pairs = []
rng_differences = 0
keys = ['conditionID', 'swapID', 'seed', 'map', 'firstAI', 'secondAI', 'firstDeckSeed', 'secondDeckSeed',
        'firstPolicySeed', 'secondPolicySeed', 'initialFirstDeckHash', 'initialSecondDeckHash']
for i, (a, b) in enumerate(zip(meta['Revised']['matches'], meta['DiamondBuff']['matches'])):
    check(all(a[k] == b[k] for k in keys), 'Initial pair ' + str(i))
    rng_differences += (a['randomCalls'], a['randomFingerprint']) != (b['randomCalls'], b['randomFingerprint'])
    if a['status'] == b['status'] == 'completed':
        pairs.append(dict(match=i, map=a['map'], conditionID=a['conditionID'], swapID=a['swapID'],
                          revisedTurns=a['turns'], diamondBuffTurns=b['turns'], delta=b['turns']-a['turns']))
check(len(meta['Revised']['matches']) == len(meta['DiamondBuff']['matches']) == 1000, '1000 games per version')
previous = PROJECT / 'heart_nerf_test/20261111-125107-718/experiment.json'
previous_exp = read(previous)
check(previous_exp['revisedCards'] == exp['revisedCards'], 'Revised 57-card definitions reproduce previous backup exactly')
previous_relative = Path(previous_exp['revisedDirectory']).parts[-2:]
previous_meta = read(previous.parent.joinpath(*previous_relative) / 'test_metadata.json')
legacy_mismatches = []
for i, (a, b) in enumerate(zip(previous_meta['matches'], meta['Revised']['matches'])):
    for key in ['status', 'turns', 'winner', 'reason', 'actions', 'randomCalls', 'randomFingerprint',
                'firstPolicyRandomCalls', 'secondPolicyRandomCalls', 'firstPolicyRandomFingerprint', 'secondPolicyRandomFingerprint']:
        if a[key] != b[key]:
            legacy_mismatches.append(f'{i}/{key}')
check(not legacy_mismatches, 'All 1000 previous Revised results and RNG histories reproduce')

card_rows = []
role_rows = []
hearts = []
target_rows = []
definitions = {v: {r['definition']['cardID']: r['definition'] for r in data[v]['cards']} for v in paths}
all_stats = {v: {r['definition']['cardID']: r['statistics'] for r in data[v]['cards']} for v in paths}
for version in paths:
    for card in CHANGED:
        for map_name in ['all'] + MAPS:
            row = next(r for r in (data[version]['cards'] if map_name == 'all' else data[version]['maps'])
                       if r['definition']['cardID'] == card and (map_name == 'all' or r['statistics']['group'] == map_name))
            s = row['statistics']
            total = s['attackDamage'] + s['effectDamage']
            card_rows.append(dict(version=version, map=map_name, cardID=card, uses=s['uses'],
                effectDamage=s['effectDamage'], totalDamage=total, effectDamagePerUse=ratio(s['effectDamage'], s['uses']),
                totalDamagePerUse=ratio(total, s['uses']), kills=s['kills'], killsPerUse=ratio(s['kills'], s['uses']),
                enemyHeroDamage=s['enemyHeroDamage'], manaPaid=s['manaPaid'], totalDamagePerMana=ratio(total, s['manaPaid']),
                effectDamagePerMana=ratio(s['effectDamage'], s['manaPaid']), winsUsed=s['winsUsed'],
                playerMatchesUsed=s['playerMatchesUsed'], usageWinRate=ratio(s['winsUsed'], s['playerMatchesUsed']),
                sampleStatus='insufficient_sample' if s['uses'] < (200 if map_name == 'all' else 30) else 'reviewable_observational'))
    for suit in ['Diamond', 'Spade', 'Heart', 'Club', 'Unknown']:
        selected = [r['statistics'] for r in data[version]['cards'] if
                    (r['definition']['type'] == 'Unknown' if suit == 'Unknown' else
                     r['definition']['suit'] == suit and r['definition']['type'] == 'Normal')]
        sums = {k: sum(s[k] for s in selected) for k in ['uses', 'attackDamage', 'effectDamage', 'healing',
                'extraHP', 'extraHPAbsorbed', 'draws', 'manaGained', 'attackDecrease', 'debuffPreventedDamage', 'kills']}
        total = sums['attackDamage'] + sums['effectDamage']
        role_rows.append(dict(version=version, suit=suit, **sums, totalDamage=total,
            damagePerUse=ratio(total, sums['uses']), damagePerCompletedMatch=ratio(total, data[version]['completed']),
            healingPerUse=ratio(sums['healing'], sums['uses']), drawsPerUse=ratio(sums['draws'], sums['uses'])))

    matches = {r['match']: r for r in meta[version]['matches']}
    rows = defaultdict(Counter)
    effect_totals = Counter()
    kill_totals = Counter()
    last_hit = {}
    unit_ids = {}
    current = None
    log = paths[version] / 'effect_events.jsonl.gz'
    with gzip.open(log, 'rt', encoding='utf-8-sig') as stream:
        for line in stream:
            if not any(f'"kind":"{kind}"' in line for kind in ['Summon', 'Attack', 'Damage', 'Destroy', 'JokerFailure', 'EffectMeasurement']):
                continue
            e = json.loads(line)
            match = e['match']
            if current != match:
                current = match
                last_hit.clear()
                unit_ids.clear()
            if matches[match]['status'] != 'completed':
                continue
            kind = e['kind']
            source = e['source']
            target = e['target']
            card = e['cardID'] or unit_ids.get(source, '')
            target_card = e.get('targetCardID') or unit_ids.get(target, '')
            map_name = matches[match]['map']

            def add(source_card, field, amount=1):
                for scope in ['all', map_name]:
                    rows[(source_card, scope)][field] += amount

            if kind == 'Summon':
                unit_ids[source] = card
            elif kind == 'JokerFailure':
                last_hit.pop(target, None)
            elif kind in ['Damage', 'Attack']:
                if e['amount'] > 0 and target >= 0:
                    if card:
                        last_hit[target] = (source, card)
                    else:
                        last_hit.pop(target, None)
                if kind == 'Attack':
                    if card.startswith('Diamond_') and target_card.startswith('Heart_'):
                        add(card, 'attackDamageToHeart', e['amount'])
                    counter = e['secondaryAmount']
                    if counter > 0:
                        last_hit[source] = (target, target_card)
                        if target_card.startswith('Diamond_') and card.startswith('Heart_'):
                            add(target_card, 'counterDamageToHeart', counter)
            elif kind == 'Destroy':
                killer_id, killer_card = last_hit.pop(target, (-1, ''))
                if killer_id != target and killer_card.startswith('Diamond_'):
                    kill_totals[killer_card] += 1
                    if target_card.startswith('Heart_'):
                        add(killer_card, 'heartKills')
            elif kind == 'EffectMeasurement' and card.startswith('Diamond_') and e['effect'] in ['Damage', 'RandomDamage', 'ChainDamage']:
                effect_totals[card] += e['amount']
                if e['outcome'] != 'resolved':
                    continue
                check(bool(target_card), f'Target identity {version}/{match}/{target}')
                if target_card.startswith('Heart_'):
                    add(card, 'effectApplicationsToHeart')
                    add(card, 'effectDamageToHeart', e['amount'])
                    if e['amount'] > 0 and e['before'] > 0 and e['after'] == 0:
                        add(card, 'effectHeartKills')
    for card, s in all_stats[version].items():
        if card.startswith('Diamond_'):
            check(effect_totals[card] == s['effectDamage'], f'Exact effect damage reconciliation {version}/{card}')
            check(kill_totals[card] == s['kills'], f'Exact unit kill reconciliation {version}/{card}')
            for scope in ['all'] + MAPS:
                stat = s if scope == 'all' else next(r['statistics'] for r in data[version]['maps']
                        if r['definition']['cardID'] == card and r['statistics']['group'] == scope)
                measured = rows[(card, scope)]
                target_rows.append(dict(version=version, map=scope, cardID=card, uses=stat['uses'],
                    effectDamageToHeart=measured['effectDamageToHeart'],
                    attackDamageToHeart=measured['attackDamageToHeart'], counterDamageToHeart=measured['counterDamageToHeart'],
                    heartKills=measured['heartKills'], effectHeartKills=measured['effectHeartKills'],
                    effectApplicationsToHeart=measured['effectApplicationsToHeart'],
                    heartKillsPerUse=ratio(measured['heartKills'], stat['uses']),
                    effectHeartKillRate=ratio(measured['effectHeartKills'], measured['effectApplicationsToHeart'])))

metric_rows = [metrics(v, m) for m in ['all'] + MAPS for v in paths]
metric_rows += [metrics(v, main_only=True) for v in paths]
result = dict(schemaVersion='2.1', status='completed', executionDirectory=str(RUN),
    functionalChecks=len(exp['passed']), gamesPerVersion=1000, cardChanges=diffs,
    previousRevisedMismatches=legacy_mismatches, rngPathDivergences=rng_differences,
    bothCompletedPairs=len(pairs), bothCompletedMeanTurnDelta=mean([r['delta'] for r in pairs]),
    metrics=metric_rows, cardPerformance=card_rows, diamondVsHeart=target_rows, relativeSuitPerformance=role_rows,
    auditChecks=len(audit), interpretations=[
        'All card aggregates exclude unresolved/error games. Usage win rate is winsUsed/playerMatchesUsed.',
        'Direct effect damage is distinct from normal/counter damage. Actual damage is capped by remaining HP.',
        'Heart-unit kills include attacks, counters and effects, reconciled with existing last-damage attribution.',
        'Paired seats and initial decks share seeds, not fully coupled combat RNG. Wilson intervals are descriptive for dependent swapped pairs.',
        'Other suit role metrics are observational and use the unchanged existing attribution model.'])
(OUT / 'comparison.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
(OUT / 'audit_checks.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
write_csv('match_comparison.csv', metric_rows)
write_csv('card_performance.csv', card_rows)
write_csv('diamond_vs_heart.csv', target_rows)
write_csv('relative_suit_performance.csv', role_rows)
write_csv('paired_completed_games.csv', pairs)
write_csv('card_changes.csv', diffs)
(PROJECT / 'diamond_buff_test/baseline_backup/Revised-card-data.json').write_text(
    json.dumps(exp['revisedCards'], ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({k: result[k] for k in ['functionalChecks', 'auditChecks', 'previousRevisedMismatches',
    'bothCompletedPairs', 'bothCompletedMeanTurnDelta']}, ensure_ascii=False))
print(json.dumps([r for r in metric_rows if r['map'] in ['all', 'Diamond', 'Heart'] and r['scope'] == 'all'], indent=2))
print(RUN)
