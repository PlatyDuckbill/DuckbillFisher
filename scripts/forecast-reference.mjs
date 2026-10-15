// Development-only fixture generator. No runtime dependency on JavaScript.
// Weather target body comes from the pinned MIT source in THIRD-PARTY-NOTICES.txt.
export function generateForecastReference(dataset, audit, weatherSource) {
  const body = weatherSource.match(/calculateForecastTarget\(m\)\s*\{([\s\S]*?)\n  \}/)?.[1];
  if (!body) throw new Error('Pinned calculateForecastTarget source not found');
  const target = new Function('m', body);
  const periodMs = 1400000;
  const dayMs = 4200000;
  const hourMs = 175000;
  const base = Date.parse('2026-10-15T16:50:59.114Z');
  const spots = dataset.fish.map(rule => {
    const spot = audit.spots.find(spot => spot.id === rule.fishingSpotId);
    if (!spot) throw new Error(`Missing spot ${rule.fishingSpotId}`);
    return { fishParameterId: rule.fishParameterId, territoryId: spot.territoryId,
      rates: spot.rates.map(rate => ({ weatherId: rate.id, chance: rate.rate })) };
  });
  const weatherAt = (time, rates) => {
    const value = target(time);
    let threshold = 0;
    for (const rate of rates) {
      threshold += rate.chance;
      if (value < threshold) return rate.weatherId;
    }
    throw new Error('Invalid weather rates');
  };

  // Independent event sweep: partition at all ET and weather boundaries, evaluate
  // the predicate inside each segment, then join adjacent true segments.
  // This does not port Core's nested period/intersection search.
  const windowAt = (now, rule, rates) => {
    if (rule.startHour === 0 && rule.endHour === 24 &&
        rule.weatherIds.length === 0 && rule.previousWeatherIds.length === 0)
      return { kind: 'Always', start: null, end: null };
    const first = Math.floor(now / periodMs) * periodMs;
    const limit = first + 432 * periodMs;
    const boundaries = new Set([first, limit]);
    for (let t = first; t < limit; t += periodMs) boundaries.add(t);
    for (let d = Math.floor(first / dayMs) - 1; d <= Math.ceil(limit / dayMs); d++) {
      for (const h of [rule.startHour, rule.endHour]) {
        const t = d * dayMs + h * hourMs;
        if (t > first && t < limit) boundaries.add(t);
      }
    }
    const matches = time => {
      const hour = (time % dayMs) / hourMs;
      const timeOk = rule.startHour < rule.endHour
        ? hour >= rule.startHour && hour < rule.endHour
        : hour >= rule.startHour || hour < rule.endHour;
      const p = Math.floor(time / periodMs) * periodMs;
      return timeOk &&
        (!rule.weatherIds.length || rule.weatherIds.includes(weatherAt(p, rates))) &&
        (!rule.previousWeatherIds.length || rule.previousWeatherIds.includes(weatherAt(p - periodMs, rates)));
    };
    const points = [...boundaries].sort((a, b) => a - b);
    let start = null;
    for (let i = 0; i < points.length - 1; i++) {
      const a = points[i], b = points[i + 1];
      if (b <= now) continue;
      if (matches((a + b) / 2)) {
        if (start === null) start = a;
      } else if (start !== null) {
        return { kind: start <= now ? 'Open' : 'Upcoming', start, end: a };
      }
    }
    return start === null ? { kind: 'NotFound', start: null, end: null }
      : { kind: start <= now ? 'Open' : 'Upcoming', start, end: null };
  };
  const cases = [];
  for (let i = 0; i < dataset.fish.length; i++) {
    const rule = dataset.fish[i], rates = spots[i].rates;
    const times = new Set(Array.from({ length: 8 }, (_, day) => base + day * 86400000));
    const firstWindow = windowAt(base, rule, rates);
    for (const boundary of [firstWindow.start, firstWindow.end]) {
      if (boundary !== null) for (const delta of [-1, 0, 1]) times.add(boundary + delta);
    }
    for (const now of [...times].sort((a, b) => a - b))
      cases.push({ fishParameterId: rule.fishParameterId, now, ...windowAt(now, rule, rates) });
  }
  const firstPeriod = Math.floor(base / periodMs) * periodMs;
  return { sourceRevision: dataset.sourceRevision, gameBuild: dataset.gameBuild,
    weather: { firstPeriodSeconds: firstPeriod / 1000,
      targets: Array.from({ length: 434 }, (_, i) => target(firstPeriod + (i - 1) * periodMs)) },
    spots, cases };
}

// Node CLI (optional developer tool; not required by the .NET test suite).
const fs = await import('node:fs');
const [datasetPath, auditPath, sourcePath, outputPath] = process.argv.slice(2);
if (!outputPath) throw new Error('Usage: node scripts/forecast-reference.mjs dataset.json kr-audit.json weather.js output.json');
const read = path => JSON.parse(fs.readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const result = generateForecastReference(read(datasetPath), read(auditPath), fs.readFileSync(sourcePath, 'utf8'));
fs.writeFileSync(outputPath, JSON.stringify(result, null, 2) + '\n');
