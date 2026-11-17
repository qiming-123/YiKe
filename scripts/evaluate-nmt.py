import json, sys, time, ctypes
from pathlib import Path
import ctranslate2
import sentencepiece as spm
root=Path(sys.argv[1]); before=json.loads(Path(sys.argv[2]).read_text(encoding='utf-8-sig'))
source=spm.SentencePieceProcessor(model_file=str(root/'source.spm'))
target=spm.SentencePieceProcessor(model_file=str(root/'target.spm'))
translator=ctranslate2.Translator(str(root),device='cpu',compute_type='int8',inter_threads=1,intra_threads=2)
results=[]
for item in before['results']:
    start=time.monotonic(); tokens=['>>cmn_Hans<<']+source.encode(item['source'],out_type=str)
    answer=translator.translate_batch([tokens],beam_size=4,max_input_length=0,max_decoding_length=512)[0]
    text=target.decode(answer.hypotheses[0]); results.append(dict(source=item['source'],translation=text,milliseconds=round((time.monotonic()-start)*1000)))
class Counters(ctypes.Structure):
    _fields_=[('cb',ctypes.c_ulong),('PageFaultCount',ctypes.c_ulong),('PeakWorkingSetSize',ctypes.c_size_t),('WorkingSetSize',ctypes.c_size_t),('QuotaPeakPagedPoolUsage',ctypes.c_size_t),('QuotaPagedPoolUsage',ctypes.c_size_t),('QuotaPeakNonPagedPoolUsage',ctypes.c_size_t),('QuotaNonPagedPoolUsage',ctypes.c_size_t),('PagefileUsage',ctypes.c_size_t),('PeakPagefileUsage',ctypes.c_size_t),('PrivateUsage',ctypes.c_size_t)]
state=Counters();state.cb=ctypes.sizeof(state)
ctypes.windll.psapi.GetProcessMemoryInfo(ctypes.c_void_p(-1),ctypes.byref(state),state.cb)
output=dict(peakWorkingBytes=state.PeakWorkingSetSize,privateBytes=state.PrivateUsage,results=results)
Path(sys.argv[3]).write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(output,ensure_ascii=True))
