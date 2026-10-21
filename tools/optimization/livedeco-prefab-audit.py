"""Read Unity v22 serialized prefab/script references; stdlib only, no game calls.

This narrow audit rejects other asset layouts rather than guessing field offsets.
MonoBehaviour/Transform layouts are checked against their object byte lengths.
"""
import argparse
import hashlib
import struct
from pathlib import Path


class Reader:
    def __init__(self, data):
        self.data, self.pos = data, 0

    def read(self, fmt):
        values = struct.unpack_from("<" + fmt, self.data, self.pos)
        self.pos += struct.calcsize("<" + fmt)
        return values[0] if len(values) == 1 else values

    def align(self):
        self.pos = (self.pos + 3) & ~3

    def zstr(self):
        end = self.data.index(0, self.pos)
        text = self.data[self.pos:end].decode("utf-8")
        self.pos = end + 1
        return text

    def string(self):
        length = self.read("i")
        assert 0 <= length <= len(self.data) - self.pos
        text = self.data[self.pos:self.pos + length].decode("utf-8")
        self.pos += length
        self.align()
        return text


class Asset:
    def __init__(self, path):
        self.path = path
        with path.open("rb") as f:
            header = f.read(48)
            assert struct.unpack_from(">I", header, 8)[0] == 22, "requires Unity v22"
            assert header[16] == 0, "requires little-endian metadata"
            size, file_size, self.data_start = struct.unpack_from(">Iqq", header, 20)
            assert file_size == path.stat().st_size
            r = Reader(f.read(size))
        self.version = r.zstr()
        r.read("i")  # target platform
        tree = r.read("?")
        types = []
        for _ in range(r.read("i")):
            cls, _, _ = r.read("i?h")
            if cls == 114:
                r.pos += 16  # script ID
            r.pos += 16  # old type hash
            if tree:
                nodes, strings = r.read("ii")
                r.pos += nodes * 32 + strings
                dependencies = r.read("i")
                r.pos += dependencies * 4
            types.append(cls)
        self.objects = {}
        for _ in range(r.read("i")):
            r.align()
            oid, start, length, tid = r.read("qqIi")
            assert 0 <= tid < len(types)
            assert 0 <= self.data_start + start <= file_size - length
            self.objects[oid] = (start, length, types[tid])
        for _ in range(r.read("i")):
            r.read("i")
            r.align()
            r.read("q")
        self.externals = []
        for _ in range(r.read("i")):
            r.zstr()
            r.pos += 20  # GUID and type
            self.externals.append(r.zstr())

    def object(self, oid, cls):
        start, length, actual = self.objects[oid]
        assert cls == actual, (oid, cls, actual)
        with self.path.open("rb") as f:
            f.seek(self.data_start + start)
            return Reader(f.read(length))


def audit(path):
    assets = {}

    def load(path):
        if path not in assets:
            assets[path] = Asset(path)
        return assets[path]

    a = load(path)

    def script_name(file_id, oid):
        other = a if file_id == 0 else load(path.parent / Path(a.externals[file_id - 1]).name)
        r = other.object(oid, 115)
        r.string()  # name
        r.pos += 20  # execution order and properties hash
        cls, ns, assembly = r.string(), r.string(), r.string()
        return assembly.removesuffix(".dll") + "::" + (ns + "." + cls if ns else cls)

    scripts = {}
    for oid, (_, _, cls) in a.objects.items():
        if cls != 114:
            continue
        r = a.object(oid, 114)
        file_id, go = r.read("iq")
        assert file_id == 0
        enabled = r.read("?")
        r.align()
        name = script_name(*r.read("iq"))
        assert r.string() == "", "requires unnamed prefab components"
        scripts[oid] = (go, name, enabled, r)

    roots = [(oid, v[0]) for oid, v in scripts.items() if v[1] == "Assembly-CSharp::scrVisualDecoration"]
    assert roots, "no visual-decoration prefab found"
    toggles = 0

    def walk(go_id, depth=0):
        nonlocal toggles
        assert depth < 16
        r = a.object(go_id, 1)
        components = [r.read("iq") for _ in range(r.read("i"))]
        r.read("i")  # layer
        name = r.string()
        for file_id, oid in components:
            assert file_id == 0
            if oid in scripts:
                _, cls, enabled, stored = scripts[oid]
                fields = Reader(stored.data)
                fields.pos = stored.pos
                print(f"{' ' * depth}{name}: {cls} id={oid} enabled={enabled}")
                if cls == "Assembly-CSharp::ToggleVisible":
                    target_file, target_id = fields.read("iq")
                    ignore = fields.read("?")
                    fields.align()
                    assert fields.pos == len(fields.data)
                    assert target_file == 0 and target_id in scripts
                    target_go, target_cls, target_enabled, _ = scripts[target_id]
                    print(f"  target={target_cls} id={target_id} same_go={target_go == go_id} "
                          f"enabled={target_enabled} ignoreIfDisabled={ignore}")
                    toggles += 1
        tr = a.object(components[0][1], 4)
        assert tr.read("iq") == (0, go_id)
        tr.pos += 40  # local quaternion, position, scale
        children = [tr.read("iq") for _ in range(tr.read("i"))]
        for file_id, child_id in children:
            assert file_id == 0
            child = a.object(child_id, 4)
            child_file, child_go = child.read("iq")
            assert child_file == 0
            walk(child_go, depth + 1)

    print(f"asset={path.name} version={a.version} sha256={hashlib.sha256(path.read_bytes()).hexdigest()}")
    for oid, go in roots:
        print(f"visual_prefab={oid}")
        walk(go)
    assert toggles > 0, "no ToggleVisible resolved"
    print(f"visual_prefabs={len(roots)} toggles={toggles}; serialized evidence only, no runtime admission proven")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("asset", type=Path)
    audit(parser.parse_args().asset)
