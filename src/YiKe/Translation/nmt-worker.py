"""Private, isolated translation worker. One request, no network or disk text cache."""
import json, sys, re
from pathlib import Path
sys.stdin.reconfigure(encoding='utf-8');sys.stdout.reconfigure(encoding='utf-8')
def run():
    import ctranslate2
    import sentencepiece as spm
    root=Path(__file__).resolve().parent/'model'
    request=json.loads(sys.stdin.read(32768)); text=request['text']
    if not isinstance(text,str) or not text.strip() or len(text)>4000: raise ValueError('invalid-text')
    source=spm.SentencePieceProcessor(model_file=str(root/'source.spm'))
    target=spm.SentencePieceProcessor(model_file=str(root/'target.spm'))
    translator=ctranslate2.Translator(str(root),device='cpu',compute_type='int8',inter_threads=1,intra_threads=2)
    answer=[]
    for part in re.split(r'(\r\n|\r|\n)',text):
        if not part.strip(): answer.append(part); continue
        tokens=['>>cmn_Hans<<']+source.encode(part,out_type=str)
        if len(tokens)>512: raise ValueError('sentence-too-long')
        result=translator.translate_batch([tokens],beam_size=4,max_input_length=0,max_decoding_length=1024,return_end_token=True)[0]
        output=result.hypotheses[0]
        if not output or output[-1]!='</s>': raise ValueError('incomplete')
        translated=target.decode(output[:-1])
        # OPUS training data can contain Qt UI-context suffixes, not source content.
        if '@' not in part: translated=re.sub(r'\s*@\s*(?:label|info|action|title)(?::[^\s]+)?\s*$', '', translated)
        answer.append(translated)
    print(json.dumps({'text':''.join(answer)},ensure_ascii=True),flush=True)
try: run()
except Exception:
    print('{"error":"translation-failed"}',flush=True)
    sys.exit(1)
