#!/usr/bin/env python3
"""Rank candidate thumbnails by visual similarity to reference images (SigLIP, GPU).
  rank_style.py --refs <dir> --cands <dir> [--top 8] [--out ranked.csv] [--meta cands.jsonl] [--text "wet mossy stone wall"] [--model google/siglip-base-patch16-224]
Score = 0.5*cos(mean ref embedding) + 0.5*max cos over refs. CSV: rank,file,score,mean_cos,max_cos,best_ref,text_cos,title,url.
--meta: fab_search.py JSON lines with thumb_file; titles/urls are joined by file name. --text adds a text-prompt cosine (reported only, not in score).
Run with a python that has torch+transformers, e.g. C:\AI\trellis2\venv\Scripts\python.exe"""
import argparse, csv, glob, json, os, sys
import torch
from PIL import Image
from transformers import AutoImageProcessor, AutoModel, AutoProcessor

EXT = ('.jpg', '.jpeg', '.png', '.webp')
def files(d): return sorted(f for f in glob.glob(os.path.join(d, '*')) if f.lower().endswith(EXT))

def embed_images(model, proc, paths, dev, bs=16):
    out = []
    for i in range(0, len(paths), bs):
        ims = [Image.open(p).convert('RGB') for p in paths[i:i + bs]]
        with torch.no_grad():
            e = model.get_image_features(**proc(images=ims, return_tensors='pt').to(dev))
        if not torch.is_tensor(e): e = e.pooler_output
        out.append(torch.nn.functional.normalize(e.float(), dim=-1).cpu())
    return torch.cat(out)

if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    ap = argparse.ArgumentParser()
    ap.add_argument('--refs', required=True); ap.add_argument('--cands', required=True)
    ap.add_argument('--top', type=int, default=8); ap.add_argument('--out'); ap.add_argument('--meta'); ap.add_argument('--text')
    ap.add_argument('--model', default='google/siglip-base-patch16-224')
    a = ap.parse_args()
    refs, cands = files(a.refs), files(a.cands)
    if not refs or not cands: sys.exit('no images in refs or cands')
    dev = 'cuda' if torch.cuda.is_available() else 'cpu'
    model = AutoModel.from_pretrained(a.model).to(dev).eval(); proc = AutoImageProcessor.from_pretrained(a.model)  # image-only: no sentencepiece needed
    R, C = embed_images(model, proc, refs, dev), embed_images(model, proc, cands, dev)
    meanR = torch.nn.functional.normalize(R.mean(0), dim=-1)
    mean_cos = C @ meanR; sims = C @ R.T; max_cos, best = sims.max(1)
    score = 0.5 * mean_cos + 0.5 * max_cos
    tcos = [None] * len(cands)
    if a.text:
        tproc = AutoProcessor.from_pretrained(a.model)  # needs sentencepiece+protobuf in the venv
        with torch.no_grad():
            t = model.get_text_features(**tproc(text=[a.text], padding='max_length', return_tensors='pt').to(dev))
        if not torch.is_tensor(t): t = t.pooler_output
        tcos = (C @ torch.nn.functional.normalize(t.float(), dim=-1).cpu().T).squeeze(1).tolist()
    meta = {}
    if a.meta:
        for l in open(a.meta, encoding='utf-8'):
            if l.strip():
                m = json.loads(l)
                if m.get('thumb_file'): meta[os.path.basename(m['thumb_file'])] = m
    rows = sorted(range(len(cands)), key=lambda i: -score[i])
    w = csv.writer(open(a.out, 'w', newline='', encoding='utf-8') if a.out else sys.stdout)
    w.writerow(['rank', 'file', 'score', 'mean_cos', 'max_cos', 'best_ref', 'text_cos', 'title', 'url'])
    for r, i in enumerate(rows[:a.top], 1):
        m = meta.get(os.path.basename(cands[i]), {})
        w.writerow([r, cands[i], f'{score[i]:.4f}', f'{mean_cos[i]:.4f}', f'{max_cos[i]:.4f}', os.path.basename(refs[best[i]]),
                    '' if tcos[i] is None else f'{tcos[i]:.4f}', m.get('title', ''), m.get('url', '')])
