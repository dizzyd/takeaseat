# Take a Seat

Sit properly in chairs, stools, benches and couches: right-click to sit, sneak to stand.
Requires [W4RD0's Furniture](https://mods.vintagestory.at/w4rd0sfurniture) 1.4.0 or newer.

## Features

- Right-click a seat with no sneak held to sit down. Sneak + right-click still does the
  normal thing (e.g. placing a block against the chair).
- Sneak to stand up. You're put back on your feet in front of the seat, or beside it,
  or on top of it if there's no room elsewhere.
- Chairs with a backrest face you away from it, and you lounge back against it. Backless
  stools face whichever way you were looking, snapped to the nearest side, and you sit
  upright. A bench or piano stool faces across itself, on whichever side you were looking.
- Benches and couches seat two.
- **Well rested.** Sit down for 10 minutes (in-game time) and for the next 4 hours you get
  hungry 10% more slowly and heal 10% better. It can be earned once a day (again 20 hours
  after the last time). Stay seated for more than 2 hours and the rested feeling wears off;
  there's no penalty beyond losing it. The numbers are at the top of `WellRested.cs`.
- Breaking or replacing the seat stands you up. Logging out while seated puts you back in
  the seat when you return, unless someone else has taken it.

## How it works

The mod adds a `takeaseat.Seat` block behavior. Everything about a seat is worked out from
the block's collision boxes:

- the box that starts at the floor is the seat, and its top is the sitting height
- a box standing on top of it is the backrest
- a seat about two blocks long gets two places

No block entity is involved, so chairs placed before this mod was installed work too.

## Supporting another furniture mod

Patch the behavior onto its blocks. If a block uses `behaviorsByType`, add it to every entry
there as well, because those replace `behaviors` rather than adding to it:

```json
{ "op": "add", "path": "/behaviors/-", "value": { "name": "takeaseat.Seat" },
  "file": "othermod:blocktypes/chair.json", "dependsOn": [{ "modid": "othermod" }] }
```

Optional properties, for when the shape guesses wrong:

| property | default | meaning |
|---|---|---|
| `seats` | from the seat's length | number of places |
| `seatHeight` | top of the seat box | sitting height, in blocks |
| `legRoom` | `0.13` | how far past the seat's front edge the sitter's position goes, so the lower legs hang in front of the seat |
| `eyeHeight` | `1.0` | first-person eye height above the seat |
| `lounge` | `true` | with a backrest, lean back against it; `false` sits upright |
| `loungeDepth` | `0.64` | how far behind the sitter's position their back reaches when lounging |
| `facing` | none | `"variant"`: with no backrest in the collision boxes, face away from the `side` variant (for chairs whose back is only in the model) |

## The lounge animation

The pose used on seats with a back is generated from vanilla's `sitidle` by
`tools/make-lounge.py`. Edit the numbers there and rerun it rather than editing the patch.

## Building

```bash
export VINTAGE_STORY="$(ls -d ~/.cairn/games/1.22* | sort -V | tail -1)"
./build.sh
```

## Testing

`tests/` is an in-game vstestkit suite. It needs W4RD0's Furniture and the Attribute
Rendering Library in `tests/fixtures/Mods`; `tests/fixtures/fetch.sh` downloads them. Run it on the client tier, and pass
`--mods` as an **absolute** path:

```bash
bash scripts/run.sh mods/takeaseat/tests --mod mods/takeaseat/takeaseat \
    --mods $PWD/mods/takeaseat/tests/fixtures/Mods --client
```

## License

MIT License
