const fs = require('node:fs');
const path = require('node:path');
const readline = require('node:readline');
const zlib = require('node:zlib');
const root = process.argv[2];
const output = process.argv[3];
if (!root || !output) throw new Error('Provide experiment directory and audit directory');
fs.mkdirSync(output, { recursive: true });
const read = name => JSON.parse(fs.readFileSync(path.join(root, name), 'utf8').replace(/^\uFEFF/, ''));
const summary = read('balance_summary.json');
const metadata = read('test_metadata.json');
const passed = [];
function check(ok, name) { if (!ok) throw new Error(name); passed.push(name); }
const expectedCounts = [480, 480, 20, 20];
check(summary.cohorts.every((c, i) => c.attempted === expectedCounts[i]), '480 + 480 + 20 + 20 schedule');
check(metadata.main.length === 480 && metadata.stress.length === 20, '240 paired main conditions and 20 stress conditions');
const conditionCounts = new Map();
for (const item of metadata.main) {
  const key = `${item.map}|${item.conditionID}`;
  conditionCounts.set(key, (conditionCounts.get(key) || 0) + 1);
}
check(conditionCounts.size === 240 && [...conditionCounts.values()].every(n => n === 2), 'Each main condition has two seat-swapped legs');
for (let map = 0; map < 6; map++) {
  const rows = metadata.main.filter(m => m.map === map && m.swapID === 'AB');
  check(rows.length === 40, `Map ${map} has 40 initial conditions`);
  for (const ai of [1, 2, 3]) check(rows.filter(m => m.first === ai && m.second === ai).length === 4, `Map ${map}, same AI ${ai}, four conditions`);
  const mixed = rows.filter(m => m.first !== m.second);
  check(mixed.length === 28, `Map ${map} has 27 mixed conditions plus one rotating extra`);
}
for (const versionKey of ['ruleDataSHA256', 'mapDataSHA256', 'aiCodeSHA256', 'rulesCodeSHA256']) {
  check(new Set(metadata.versions.map(v => v[versionKey])).size === 1, `Shared ${versionKey}`);
}
check(new Set(metadata.versions.map(v => v.cardDataSHA256)).size === 2, 'Exactly two card data versions');
const old = summary.cohorts[0].matches;
const revised = summary.cohorts[1].matches;
for (let i = 0; i < old.length; i++) {
  for (const key of ['conditionID', 'swapID', 'initialFirstDeckHash', 'initialSecondDeckHash', 'firstPolicySeed', 'secondPolicySeed']) {
    check(old[i][key] === revised[i][key], `Pair ${i} matches ${key}`);
  }
}
const raw = {};
const completed = {};
const cardStats = {};
const heartRows = [];
const spadeRows = [];
const unknownRows = [];
for (const cohort of summary.cohorts) {
  const cohortDirectory = path.join(root, cohort.name, path.basename(cohort.directory));
  raw[cohort.name] = JSON.parse(fs.readFileSync(path.join(cohortDirectory, 'balance_summary.json'), 'utf8'));
  completed[cohort.name] = new Set(cohort.matches.filter(m => m.status === 'completed').map(m => m.match));
  const byId = new Map(raw[cohort.name].cards.map(row => [row.definition.cardID, row]));
  cardStats[cohort.name] = byId;
  check(raw[cohort.name].cards.length === 57 && raw[cohort.name].maps.length === 342 && raw[cohort.name].ai.length === 228, `${cohort.name} exports all card/map/AI rows`);
  let received = 0, accounted = 0;
  for (const row of byId.values()) {
    const s = row.statistics;
    check(s.attackDamage + s.effectDamage === s.enemyHeroDamage + s.enemyUnitDamage, `${cohort.name}/${row.definition.cardID} damage partition`);
    received += s.damageReceived;
    accounted += s.enemyUnitDamage + s.selfDamage + s.mapDamageReceived;
  }
  check(received === accounted, `${cohort.name} damage conservation`);
  for (const rank of ['4', '7', '10', 'J', 'K']) {
    const cardID = `Heart_${rank}`;
    const row = byId.get(cardID);
    const effect = raw[cohort.name].effects.filter(e => e.cardID === cardID && (cohort.name.startsWith('baseline') ? e.kind === 'Heal' && e.target === 'Self' : e.kind === 'FirstAttackHeal'));
    heartRows.push({ cohort: cohort.name, cardID, uses: row.statistics.uses, actualRecovery: effect.reduce((a, e) => a + e.actualAmount, 0), activations: effect.reduce((a, e) => a + e.activations, 0), aliveConditions: effect.reduce((a, e) => a + e.conditionsMet, 0), sourceDestroyed: effect.reduce((a, e) => a + e.invalidated, 0) });
  }
  const e = raw[cohort.name].effects.find(e => e.cardID === 'Spade_K' && e.kind === 'ModifyAttack');
  spadeRows.push({ cohort: cohort.name, uses: byId.get('Spade_K').statistics.uses, ...e });
  for (const cardID of ['Unknown_J', 'Unknown_Q', 'Unknown_K']) {
    const row = byId.get(cardID);
    unknownRows.push({ cohort: cohort.name, cardID, uses: row.statistics.uses, summons: row.statistics.summons, meanSurvival: row.averageSurvival, terminalCensored: row.statistics.censoredUnits, damageReceived: row.statistics.damageReceived });
  }
}
const events = {};
const jokerGroups = new Map();
const payment = {};
async function analyzeEvents() {
  const logPath = path.join(root, 'effect_events.jsonl');
  const input = fs.existsSync(logPath)
    ? fs.createReadStream(logPath)
    : fs.createReadStream(logPath + '.gz').pipe(zlib.createGunzip());
  const lines = readline.createInterface({ input, crlfDelay: Infinity });
  for await (const line of lines) {
    if (!line.includes('"EffectMeasurement"') && !line.includes('"JokerOutcome"') && !line.includes('"ManaPayment"')) continue;
    const e = JSON.parse(line);
    if (!completed[e.cohort].has(e.match)) continue;
    if (e.kind === 'ManaPayment') {
      payment[e.cohort] = (payment[e.cohort] || 0) + e.amount;
      const definition = cardStats[e.cohort].get(e.cardID).definition;
      check(e.requested === definition.mana && e.amount === definition.mana && e.before - e.after === e.amount, `${e.cohort}/${e.match} exact mana payment`);
    }
    if (e.kind === 'EffectMeasurement' && e.cardID === 'Spade_K' && e.effect === 'ModifyAttack') {
      const key = `${e.cohort}|${e.outcome}`;
      events[key] = (events[key] || 0) + 1;
    }
    if (e.kind !== 'JokerOutcome') continue;
    const key = `${e.cohort}|${e.targetCardID}|${e.paid}`;
    const row = jokerGroups.get(key) || { cohort: e.cohort, targetCardID: e.targetCardID, targetCardType: e.targetCardType, paid: e.paid, uses: 0, success: 0, failure: 0, expectedSuccessSum: 0, spentMana: 0, successAttackGain: 0, successHPGain: 0, failureAttackLoss: 0, failureHPLoss: 0 };
    row.uses++; row.expectedSuccessSum += e.expectedProbability; row.spentMana += e.spentMana;
    if (e.outcome === 'success') { row.success++; row.successAttackGain += e.afterAttack - e.beforeAttack; row.successHPGain += e.after - e.before; }
    else { row.failure++; row.failureAttackLoss += e.beforeAttack; row.failureHPLoss += e.before; }
    jokerGroups.set(key, row);
  }
  for (const cohort of summary.cohorts) {
    const paid = [...cardStats[cohort.name].values()].reduce((a, r) => a + r.statistics.manaPaid, 0);
    check((payment[cohort.name] || 0) === paid, `${cohort.name} payment events reconcile aggregate mana spent`);
    const stats = [...cardStats[cohort.name].values()].filter(r => r.definition.type === 'Joker');
    const n = stats.reduce((a, r) => a + r.statistics.jokerSuccesses + r.statistics.jokerFailures, 0);
    const measured = [...jokerGroups.values()].filter(r => r.cohort === cohort.name).reduce((a, r) => a + r.uses, 0);
    check(n === measured, `${cohort.name} target-specific Joker outcomes reconcile`);
  }
  check((events['revised_main|invalidated'] || 0) === 0 && (events['revised_stress|invalidated'] || 0) === 0, 'Revised Spade K has zero stale-target invalidations');
  const quote = v => `"${String(v ?? '').replaceAll('"', '""')}"`;
  const writeCsv = (name, rows) => { const headers = Object.keys(rows[0]); fs.writeFileSync(path.join(output, name), '\uFEFF' + [headers, ...rows.map(r => headers.map(h => r[h]))].map(row => row.map(quote).join(',')).join('\r\n')); };
  writeCsv('heart_recovery_comparison.csv', heartRows);
  writeCsv('spade_k_comparison.csv', spadeRows);
  writeCsv('unknown_survival_comparison.csv', unknownRows);
  writeCsv('joker_target_statistics.csv', [...jokerGroups.values()]);
  const unresolved = summary.cohorts.flatMap(cohort => cohort.matches
    .filter(match => match.status !== 'completed')
    .map(match => ({ cohort: cohort.name, match: match.match, conditionID: match.conditionID,
      swapID: match.swapID, map: match.map, status: match.status, reason: match.reason,
      turns: match.turns, firstHP: match.firstHP, secondHP: match.secondHP,
      firstDeck: match.firstDeck, secondDeck: match.secondDeck,
      firstBoard: match.firstBoard, secondBoard: match.secondBoard,
      firstHandIDs: match.firstHandIDs, secondHandIDs: match.secondHandIDs })));
  const result = { schemaVersion: '2.0', experiment: root, completed: true, checksPassed: passed.length, heartRows, spadeOutcomes: events, unknownRows, cohortSummary: summary.cohorts.map(({ matches, ...other }) => other), unresolved, note: 'Completed games only in all effect and target statistics. Paired observations are dependent; screening thresholds are not final balance validation. Exact 64-bit RNG fingerprints remain in the original C# exports.' };
  fs.writeFileSync(path.join(output, 'AnalysisAudit.json'), JSON.stringify(result, null, 2));
  fs.writeFileSync(path.join(output, 'AuditChecks.json'), JSON.stringify(passed, null, 2));
  console.log(JSON.stringify({ checksPassed: passed.length, heartMain: heartRows.filter(r => r.cohort.endsWith('_main')), spadeOutcomes: events, jokerGroups: jokerGroups.size }));
}
analyzeEvents().catch(error => { console.error(error); process.exitCode = 1; });
