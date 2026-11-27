# -*- coding: utf-8 -*-
"""Выровнять хуки &ИзменениеИКонтроль расширения по текстам методов restored.

  python align_iik_hooks_to_restored.py --ext DIR --restored DIR [--up DIR]
"""
from __future__ import annotations
import argparse, re, collections, json, sys
from pathlib import Path

HEAD_RE = re.compile(r"^(Процедура|Функция)\s+([^\s\(\/]+)\s*\(", re.I)
END_RE = re.compile(r"^(КонецПроцедуры|КонецФункции|Конецфункции)\b", re.I)
PREF_CALL = re.compile(r"(?<![\wА-Яа-яЁё])УП_([A-Za-zА-Яа-яЁё_][A-Za-zА-Яа-яЁё0-9_]*)")
IIK_RE = re.compile(r'^&ИзменениеИКонтроль\("([^"]+)"\)\s*$', re.I)
INSTEAD_RE = re.compile(r'^&Вместо\("([^"]+)"\)\s*$', re.I)
COMP_DIR_RE = re.compile(
    r'^&(НаКлиентеНаСервереБезКонтекста|НаСервереБезКонтекста|НаКлиентеНаСервере|НаСервере|НаКлиенте)\b', re.I)

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
        if enc == "utf-8-sig":
            text = text.decode("utf-8", errors="replace")
        else:
            text = text.decode(enc, errors="replace")
        return text, "utf-8-sig" if raw.startswith(b"\xef\xbb\xbf") else enc, "\r\n"
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
        iik=instead=None
        for d in dirs:
            ds=d.strip()
            mm=IIK_RE.match(ds)
            if mm: iik=mm.group(1)
            mm=INSTEAD_RE.match(ds)
            if mm: instead=mm.group(1)
        res.append({"name":name,"kind":kind,"dirs":dirs,"iik":iik,"instead":instead,
                    "header":"\n".join(sig),"body":"\n".join(body),"pending":pending[:],
                    "start": pending_start if pending else i, "end":t})
        pending=[]; i=t
    return res, lines

def restore_base_body(body: str) -> str:
    out=[]; ins=False
    for ln in body.splitlines():
        s=ln.strip()
        if s=="#Вставка":
            ins=True; continue
        if s=="#КонецВставки":
            ins=False; continue
        if s in ("#Удаление","#КонецУдаления"):
            continue
        if ins: continue
        out.append(ln)
    return "\n".join(out)

def unprefix_header(header, hook_name, typ_name):
    if hook_name==typ_name: return header
    return re.sub(r"(?i)(Процедура|Функция)(\s+)"+re.escape(hook_name)+r"(\s*\()",
                  lambda m: m.group(1)+m.group(2)+typ_name+m.group(3), header, count=1)

def unprefix_calls(text, typ_names):
    def repl(m):
        ident=m.group(1)
        return ident if ident.lower() in typ_names else m.group(0)
    return PREF_CALL.sub(repl, text)

def unprefix_calls_baseline(body, typ_names):
    out=[]; ins=False
    for ln in body.splitlines():
        s=ln.strip()
        if s=="#Вставка":
            ins=True; out.append(ln); continue
        if s=="#КонецВставки":
            ins=False; out.append(ln); continue
        out.append(ln if ins else unprefix_calls(ln, typ_names))
    return "\n".join(out)

def compile_dirs(dirs):
    return [d for d in dirs if COMP_DIR_RE.match(d.strip())]

def norm(s):
    return "\n".join(ln.rstrip() for ln in (s or "").splitlines()).strip()

def drop_end(body):
    ls=body.splitlines()
    if ls and END_RE.match(ls[-1].strip()):
        return "\n".join(ls[:-1]), ls[-1]
    return body, ""

def has_preproc(body):
    skip={"#Вставка","#КонецВставки","#Удаление","#КонецУдаления"}
    return any(ln.strip().startswith("#") and ln.strip() not in skip for ln in (body or "").splitlines())

def find_method(methods, name):
    nl=name.lower()
    for m in methods:
        if m["name"].lower()==nl or (m.get("iik") and m["iik"].lower()==nl):
            return m
    return None

def rebuild_iik_full(rm, up_m, typ_name):
    cdl = compile_dirs(rm["dirs"]) or (compile_dirs(up_m["dirs"]) if up_m else [])
    rcore, rend = drop_end(rm["body"])
    ucore, uend = drop_end(up_m["body"]) if up_m else ("", "")
    end = rend or uend or ("КонецФункции" if rm["kind"].lower().startswith("функ") else "КонецПроцедуры")
    chunks=[]
    if cdl: chunks.append("\n".join(cdl))
    chunks.append('&ИзменениеИКонтроль("%s")' % typ_name)
    chunks.append(rm["header"])
    chunks.append("\n".join(["#Удаление", rcore, "#КонецУдаления", "#Вставка", ucore, "#КонецВставки", end]))
    return "\n".join(chunks)

def rebuild_instead(up_m, typ_name):
    cdl=compile_dirs(up_m["dirs"])
    header=unprefix_header(up_m["header"], up_m["name"], typ_name)
    header=re.sub(r"(?i)(Процедура|Функция)(\s+)"+re.escape(typ_name)+r"(\s*\()",
                  lambda m: m.group(1)+m.group(2)+"УП_"+typ_name+m.group(3), header, count=1)
    chunks=[]
    if cdl: chunks.append("\n".join(cdl))
    chunks.append('&Вместо("%s")' % typ_name)
    chunks.append(header)
    chunks.append(up_m["body"])
    return "\n".join(chunks)

def own_from_iik(m, typ_name):
    # drop ИиК annotation; keep compile dirs; keep #Вставка body if present else whole body
    ins=[]; ins_on=False; any_ins=False; end=""
    for ln in m["body"].splitlines():
        s=ln.strip()
        if END_RE.match(s):
            end=ln; continue
        if s=="#Вставка":
            ins_on=True; any_ins=True; continue
        if s=="#КонецВставки":
            ins_on=False; continue
        if s in ("#Удаление","#КонецУдаления"):
            continue
        if any_ins and not ins_on:
            continue
        if not any_ins or ins_on:
            ins.append(ln)
    header=unprefix_header(m["header"], m["name"], typ_name)
    cdl=compile_dirs(m["dirs"])
    chunks=[]
    if cdl: chunks.append("\n".join(cdl))
    chunks.append(header)
    if ins: chunks.append("\n".join(ins))
    if end: chunks.append(end)
    return "\n".join(chunks)

def load_tree(root: Path):
    out={}
    if not root or not root.exists():
        return out
    for p in root.rglob("*.bsl"):
        rel=str(p.relative_to(root)).replace("/","\\")
        text,enc,nl=decode(p.read_bytes())
        methods, lines=parse_methods(text)
        out[rel.lower()]={"path":p,"rel":rel,"text":text,"enc":enc,"nl":nl,"methods":methods,"lines":lines}
    return out

def process_module(ext, rest, up, typ_names_extra, stats, leftovers):
    methods=ext["methods"]
    iiks=[m for m in methods if m["iik"]]
    if not iiks:
        return False
    rmethods=rest["methods"] if rest else []
    umethods=up["methods"] if up else []
    typ_names=set(typ_names_extra)
    for rm in rmethods:
        typ_names.add(rm["name"].lower())
    for m in iiks:
        typ_names.add(m["iik"].lower())
    rmap={rm["name"].lower():rm for rm in rmethods}
    umap={um["name"].lower():um for um in umethods}
    # rebuild file from end to start
    lines=ext["lines"][:]
    changed=False
    actions=[]
    for m in sorted(iiks, key=lambda x: x["start"], reverse=True):
        typ=m["iik"]
        rm=rmap.get(typ.lower())
        um=umap.get(typ.lower())
        header=unprefix_calls(unprefix_header(m["header"], m["name"], typ), typ_names)
        body=unprefix_calls_baseline(m["body"], typ_names)
        base=restore_base_body(body)
        action="keep"
        new_text=None
        if rm and header==rm["header"] and base==rm["body"]:
            # only rewrite if name/calls changed vs original
            if header!=m["header"] or body!=m["body"]:
                action="unprefix"
                cdl=compile_dirs(m["dirs"])
                chunks=[]
                if cdl: chunks.append("\n".join(cdl))
                chunks.append('&ИзменениеИКонтроль("%s")' % typ)
                chunks.append(header)
                chunks.append(body)
                new_text="\n".join(chunks)
            else:
                action="already_ok"
        elif rm and norm(header)==norm(rm["header"]) and norm(base)==norm(rm["body"]):
            # whitespace / line-ending only: rebuild typovaya parts from restored, keep inserts if any
            action="ws_align"
            new_text=rebuild_iik_full(rm, um, typ) if um and norm(base)!=norm(um["body"] if um else "") or True else None
            # Always use exact restored as #Удаление and UP (or current inserts) as #Вставка.
            if um:
                new_text=rebuild_iik_full(rm, um, typ)
                if norm(rm["body"])==norm(um["body"]) and norm(rm["header"])==norm(unprefix_header(um["header"], um["name"], typ)):
                    action="remove_identical"
                    new_text=""
            else:
                new_text=rebuild_iik_full(rm, None, typ)
        elif rm and um and norm(rm["body"])==norm(um["body"]) and norm(rm["header"])==norm(unprefix_header(um["header"], um["name"], typ)):
            action="remove_identical"
            new_text=""
        elif rm and um and has_preproc(rm["body"]) and not has_preproc(um["body"]):
            action="instead"
            new_text=rebuild_instead(um, typ)
        elif rm and um:
            action="rebuild_iik"
            new_text=rebuild_iik_full(rm, um, typ)
        elif rm and not um:
            # no UP method: keep unprefixed if we can force typovaya exact via empty insert? cannot invent UP.
            # If only prefix/ws we already handled; remaining -> keep unprefix only if base close else leftover
            action="leftover_no_up"
            leftovers.append((ext["rel"], typ, "no_up"))
            if header!=m["header"] or body!=m["body"]:
                cdl=compile_dirs(m["dirs"])
                chunks=[]
                if cdl: chunks.append("\n".join(cdl))
                chunks.append('&ИзменениеИКонтроль("%s")' % typ)
                chunks.append(header)
                chunks.append(body)
                new_text="\n".join(chunks)
                action="unprefix_leftover"
        elif not rm and um:
            action="own_up_only"
            new_text=rebuild_instead(um, typ).replace('&Вместо(', '&Вместо(')
            # missing in restored: own method, not ИиК and not &Вместо (nothing to replace)
            new_text=own_from_iik(m, typ)
        elif not rm:
            action="own_missing"
            new_text=own_from_iik(m, typ)
        stats[action]+=1
        if action=="already_ok":
            continue
        if new_text is None:
            leftovers.append((ext["rel"], typ, action))
            continue
        # replace lines[m.start:m.end]
        new_lines=new_text.splitlines() if new_text else []
        # keep a blank line after method if originally present
        lines[m["start"]:m["end"]] = new_lines
        changed=True
        actions.append((typ, action))
    if changed:
        # tidy: avoid triple blank lines
        text="\n".join(lines)
        while "\n\n\n\n" in text:
            text=text.replace("\n\n\n\n","\n\n\n")
        ext["path"].write_bytes(encode(text, ext["enc"], ext["nl"]))
    return changed

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--ext", required=True)
    ap.add_argument("--restored", required=True)
    ap.add_argument("--up", default="")
    ap.add_argument("--report", default="")
    args=ap.parse_args()
    ext_root=Path(args.ext); rest_root=Path(args.restored); up_root=Path(args.up) if args.up else None
    print("loading trees...", flush=True)
    exts=load_tree(ext_root)
    rests=load_tree(rest_root)
    ups=load_tree(up_root) if up_root else {}
    print("ext modules", len(exts), "rest", len(rests), "up", len(ups), flush=True)
    stats=collections.Counter(); leftovers=[]; changed_files=0
    for key, ext in exts.items():
        rest=rests.get(key); up=ups.get(key)
        if process_module(ext, rest, up, set(), stats, leftovers):
            changed_files+=1
    print("changed_files", changed_files)
    print("actions", dict(stats))
    print("leftovers", len(leftovers))
    for item in leftovers[:30]:
        print("  leftover", item)
    if args.report:
        Path(args.report).write_text(json.dumps({"stats":dict(stats),"leftovers":leftovers,"changed_files":changed_files}, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0 if not leftovers else 1

if __name__=="__main__":
    sys.exit(main())