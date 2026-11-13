/* Shared offline inventory rules. No network or platform dependencies. */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object') module.exports = api;
  else root.StockCore = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';
  const MAX_ITEMS = 5000, MAX_RECORDS = 10000;
  const number = v => { const n = Number(v); if (!Number.isFinite(n) || n < 0 || n > 100000000 || Math.abs(n * 1000 - Math.round(n * 1000)) > 0.0001) throw Error('Use a non-negative quantity with up to 3 decimal places.'); return n; };
  const text = (v, max, label) => { if (typeof v !== 'string' || !v.trim() || v.length > max || v !== v.trim()) throw Error(label + ' is required (maximum ' + max + ' characters).'); return v.trim(); };
  const stable = v => JSON.stringify(v, function(k,x) { return x && typeof x === 'object' && !Array.isArray(x) ? Object.fromEntries(Object.keys(x).sort().map(k => [k,x[k]])) : x; });
  const round = n => Math.round(n * 1000) / 1000;
  function balances(doc) {
    const result = Object.fromEntries(doc.items.map(i => [i.sku, i.openingQuantity]));
    for (const m of doc.movements) {
      if (!(m.sku in result)) throw Error('Record refers to an unknown item.');
      result[m.sku] = round(result[m.sku] + (m.kind === 'receive' ? (['damaged','quarantined'].includes(m.condition) ? 0 : m.quantity) : -m.quantity));
      if (result[m.sku] < 0) throw Error('Insufficient stock for ' + m.sku + '.');
    }
    return result;
  }
  function validate(doc, previous) {
    if (!doc || doc.schema !== 1 || !Number.isInteger(doc.revision) || doc.revision < 0 || !Array.isArray(doc.items) || !Array.isArray(doc.movements) || doc.items.length > MAX_ITEMS || doc.movements.length > MAX_RECORDS) throw Error('Unsupported inventory file or size.');
    const codes = new Set(), ids = new Set();
    for (const i of doc.items) {
      text(i.sku, 50, 'Item code'); text(i.name, 255, 'Item name'); text(i.category, 80, 'Category'); text(i.unit, 20, 'Unit');
      if (typeof i.barcode !== 'string' || i.barcode.length > 200) throw Error('Invalid barcode / NFC ID.');
      number(i.openingQuantity); number(i.reorderLevel);
      if (codes.has(i.sku.toLowerCase())) throw Error('Duplicate item code: ' + i.sku);
      codes.add(i.sku.toLowerCase());
    }
    for (const m of doc.movements) {
      text(m.id, 80, 'Record ID'); text(m.sku, 50, 'Item code'); text(m.signer, 120, 'Receiver / issuer'); text(m.location, 255, 'Location');
      text(m.reference, 100, 'Delivery / job reference');
      if (!['receive', 'issue'].includes(m.kind) || number(m.quantity) <= 0 || (m.kind === 'issue' && m.condition !== 'accepted') || !['accepted','damaged','short','quarantined'].includes(m.condition) || typeof m.notes !== 'string' || m.notes.length > 1000 || (m.condition !== 'accepted' && !m.notes.trim())) throw Error('Invalid goods record.');
      if (!m.acknowledged || !/^data:image\/png;base64,[A-Za-z0-9+/=]+$/.test(m.signature) || m.signature.length > 150000 || m.signature.length < 150) throw Error('Draw a signature and acknowledge the goods record.');
      if (!Number.isFinite(Date.parse(m.recordedAt)) || !Number.isFinite(Date.parse(m.occurredAt))) throw Error('Invalid record time.');
      if (m.coordinates !== null && (!Number.isFinite(m.coordinates.latitude) || Math.abs(m.coordinates.latitude) > 90 || !Number.isFinite(m.coordinates.longitude) || Math.abs(m.coordinates.longitude) > 180 || !Number.isFinite(m.coordinates.accuracy) || m.coordinates.accuracy < 0)) throw Error('Invalid GPS location.');
      if (ids.has(m.id)) throw Error('Duplicate goods record ID.');
      ids.add(m.id);
    }
    if (previous) {
      if (doc.revision !== previous.revision) throw Error('Inventory changed. Reload before saving.');
      const next = new Map(doc.movements.map(m => [m.id, stable(m)]));
      for (const m of previous.movements) if (next.get(m.id) !== stable(m)) throw Error('Signed records cannot be changed or deleted. Add a correcting record.');
      for (const i of previous.items) {
        const n = doc.items.find(x => x.sku === i.sku);
        if (!n) throw Error('Existing items cannot be removed by an import.');
        if (previous.movements.some(m => m.sku === i.sku) && (n.openingQuantity !== i.openingQuantity || n.unit !== i.unit)) throw Error('Opening quantity / unit is locked after signing a movement.');
      }
    }
    balances(doc);
    if (JSON.stringify(doc).length > 20000000) throw Error('Workspace exceeds 20 MB. Export and archive before adding more records.');
    return doc;
  }
  function parseCsv(input) {
    if (input.length > 2000000) throw Error('CSV exceeds 2 MB.');
    input = input.replace(/^\uFEFF/, '');
    const rows = []; let row = [], cell = '', quoted = false, afterQuote = false;
    for (let x = 0; x <= input.length; x++) {
      const c = input[x];
      if (quoted) {
        if (c === undefined) throw Error('Unclosed CSV quote.');
        if (c === '"') { if (input[x + 1] === '"') { cell += '"'; x++; } else { quoted = false; afterQuote = true; } }
        else cell += c;
      } else if (c === ',' || c === '\n' || c === undefined) {
        row.push(cell.replace(/\r$/, '')); cell = ''; afterQuote = false;
        if (c !== ',') { if (row.some(v => v !== '')) rows.push(row); row = []; }
      } else if (c === '"' && !cell && !afterQuote) quoted = true;
      else if (afterQuote && c !== '\r') throw Error('Invalid CSV text after a quote.');
      else cell += c;
    }
    if (!rows.length) throw Error('CSV is empty.');
    const headers = rows.shift().map(h => h.trim());
    const required = ['sku','name','category','unit','barcode','openingQuantity','reorderLevel'];
    if (required.some(h => !headers.includes(h)) || new Set(headers).size !== headers.length) throw Error('Use the CSV template headers: ' + required.join(', '));
    return rows.map(r => { if (r.length !== headers.length) throw Error('CSV column count differs from the header.'); const o = Object.fromEntries(headers.map((h, n) => [h, r[n]])); required.slice(0,5).forEach(h => { if (o[h].startsWith("'")) o[h] = o[h].slice(1); }); o.openingQuantity = number(o.openingQuantity); o.reorderLevel = number(o.reorderLevel); return Object.fromEntries(required.map(h => [h,o[h]])); });
  }
  function csv(items) {
    const headers = ['sku','name','category','unit','barcode','openingQuantity','reorderLevel'];
    const safe = v => { let s = String(v); if (/^[=+\-@\t\r']/.test(s)) s = "'" + s; return '"' + s.replace(/"/g, '""') + '"'; };
    return headers.join(',') + '\r\n' + items.map(i => headers.map(h => safe(i[h])).join(',')).join('\r\n');
  }
  function merge(doc, incoming) {
    const next = JSON.parse(JSON.stringify(doc));
    if (Array.isArray(incoming)) incoming = { items: incoming, movements: [] };
    if (!incoming || !Array.isArray(incoming.items) || !Array.isArray(incoming.movements)) throw Error('Choose a SiteSync JSON backup or CSV template.');
    let added = 0, skipped = 0;
    for (const item of incoming.items) {
      const existing = next.items.find(i => i.sku.toLowerCase() === String(item.sku).toLowerCase());
      if (existing) { if (stable(existing) !== stable(item)) skipped++; }
      else { next.items.push(item); added++; }
    }
    for (const m of incoming.movements) {
      const existing = next.movements.find(v => v.id === m.id);
      if (existing && JSON.stringify(existing) !== stable(m)) throw Error('A signed record ID conflicts with this backup.');
      if (!existing) next.movements.push(m);
    }
    validate(next, doc);
    return { document: next, added: added, skipped: skipped };
  }
  return { validate: validate, balances: balances, csv: csv, parseCsv: parseCsv, merge: merge, round: round };
});
