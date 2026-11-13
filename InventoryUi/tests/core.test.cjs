const {test}=require('node:test'),assert=require('node:assert/strict'),C=require('../core.js');
const item=()=>({sku:'BOLT-1',name:'M12 bolts',category:'Fixings',unit:'box',barcode:'ABC',openingQuantity:5,reorderLevel:1});
const doc=()=>({schema:1,revision:0,items:[item()],movements:[]});
const movement=(overrides={})=>Object.assign({id:'one',sku:'BOLT-1',kind:'issue',quantity:2,reference:'ORDER-1',condition:'accepted',signer:'Test receiver',location:'Test yard',coordinates:null,occurredAt:new Date().toISOString(),recordedAt:new Date().toISOString(),notes:'',acknowledged:true,signature:'data:image/png;base64,'+'A'.repeat(180)},overrides);
test('CSV preserves commas, quotes, newlines and leading apostrophes',()=>{const i=item();i.name="\u0027special, bolts\n\"quoted\"";assert.deepEqual(C.parseCsv(C.csv([i])),[i]);});
test('spreadsheet formula text is escaped without losing original data on import',()=>{const i=item();i.name='=HYPERLINK("bad")';assert.match(C.csv([i]),/\u0027=HYPERLINK/);assert.equal(C.parseCsv(C.csv([i]))[0].name,i.name);});
test('CSV rejects malformed quotes and wrong columns',()=>{assert.throws(()=>C.parseCsv('sku,name\n"x,y'));assert.throws(()=>C.parseCsv('sku,name,category,unit,barcode,openingQuantity,reorderLevel\na,b'));});
test('issues cannot make stock negative',()=>{const d=doc();d.movements.push(movement({quantity:6}));assert.throws(()=>C.validate(d),/Insufficient/);});
test('quarantined stock cannot be issued as available stock',()=>{const d=doc();d.items[0].openingQuantity=0;d.movements.push(movement({kind:'receive',condition:'quarantined',quantity:10,notes:'Held for inspection'}));assert.equal(C.balances(d)['BOLT-1'],0);d.movements.push(movement({id:'two',quantity:1}));assert.throws(()=>C.validate(d));});
test('signed records and opening balance are preserved',()=>{const old=doc();old.movements.push(movement());const next=structuredClone(old);next.movements[0].quantity=1;assert.throws(()=>C.validate(next,old),/cannot be changed/);const n=structuredClone(old);n.items[0].openingQuantity=9;assert.throws(()=>C.validate(n,old),/locked/);});
test('imports add own stock without overwriting duplicate codes',()=>{const old=doc(),i=item();i.name='Changed';const other={...item(),sku:'PAINT-2',name:'Paint'};const merged=C.merge(old,[i,other]);assert.equal(merged.added,1);assert.equal(merged.skipped,1);assert.equal(merged.document.items[0].name,'M12 bolts');assert.equal(old.items.length,1);});
test('stale revisions and invalid GPS are rejected',()=>{const old=doc(),n=doc();n.revision=3;assert.throws(()=>C.validate(n,old));const d=doc();d.movements.push(movement({coordinates:{latitude:300,longitude:1,accuracy:1}}));assert.throws(()=>C.validate(d),/GPS/);});
test('72 catalogue definitions have unique codes and no invented stock',()=>{const catalog=require('../catalog.json');C.validate({schema:1,revision:0,items:catalog,movements:[]});assert.equal(catalog.length,72);assert.ok(catalog.every(i=>i.openingQuantity===0));});

test('MAUI entry includes the supported framework bridge before inventory startup',()=>{
 const fs=require('node:fs'),path=require('node:path');
 const root=path.join(__dirname,'..');
 const html=fs.readFileSync(path.join(root,'maui.html'),'utf8');
 assert.ok(html.indexOf('_framework/hybridwebview.js')<html.indexOf('app.js'));
 assert.equal(html.replace('<script src="_framework/hybridwebview.js"></script>',''),fs.readFileSync(path.join(root,'index.html'),'utf8'));
});
