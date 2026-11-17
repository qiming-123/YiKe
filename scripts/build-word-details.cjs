// Build-only tool: preserve pinned source identity and seekable dictionary layout.
const fs = require('fs'), crypto = require('crypto');
const root = require('path').resolve(__dirname, '..');
const source = fs.readFileSync(process.argv[2]);
const lock = JSON.parse(fs.readFileSync(root + '/assets/word-dictionary-source.json', 'utf8').replace(/^\uFEFF/, ''));
if (crypto.createHash('sha256').update(source).digest('hex') !== lock.csvSha256) throw Error('Dictionary source hash mismatch');
const records = new Map(); let field='', row=[], quoted=false, headers;
function flush() {
 if (!headers) { headers=row; row=[]; return; }
 const r=Object.fromEntries(headers.map((key,i)=>[key,row[i]||''])); row=[];
 const key=r.word.trim().toLowerCase(), translation=r.translation.replace(/\\n/g,'\n').replace(/\0/g,'').trim();
 if(!/^[a-z][a-z0-9 _+./'-]{0,127}$/.test(key)||!/[\u3400-\u9fff\uf900-\ufaff]/.test(translation))return;
 const value=JSON.stringify({translation:translation.slice(0,1800),phonetic:r.phonetic.slice(0,200),definition:r.definition.replace(/\\n/g,'\n').slice(0,600),exchange:r.exchange.slice(0,400)});
 if(Buffer.byteLength(value)>8192)throw Error('Entry too large: '+key);
 if(!records.has(key))records.set(key,value);
}
const csv=source.toString('utf8').replace(/^\uFEFF/,'');
for(let i=0;i<csv.length;i++) { const c=csv[i]; if(c==='"'){if(quoted&&csv[i+1]==='"'){field+='"';i++;}else quoted=!quoted;}else if(c===','&&!quoted){row.push(field);field='';}else if(c==='\n'&&!quoted){row.push(field.replace(/\r$/,''));field='';flush();}else field+=c; }
if(field.length||row.length){row.push(field);flush();}
const items=[...records].sort(([a],[b])=>a<b?-1:a>b?1:0), header=Buffer.alloc(12+4*(items.length+1));
header.write('YIKEDIC2');header.writeUInt32LE(items.length,8);
const out=root+'/assets/word-dictionary.dat', fd=fs.openSync(out,'w');fs.writeSync(fd,header);let position=header.length;
items.forEach(([key,value],i)=>{header.writeUInt32LE(position,12+i*4);const k=Buffer.from(key,'ascii'),v=Buffer.from(value),record=Buffer.alloc(4+k.length+v.length);record.writeUInt16LE(k.length);k.copy(record,2);record.writeUInt16LE(v.length,2+k.length);v.copy(record,4+k.length);fs.writeSync(fd,record);position+=record.length;});
header.writeUInt32LE(position,12+items.length*4);fs.writeSync(fd,header,0,header.length,0);fs.closeSync(fd);
fs.writeFileSync(root+'/assets/word-dictionary.json',JSON.stringify({format:'YIKEDIC2',entries:items.length,bytes:position,commit:lock.commit,sourceCsvSha256:lock.csvSha256,sha256:crypto.createHash('sha256').update(fs.readFileSync(out)).digest('hex'),license:'MIT'},null,2)+'\n');
console.log(items.length+' structured entries');
