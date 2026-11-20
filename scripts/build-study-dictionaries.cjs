/* Build from https://kaikki.org/zhwiktionary/rawdata.html . Usage: node scripts/build-study-dictionaries.cjs dump.jsonl.gz output-dir */
const fs = require('node:fs');
const path = require('node:path');
const { createGunzip } = require('node:zlib');
const { createInterface } = require('node:readline');
const [source, out] = process.argv.slice(2);
const dictionaries = new Map(['ja','ko','fr','de','es'].map(code => [code,new Map()]));
const unique = list => [...new Set(list.filter(Boolean))];
const attribution = '中文维基词典贡献者 · Wiktextract / kaikki.org · CC BY-SA 4.0 https://creativecommons.org/licenses/by-sa/4.0/ · 已整理格式和词形索引';
async function build() {
  const lines = createInterface({input: fs.createReadStream(source).pipe(createGunzip()), crlfDelay:Infinity});
  for await (const line of lines) {
    const x = JSON.parse(line), dict = dictionaries.get(x.lang_code);
    if (!dict || !x.word) continue;
    const glosses = (x.senses || []).flatMap(s => s.glosses || []);
    if (!glosses.length) continue;
    let item = dict.get(x.word);
    if (!item) dict.set(x.word,item = {Headword:x.word, Reading:[], Meaning:[], Example:[], Forms:[]});
    const pos = [x.pos_title,x.pos].find(p => p && p !== 'unknown');
    item.Meaning.push(...glosses.map(g => pos ? pos+' · '+g : g));
    item.Reading.push(...(x.sounds || []).map(s => s.ipa || s.roman), ...(x.forms || []).filter(f => (f.tags || []).includes('romanization')).map(f=>f.form));
    item.Forms.push(...(x.forms || []).map(f=>f.form));
    item.Example.push(...(x.senses || []).flatMap(s => (s.examples || []).map(ex => [ex.text,ex.translation].filter(Boolean).join('\n'))));
  }
  fs.mkdirSync(out,{recursive:true});
  for (const [code,dict] of dictionaries) {
    if (process.argv[4] && code !== process.argv[4]) continue;
    const curated = JSON.parse(fs.readFileSync(path.join(__dirname,"../study-packs",code+"-starter.json"))).Entries;
    const readings = new Map(curated.map(x => [x.Headword,x.Reading]));
    const records = [], index = new Map();
    const key = s => s.trim().normalize('NFC').toLowerCase();
    const add = item => {
      const pos = records.length;
      records.push(Buffer.from(JSON.stringify(item)));
      return pos;
    };
    const aliases = [];
    for (const item of dict.values()) {
      const forms = unique(item.Forms);
      const pos = add({...item, Reading:unique(item.Reading).join(' / ') || readings.get(item.Headword) || '',Meaning:unique(item.Meaning).join('\n'),Example:unique(item.Example).join('\n'),Forms:[]});
      const word = key(item.Headword); if (!index.has(word)) index.set(word,pos);
      for (const form of forms) aliases.push([key(form),pos]);
    }
    // Keep the curated beginner inflections available when absent from the upstream data.
    for (const x of curated) {
      const word = key(x.Headword);
      if (!index.has(word)) index.set(word,add(x));
      for (const form of x.Forms) aliases.push([key(form),index.get(word)]);
    }
    for (const [alias,pos] of aliases) if (alias && !index.has(alias)) index.set(alias,pos);
    const keys = [...index].map(([word,id])=>[Buffer.from(word),id]).sort((a,b)=>Buffer.compare(a[0],b[0]));
    const header = Buffer.alloc(18); header.write('YIKELEX1'+code); header.writeUInt32LE(keys.length,10); header.writeUInt32LE(records.length,14);
    const offsets = Buffer.alloc(4*keys.length), keyRows=[];
    let cursor=header.length+offsets.length;
    const dataStart=cursor+keys.reduce((a,[b])=>a+12+b.length,0);
    let at=dataStart; const positions=records.map(b=>{const p=at;at+=b.length;return p});
    for (let i=0;i<keys.length;i++) {
      const [word,id]=keys[i], row=Buffer.alloc(12); offsets.writeUInt32LE(cursor,i*4); row.writeUInt32LE(word.length);row.writeUInt32LE(positions[id],4);row.writeUInt32LE(records[id].length,8);
      keyRows.push(row,word);cursor+=12+word.length;
    }
    const file=path.join(out,code+'.ykdict');const stream=fs.createWriteStream(file);
    for (const part of [header,offsets,...keyRows,...records,Buffer.from('\n'+attribution+'\nhttps://kaikki.org/zhwiktionary/rawdata.html\n')]) {
      if (!stream.write(part)) await new Promise(resolve=>stream.once('drain',resolve));
    }
    await new Promise(resolve=>stream.end(resolve));console.log(code,dict.size,keys.length,fs.statSync(file).size);
  }
}
build().catch(e=>{console.error(e);process.exitCode=1});
