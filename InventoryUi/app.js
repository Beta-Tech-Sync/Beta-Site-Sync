/* One workflow, with replaceable native, authenticated web and local-demo adapters. */
(async function () {
'use strict';
const $ = id => document.getElementById(id), C = StockCore;
let state, token = null, editing = null, coordinates = null, signed = false, stream = null, scanner = null, pending = new Map();
const hybrid = () => window.hybridWebViewHost || (window.chrome && window.chrome.webview) || (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.webwindowinterop);
const native = () => !!window.StockNative || !!hybrid();
const demo = !native() && !location.pathname.startsWith('/workspace');
const copy = v => JSON.parse(JSON.stringify(v));
const tell = s => { $('message').textContent = s; };
const fail = e => tell(e.message || String(e));
window.stockNativeResult = function (result) { const p = pending.get(result.id); if (!p) return; pending.delete(result.id); result.error ? p.reject(Error(result.error)) : p.resolve(result.value); };
function callNative(action, payload) {
 return new Promise((resolve,reject) => {
  const id = crypto.randomUUID ? crypto.randomUUID() : String(Date.now()) + Math.random();
  pending.set(id,{resolve:resolve,reject:reject});
  const message = JSON.stringify({id:id,action:action,payload:payload});
  if (window.StockNative) window.StockNative.postMessage(message);
  else if (window.hybridWebViewHost) window.hybridWebViewHost.sendMessage('__RawMessage|' + message);
  else if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage('__RawMessage|' + message);
  else window.webkit.messageHandlers.webwindowinterop.postMessage('__RawMessage|' + message);
  setTimeout(() => { if (pending.has(id)) { pending.delete(id); reject(Error('Action timed out. No change has been confirmed; reload before retrying.')); } },120000);
 });
}
async function request(path, method, body) {
 const r = await fetch(path,{method:method || 'GET',headers:{Authorization:'Bearer '+token,'Content-Type':'application/json'},body:body === undefined ? undefined : JSON.stringify(body)});
 if (!r.ok) { let error; try { error=await r.json(); } catch {} throw Error(error && (error.detail || error.error) || (r.status === 401 ? 'Sign in again to continue.' : 'Request failed: '+r.status)); }
 return r.json();
}
const adapter = {
 async load() {
  if (native()) return callNative('load');
  if (!demo) return request('/api/workspace');
  const raw=localStorage.getItem('sitesync.workspace.v1');
  if(raw) return JSON.parse(raw);
  return {schema:1,revision:0,items:await (await fetch('catalog.json')).json(),movements:[]};
 },
 async save(next) {
  C.validate(next,state);
  if (native()) return callNative('save',next);
  if (!demo) return request('/api/workspace','PUT',next);
  const current=localStorage.getItem('sitesync.workspace.v1');
  if(current && JSON.parse(current).revision !== next.revision) throw Error('Another tab changed this inventory. Reload.');
  const saved=copy(next);saved.revision++;
  localStorage.setItem('sitesync.workspace.v1',JSON.stringify(saved));return saved;
 },
 async file(name,type,contents,share) {
  if (native()) return callNative('export',{name:name,type:type,contents:contents,share:!!share});
  const file=new File([contents],name,{type:type});
  if(share && navigator.canShare && navigator.canShare({files:[file]})) { await navigator.share({files:[file],title:'SiteSync inventory'});return; }
  if(share) tell('File sharing is unavailable in this browser. Downloading the file so you can attach it in your chosen app.');
  const url=URL.createObjectURL(file),a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
 }
};
async function save(next,msg) { state=await adapter.save(next);C.validate(state);render();tell(msg); }
async function load() { state=await adapter.load();C.validate(state);$('workspace').hidden=false;$('login').hidden=true;render();tell('Ready. Saved changes stay in the workspace shown above.'); }
function button(text,fn) {const b=document.createElement('button');b.type='button';b.textContent=text;b.onclick=()=>Promise.resolve().then(fn).catch(fail);return b;}

function receiptHtml(m) {
 const escape = v => String(v).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 const item=state.items.find(i=>i.sku===m.sku);
 const fields=[['Record',m.id],['Movement',m.kind],['Item',m.sku+' - '+item.name],['Quantity',m.quantity+' '+item.unit],['Reference',m.reference],['Condition',m.condition],['Accepted by',m.signer],['Location',m.location],['Goods time',new Date(m.occurredAt).toLocaleString()],['Recorded time',new Date(m.recordedAt).toISOString()],['Notes',m.notes||'None']];
 if(m.coordinates)fields.push(['GPS',m.coordinates.latitude+', '+m.coordinates.longitude+' (accuracy '+m.coordinates.accuracy+' m)']);
 return '<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>SiteSync goods receipt</title></head><body style="font-family:system-ui;max-width:800px;margin:32px auto;padding:20px;color:#12334b"><h1>SiteSync goods receipt</h1><table style="width:100%;text-align:left">'+fields.map(f=>'<tr><th style="padding:8px">'+escape(f[0])+'</th><td style="padding:8px">'+escape(f[1])+'</td></tr>').join('')+'</table><h2>Acknowledgement signature</h2><img alt="Acknowledgement signature" style="max-width:100%;width:500px" src="'+m.signature+'"><p>The named signer acknowledged the item, actual quantity, condition and location recorded above. This is a record copy, not a safety certification or verified digital identity.</p><p>Print this page to save a PDF copy.</p></body></html>';
}

function render() {
 const balances=C.balances(state),category=$('category').value,query=$('search').value.toLowerCase();
 $('skuCount').textContent=state.items.length;$('lowCount').textContent=state.items.filter(i=>balances[i.sku]<i.reorderLevel).length;$('recordCount').textContent=state.movements.length;
 const categories=[...new Set(state.items.map(i=>i.category))].sort();$('category').replaceChildren(new Option('All categories',''),...categories.map(c=>new Option(c,c)));$('category').value=categories.includes(category)?category:'';
 const rows=state.items.filter(i=>(!category||i.category===category)&&[i.name,i.sku,i.barcode,i.category].join(' ').toLowerCase().includes(query));
 $('items').replaceChildren(...rows.map(i=>{
  const tr=document.createElement('tr'),name=document.createElement('td');name.textContent=i.name;const small=document.createElement('small');small.textContent=i.sku+(i.barcode?' · '+i.barcode:'');name.append(small);tr.append(name);
  [i.category,balances[i.sku],i.unit].forEach((v,n)=>{const td=document.createElement('td');td.textContent=v;if(n===1&&balances[i.sku]<i.reorderLevel)td.className='low';tr.append(td);});
  const actions=document.createElement('td');actions.append(button('Edit',()=>openItem(i)),button('Goods',()=>openMovement(i.sku)));tr.append(actions);return tr;
 }));
 $('records').replaceChildren(...state.movements.slice().reverse().slice(0,100).map(m=>{
  const div=document.createElement('article');div.className='record';const info=document.createElement('div'),strong=document.createElement('strong');strong.textContent=(m.kind==='receive'?'Received ':'Issued ')+m.quantity+' · '+m.sku;info.append(strong);
  [m.reference+' · '+m.condition,m.signer+' · '+m.location,'Goods time: '+new Date(m.occurredAt).toLocaleString(),'Recorded: '+new Date(m.recordedAt).toLocaleString(),m.notes].filter(Boolean).forEach(v=>{const p=document.createElement('p');p.textContent=v;info.append(p);});
  if(m.coordinates){const p=document.createElement('p');p.textContent='GPS '+m.coordinates.latitude.toFixed(5)+', '+m.coordinates.longitude.toFixed(5)+' ±'+Math.round(m.coordinates.accuracy)+' m';info.append(p);}
  const img=document.createElement('img');img.src=m.signature;img.alt='Acknowledgement signature by '+m.signer;div.append(info,img,button('Share receipt',()=>{if(!confirm('Share this receipt, including the signature, signer name and location, with your chosen recipient?'))return;return adapter.file('goods-'+m.id+'.html','text/html',receiptHtml(m),true);}));return div;
 }));
}
function openItem(item) {
 editing=item?item.sku:null;const f=$('itemForm');f.reset();
 if(item)Object.keys(item).forEach(k=>{if(f.elements[k])f.elements[k].value=item[k];});
 f.elements.sku.readOnly=!!item;
 const locked=item&&state.movements.some(m=>m.sku===item.sku);f.elements.openingQuantity.readOnly=!!locked;f.elements.unit.readOnly=!!locked;
 $('itemDialog').showModal();
}
function openMovement(sku) {
 const f=$('movementForm');f.reset();coordinates=null;signed=false;$('gps').textContent='GPS is optional. Enter the site location even when GPS is unavailable.';
 f.elements.sku.replaceChildren(...state.items.map(i=>new Option(i.sku+' · '+i.name,i.sku)));if(sku)f.elements.sku.value=sku;
 const now=new Date();f.elements.occurredAt.value=new Date(now-now.getTimezoneOffset()*60000).toISOString().slice(0,16);
 context.clearRect(0,0,640,200);$('movementDialog').showModal();
}
const canvas=$('signature'),context=canvas.getContext('2d');context.lineWidth=3;context.lineCap='round';context.strokeStyle='#15364a';let drawing=false,points=0;
function point(e){const r=canvas.getBoundingClientRect();return [(e.clientX-r.left)*640/r.width,(e.clientY-r.top)*200/r.height];}
canvas.onpointerdown=e=>{drawing=true;points=0;canvas.setPointerCapture(e.pointerId);context.beginPath();context.moveTo(...point(e));};
canvas.onpointermove=e=>{if(!drawing)return;context.lineTo(...point(e));context.stroke();points++;if(points>3)signed=true;};
canvas.onpointerup=()=>drawing=false;canvas.onpointercancel=()=>drawing=false;
$('clear').onclick=()=>{context.clearRect(0,0,640,200);signed=false;};
$('itemForm').onsubmit=async e=>{e.preventDefault();try{
 const values=Object.fromEntries(new FormData(e.target));const item={sku:values.sku.trim(),name:values.name.trim(),category:values.category.trim(),unit:values.unit.trim(),barcode:values.barcode.trim(),openingQuantity:Number(values.openingQuantity),reorderLevel:Number(values.reorderLevel)};
 const next=copy(state);if(editing)next.items[next.items.findIndex(i=>i.sku===editing)]=item;else next.items.push(item);
 await save(next,'Stock line saved.');$('itemDialog').close();
 }catch(e){fail(e);alert(e.message);}};
$('movementForm').onsubmit=async e=>{e.preventDefault();const b=$('saveMovement');b.disabled=true;try{
 if(!signed)throw Error('Draw the accepting person’s signature first.');
 const v=Object.fromEntries(new FormData(e.target)),next=copy(state),m={id:crypto.randomUUID(),sku:v.sku,kind:v.kind,quantity:Number(v.quantity),reference:v.reference.trim(),condition:v.condition,signer:v.signer.trim(),location:v.location.trim(),coordinates:coordinates,occurredAt:new Date(v.occurredAt).toISOString(),recordedAt:new Date().toISOString(),notes:v.notes.trim(),acknowledged:true,signature:canvas.toDataURL('image/png')};
 next.movements.push(m);await save(next,'Signed goods record saved.');$('movementDialog').close();
 }catch(e){fail(e);alert(e.message);}finally{b.disabled=false;}};
$('location').onclick=async()=>{try{
 coordinates=native()?await callNative('location'):await new Promise((resolve,reject)=>navigator.geolocation.getCurrentPosition(p=>resolve({latitude:p.coords.latitude,longitude:p.coords.longitude,accuracy:p.coords.accuracy}),reject,{enableHighAccuracy:true,timeout:20000,maximumAge:0}));
 $('gps').textContent='GPS attached: '+coordinates.latitude.toFixed(5)+', '+coordinates.longitude.toFixed(5)+' ±'+Math.round(coordinates.accuracy)+' m. Only saved with this signed record.';
 }catch(e){coordinates=null;fail(e);$('gps').textContent='GPS unavailable. Enter the handover location manually.';}};
$('search').oninput=render;$('category').onchange=render;
$('add').onclick=()=>openItem();$('movement').onclick=()=>openMovement();$('reload').onclick=()=>load().catch(fail);
document.querySelectorAll('[data-close]').forEach(b=>b.onclick=()=>b.closest('dialog').close());
$('scanDialog').onclose=()=>{if(stream)stream.getTracks().forEach(t=>t.stop());stream=null;};
function scanned(value){$('search').value=value;const item=state.items.find(i=>i.sku===value||i.barcode===value);render();if(item)openMovement(item.sku);else tell('Code read: '+value+'. No matching item yet. Add an item and place this code in its barcode / NFC ID field.');}
$('scan').onclick=async()=>{try{
 if(native()){scanned(await callNative('scan'));return;}
 if(!('BarcodeDetector' in window))throw Error('Camera decoding is unavailable in this browser. Use an Android app or a USB/Bluetooth scanner, or enter the code.');
 const detector=new BarcodeDetector();stream=await navigator.mediaDevices.getUserMedia({video:{facingMode:{ideal:'environment'}},audio:false});$('camera').srcObject=stream;$('scanDialog').showModal();
 const detect=async()=>{if(!stream)return;try{const codes=await detector.detect($('camera'));if(codes.length){$('scanDialog').close();scanned(codes[0].rawValue);return;}}catch{}requestAnimationFrame(detect);};detect();
 }catch(e){if(stream)stream.getTracks().forEach(t=>t.stop());stream=null;fail(e);}};
$('nfc').onclick=async()=>{try{
 if(native()){tell('Hold a compatible NFC tag near the phone.');scanned(await callNative('nfc'));return;}
 if(!('NDEFReader' in window))throw Error('Web NFC is unavailable. Use an NFC-capable Android app/Chrome, or an external reader in keyboard mode. UHF RFID needs a separate reader.');
 const abort=new AbortController(),reader=new NDEFReader();await reader.scan({signal:abort.signal});tell('Hold a compatible NFC tag near the phone.');
 reader.onreading=e=>{abort.abort();const id=e.serialNumber.replace(/:/g,'').toUpperCase();if(!id)fail(Error('This NFC tag did not expose an ID.'));else scanned(id);};setTimeout(()=>abort.abort(),30000);
 }catch(e){fail(e);}};
$('export').onclick=()=>$('exportDialog').showModal();
const stamp=()=>new Date().toISOString().slice(0,10);
$('json').onclick=()=>adapter.file('sitesync-'+stamp()+'.json','application/json',JSON.stringify(state,null,2),false).catch(fail);
$('share').onclick=()=>adapter.file('sitesync-'+stamp()+'.json','application/json',JSON.stringify(state,null,2),true).catch(fail);
$('csv').onclick=()=>{const b=C.balances(state);adapter.file('stock-'+stamp()+'.csv','text/csv',C.csv(state.items.map(i=>Object.assign({},i,{openingQuantity:b[i.sku]}))),false).catch(fail);};
$('template').onclick=()=>adapter.file('stock-template.csv','text/csv',C.csv([{sku:'YOUR-CODE',name:'Your stock item',category:'Your category',unit:'pcs',barcode:'',openingQuantity:0,reorderLevel:0}]),false).catch(fail);
async function importText(text) {const trimmed=text.trim(),incoming=trimmed.startsWith('{')?JSON.parse(trimmed):C.parseCsv(text),merge=C.merge(state,incoming);
 if(!confirm('Add '+merge.added+' new stock lines and merge new signed records? '+merge.skipped+' existing definitions differ and will be kept unchanged.'))return;
 await save(merge.document,'Import complete. Existing stock definitions and signed records were preserved.');
}
$('import').onclick=async()=>{try{if(native()){const text=await callNative('import');if(text)await importText(text);}else $('file').click();}catch(e){fail(e);}};
$('file').onchange=async e=>{try{const f=e.target.files[0];if(!f)return;if(f.size>20000000)throw Error('Import file exceeds 20 MB.');await importText(await f.text());}catch(e){fail(e);}finally{e.target.value='';}};
$('loginForm').onsubmit=async e=>{e.preventDefault();try{const v=Object.fromEntries(new FormData(e.target)),r=await fetch('/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(v)});if(!r.ok)throw Error('Sign-in failed. Check the email and password.');token=(await r.json()).accessToken;e.target.elements.password.value='';$('logout').hidden=false;await load();}catch(e){fail(e);}};
$('register').onclick=async()=>{try{const f=$('loginForm'),v=Object.fromEntries(new FormData(f));if(!f.reportValidity())return;const r=await fetch('/api/auth/register',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(v)});if(!r.ok)throw Error('Could not create account. Use a unique email and a password of at least 12 characters including upper/lower case, a number and a symbol.');tell('Account created. Sign in to open your private client inventory.');}catch(e){fail(e);}};
$('logout').onclick=()=>{token=null;state=null;$('workspace').hidden=true;$('login').hidden=false;$('logout').hidden=true;$('items').replaceChildren();$('records').replaceChildren();tell('Signed out.');};
$('storage').textContent=native()?'Private device workspace · share/export to transfer records':demo?'Local browser workspace · not a shared server · export a backup before clearing browser data':'Private client web workspace · saved to your signed-in account';
if(!demo&&!native()){$('login').hidden=false;tell('Sign in to access your own inventory.');}else{try{await load();}catch(e){fail(e);}}
})();
