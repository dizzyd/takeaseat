#!/usr/bin/env python3
"""Generates the takeaseat-lounge animation patch from vanilla's sitidle.

    python3 tools/make-lounge.py "$VINTAGE_STORY"

The lounge is sitidle sat back in a chair: hips tipped back RECLINE degrees, the upper
torso straightened out of sitidle's forward hunch, and everything hung off them - head,
thighs, arms - turned back by the same amount so it keeps its place. The feet swing a
little forward.

Keep RECLINE small. Seats are placed so the sitter's backside is against the backrest, and
with a vertical backrest every degree of recline pushes the shoulders further into it:
8 degrees here puts the upper back 0.09 behind the backside, which is about the gap a
chair leaves. SeatLayout's default loungeDepth follows from these numbers.
"""
import copy, json, os, sys

RECLINE = 8          # hips, degrees back
UPPER_TORSO = -16    # relative to sitidle's +12 forward hunch
FEET_FORWARD = -18   # shins, relative to sitidle
HEAD = RECLINE + 16  # undo the recline and the straightening, so the head stays level
ARMS = 15            # arms hang from the torso, so they swing forward with it; pull them back

game = sys.argv[1] if len(sys.argv) > 1 else os.environ["VINTAGE_STORY"]
shape = json.load(open(os.path.join(game, "assets/game/shapes/entity/humanoid/seraph-faceless.json")))
anim = copy.deepcopy(next(a for a in shape["animations"] if a["code"] == "sitidle"))
anim["name"] = "TakeASeatLounge"
anim["code"] = "takeaseat-lounge"

# (element, amount added to rotationZ); the hips' recline is set outright on frame 0.
deltas = {
    "UpperTorso": UPPER_TORSO,
    "Head": HEAD,
    "UpperFootL": RECLINE, "UpperFootR": RECLINE,
    "LowerFootL": FEET_FORWARD, "LowerFootR": FEET_FORWARD,
    "UpperArmL": ARMS, "UpperArmR": ARMS,
}
groups = [("offsetX", "offsetY", "offsetZ"), ("rotationX", "rotationY", "rotationZ"), ("stretchX", "stretchY", "stretchZ")]

for kf in anim["keyframes"]:
    els = kf["elements"]
    if kf["frame"] == 0:
        els.setdefault("LowerTorso", {})["rotationZ"] = -RECLINE
    for name, add in deltas.items():
        if name in els and "rotationZ" in els[name]:
            els[name]["rotationZ"] = round(els[name]["rotationZ"] + add, 2)
    # The game lerps each group of three together; a group given in part crashes the client.
    for e in els.values():
        for g in groups:
            if any(k in e for k in g):
                for k in g:
                    e.setdefault(k, 0.0)

patch = [{"op": "add", "path": "/animations/-", "file": "game:shapes/entity/humanoid/seraph-faceless.json", "value": anim}]
out = os.path.join(os.path.dirname(__file__), "../takeaseat/assets/takeaseat/patches/player-lounge-shape.json")
with open(out, "w") as f:
    json.dump(patch, f, indent=2)
    f.write("\n")
print("wrote", os.path.normpath(out))
