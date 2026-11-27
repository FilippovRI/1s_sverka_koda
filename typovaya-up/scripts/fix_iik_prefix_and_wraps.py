# -*- coding: utf-8 -*-
"""Re-prefix ИиК method names (УП_) and restore typovaya #Если/#КонецЕсли module wraps.

Does not rewrite typovaya-baseline calls inside #Удаление / outside #Вставка.
"""
from __future__ import annotations
import argparse, json, re, collections
from pathlib import Path

HEAD_RE = re.compile(r"^(Процедура|Функция)\s+([^\s\(\/]+)\s*\(", re.I)
END_RE = re.compile(r"^(КонецПроцедуры|КонецФункции|Конецфункции)\b", re.I)
IIK_RE = re.compile(r'^&ИзменениеИКонтроль\("([^"]+)"\)\s*$', re.I)
IF_WORD_RE = re.compile(r"^#\s*Если\b", re.I)
ENDIF_WORD_RE = re.compile(r"^#\s*КонецЕсли\b", re.I)

def decode(raw: bytes):
    if raw.startswith(b"\xef\xbb\xbf"):
        enc, body = "utf-8-sig", raw[3:]
    else:
        enc, body = "utf-8", raw
        for e in ("utf-8", "utf-16", "cp1251"):
            try:
                raw.decode(e); enc, body = e, raw; break
            except Exception:
                pass
    if body.count(b"\r\r\n") > 0:
        text = body.replace(b"\r\r\n", b"\n")
        text = text.decode("utf-8" if enc=="utf-8-sig" else enc, errors="replace")
        return text, ("utf-8-sig" if raw.startswith(b"\xef\xbb\xbf") else enc), "\r\n"
    if body.count(b"\r\n"):
        dec = enc if enc != "utf-8-sig" else "utf-8"
        return body.replace(b"\r\n", b"\n").decode(dec, errors="replace"), ("utf-8-sig" if raw.startswith(b"\xef\xbb\xbf") else enc), "\r\n"
    dec = enc if enc != "utf-8-sig" else "utf-8"
    return body.replace(b"\r", b"\n").decode(dec, errors="replace"), ("utf-8-sig" if raw.startswith(b"\xef\xbb\xbf") else enc), "\n"

def encode(text, enc, newline):
    payload = text.replace("\n", newline)
    if enc == "utf-8-sig":
        return b"\xef\xbb\xbf" + payload.encode("utf-8")
    return payload.encode(enc if enc != "utf-8-sig" else "utf-8")

def parse_methods(text: str):
    lines = text.splitlines(); res=[]; i=0; pending=[]; pending_start=0
    while i < len(lines):
        raw=lines[i]; s=raw.strip()
        if not s or s.startswith("//") or s.startswith("#") or s.startswith("&"):
            if not pending: pending_start=i
            pending.append(raw); i+=1; continue
        m=HEAD_RE.match(s)
        if not m:
            pending=[]; i+=1; continue
        name, kind = m.group(2), m.group(1)
        sig=[lines[i]]; depth=lines[i].count("(")-lines[i].count(")"); k=i
        while depth>0 and k+1<len(lines):
            k+=1; sig.append(lines[k]); depth+=lines[k].count("(")-lines[k].count(")")
        body=[]; t=k+1
        while t<len(lines):
            body.append(lines[t])
            if END_RE.match(lines[t].strip()):
                t+=1; break
            t+=1
        dirs=[x for x in pending if x.strip().startswith("&")]
        iik=None
        for d in dirs:
            mm=IIK_RE.match(d.strip())
            if mm: iik=mm.group(1)
        res.append({"name":name,"kind":kind,"dirs":dirs,"iik":iik,
                    "header":"\n".join(sig),"body":"\n".join(body),"pending":pending[:],
                    "start": pending_start if pending else i, "end":t, "proc":i})
        pending=[]; i=t
    return res, lines

def preproc_word(s: str):
    st=s.strip()
    if not st.startswith("#"):
        return ""
    return st[1:].split()[0].lower().replace("ё","е") if st[1:] else ""

def depth_if(text: str) -> int:
    d=0
    for ln in text.splitlines():
        w=preproc_word(ln)
        if w=="если": d+=1
        elif w=="конецесли": d-=1
    return d

def is_blank_or_comment(s: str) -> bool:
    st=s.strip()
    return (not st) or st.startswith("//")

def find_module_wrap(text: str):
    lines=text.splitlines()
    open_i=close_i=-1
    for i,s in enumerate(lines):
        if is_blank_or_comment(s):
            continue
        if preproc_word(s)=="если":
            open_i=i
        break
    for i in range(len(lines)-1, -1, -1):
        if is_blank_or_comment(lines[i]):
            continue
        if preproc_word(lines[i])=="конецесли":
            close_i=i
        break
    if open_i<0 or close_i<0 or close_i<=open_i:
        return None
    d=0
    for i in range(open_i, close_i+1):
        w=preproc_word(lines[i])
        if w=="если": d+=1
        elif w=="конецесли":
            d-=1
            if d==0 and i!=close_i:
                return None
    if d!=0:
        return None
    return lines[open_i], lines[close_i]

def strip_orphan_endif(text: str) -> tuple[str,int]:
    lines=text.splitlines()
    d=0
    for s in lines:
        w=preproc_word(s)
        if w=="если": d+=1
        elif w=="конецесли": d-=1
    if d>=0:
        return text, 0
    need=-d
    removed=0
    i=len(lines)-1
    while i>=0 and removed<need:
        if is_blank_or_comment(lines[i]):
            i-=1
            continue
        if preproc_word(lines[i])=="конецесли":
            del lines[i]
            removed+=1
            i-=1
            continue
        break
    while lines and lines[-1].strip()=="":
        lines.pop()
    return "\n".join(lines), removed

def apply_wrap(text: str, prefix: str, suffix: str) -> str:
    t=text
    while t.startswith("\n"):
        t=t[1:]
    while t.endswith("\n"):
        t=t[:-1]
    return prefix + "\n" + t + "\n" + suffix + "\n"

def unique_prefixed(typ: str, prefix: str, existing: set[str]) -> str:
    name=prefix+typ
    if name.lower() not in existing:
        return name
    name=prefix+"Хук_"+typ
    n=2
    while name.lower() in existing:
        name=prefix+"Хук"+str(n)+"_"+typ
        n+=1
    return name

def rename_header(header: str, old: str, new: str) -> str:
    return re.sub(
        r"(?i)(Процедура|Функция)(\s+)"+re.escape(old)+r"(\s*\()",
        lambda m: m.group(1)+m.group(2)+new+m.group(3),
        header, count=1)

def load_tree(root: Path):
    out={}
    for p in root.rglob("*.bsl"):
        rel=p.relative_to(root).as_posix()
        raw=p.read_bytes()
        text,enc,nl=decode(raw)
        out[rel.lower()]={"path":p,"rel":rel,"text":text,"enc":enc,"nl":nl}
    return out

def process(ext_root: Path, rest_root: Path, report: Path, prefix="УП_"):
    exts=load_tree(ext_root)
    rests=load_tree(rest_root)
    stats=collections.Counter()
    changed_files=0
    details=[]
    for key, ext in exts.items():
        text=ext["text"]
        methods, _=parse_methods(text)
        existing={m["name"].lower() for m in methods}
        new_text=text
        # 1) re-prefix IIK names (declaration only)
        # work from the original text with replacements on method headers only
        # We'll reconstruct via line surgery on a fresh parse after each? Better: collect header replacements then apply from end.
        methods, lines=parse_methods(new_text)
        file_changed=False
        existing={m["name"].lower() for m in methods}
        # reserved after planned renames
        for m in methods:
            if not m["iik"]:
                continue
            typ=m["iik"]
            stats["iik_total"]+=1
            if m["name"]==prefix+typ or m["name"].lower()== (prefix+typ).lower():
                stats["already_prefixed"]+=1
                continue
            if m["name"]!=typ:
                # already a different name (maybe УП_Хук_ or custom) — leave, but count
                stats["other_name"]+=1
                details.append({"rel":ext["rel"],"typ":typ,"hook":m["name"],"action":"other_name"})
                continue
            new_name=unique_prefixed(typ, prefix, existing)
            existing.add(new_name.lower())
            new_header=rename_header(m["header"], m["name"], new_name)
            if new_header==m["header"]:
                stats["rename_failed"]+=1
                details.append({"rel":ext["rel"],"typ":typ,"hook":m["name"],"action":"rename_failed"})
                continue
            # replace only the header lines (proc line start), not pending annotations
            # header is first lines of the method starting at proc
            hdr_lines=new_header.splitlines()
            old_hdr=m["header"].splitlines()
            if len(hdr_lines)!=len(old_hdr):
                # still replace starting at proc
                pass
            lines[m["proc"]:m["proc"]+len(old_hdr)] = hdr_lines
            file_changed=True
            if new_name==prefix+typ:
                stats["reprefixed"]+=1
            else:
                stats["reprefixed_collision"]+=1
                details.append({"rel":ext["rel"],"typ":typ,"hook":new_name,"action":"collision"})
        if file_changed:
            new_text="\n".join(lines)
        # 2) strip orphan endif
        stripped, nrem=strip_orphan_endif(new_text)
        if nrem:
            stats["orphan_endif_removed"]+=nrem
            stats["modules_stripped"]+=1
            new_text=stripped
            file_changed=True
        # 3) apply typovaya wrap if typovaya has a module wrap and extension does not
        rest=rests.get(key)
        wrap=find_module_wrap(rest["text"]) if rest else None
        ext_wrap=find_module_wrap(new_text)
        if wrap and not ext_wrap:
            new_text=apply_wrap(new_text, wrap[0], wrap[1])
            stats["wraps_applied"]+=1
            file_changed=True
        elif wrap and ext_wrap:
            stats["wraps_already"]+=1
        if file_changed:
            # tidy excessive blanks
            while "\n\n\n\n" in new_text:
                new_text=new_text.replace("\n\n\n\n","\n\n\n")
            if not new_text.endswith("\n"):
                new_text+="\n"
            ext["path"].write_bytes(encode(new_text, ext["enc"], ext["nl"]))
            changed_files+=1
    stats["changed_files"]=changed_files
    print("STATS", dict(stats))
    if report:
        report.parent.mkdir(parents=True, exist_ok=True)
        report.write_text(json.dumps({"stats":dict(stats),"details":details}, ensure_ascii=False, indent=2), encoding="utf-8")
        print("report", report)
    return stats

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--ext", required=True)
    ap.add_argument("--restored", required=True)
    ap.add_argument("--report", default="")
    args=ap.parse_args()
    return 0 if process(Path(args.ext), Path(args.restored), Path(args.report) if args.report else None) else 0

if __name__=="__main__":
    raise SystemExit(main())
