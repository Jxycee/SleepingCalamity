# Sleeping Calamity: story bible (game-facing draft)

**Status: DRAFT for the user's sign-off (2026-11-04).** Not canon. `docs/STORY.md` stays the locked source until the user accepts the amendments in section 8. Nothing here edits it.

**What this is.** The short, game-facing summary of the story: tone, the Bargain's truth, the cast, the chapter beats and dive hooks, the ending, what is playable versus lore, and the list of changes it asks of locked text. A designer or writer should be able to work from this page and follow links for depth.

**The layers**
| Layer | File | Role |
|---|---|---|
| Prose | [`docs/story/book/`](../../story/book/README.md), 7 chapters | The story as a novel. Scenes are numbered N.M. Where it disagrees with parts A/B, **the book wins**, and its README canon notes win over everything. |
| Reference lore | [`story-bible-part-a.md`](story-bible-part-a.md), [`story-bible-part-b.md`](story-bible-part-b.md) | Full cast tables, floor-by-floor notes, sample scenes, relationship matrix. Partly superseded by the book (items marked *book* below). |
| Game-facing | this file | Summary plus amendments. |
| Locked | [`docs/STORY.md`](../../STORY.md), [dungeon spec](2026-11-03-hearthdeep-dungeon-design.md) | Settled. |

**Tags.** *settled* = in STORY or the dungeon spec (or the user's 2026-11-03 decisions). *proposal* = new in parts A/B or the book, awaiting sign-off. *book* = the book changed a part A/B detail.

**Numbering.** The book gives Hollowmere two chapters, so **book ch1-2 = bible "ch1"**, book ch3 = Saltwake, ch4 = Cinderholt, ch5 = Thistledown, ch6 = Stillhollow, ch7 = the Hearth and the finale. This file uses **book numbers** throughout. Parts A/B and the dungeon spec use the old numbering (their "ch3" is book ch4).

**How to annotate.** Edit this file directly, or write notes in a copy. Per row use `OK`, `NO`, or `CHANGE: ...`. Cite book scenes as `5.11`. For the amendments in section 8 reply with the numbers you accept, reject or bend. Open questions (section 9) are ranked; answer the top ones first.

---

## 1. Tone amendment and voice rules

### 1.1 What changes from STORY section 1 (*proposal*, amendment 1)
STORY 1: whimsical on the surface, a dark undertow, whimsy as coping. **Keep all of it for the art and the play:** sunlit squares, saturated day colour, storybook shapes, warm music, bread that smells good. **Change one thing in the writing:** the darkness moves from undertow to centre. Grief and guilt are what the story is about, not a shadow under it.

- **Nobody is clean.** Every adult who comes home did something to keep the deal running (watered grain, hid a loft, looked away). The four heroes are not clean either.
- **Consequences stick.** People die and stay dead, a plot stays empty for the whole game, a name is painted off the wall, a hand turns to stone.
- **Violence costs.** Against nightmares it stays crunchy fun. Between people it is rare, short and ugly in its results, never cool.
- **The contrast is the point.** The worst things are said in the nicest places, over warm bread in a sunny square. The art never darkens to match.
- Rating target: mature teen to adult. No sexual content (longing, a kiss, a betrayal at most), no lingering gore.

Why: the user asked for adult-grade, morally grey people with an engaging quest. The settled premise (a town that sold its harvest, then its people) already is that story.

### 1.2 Voice rules (*proposal*)
1. Townsfolk talk plain and short. They lie by leaving things out.
2. Wishes stay plain (settled, STORY 8.3). The story lives in what they say after the wish is met.
3. Humour comes from people under strain. Pip jokes to avoid, Tobin when too tired to argue, Ash dry and usually true, Wren almost never.
4. Never played for laughs: the taken children, Nell, the tally, hunger, a real death, the feast food, Isolde's choice, Vane's sixty years awake.
5. Swearing sparingly; Vale oaths ("rot it", "gods' teeth", "by the bell").
6. **Mawrrow never shouts.** It is a host who has already eaten: warm, hospitable, full of table manners.
7. No villain explains himself fully. Reasons are true and incomplete.
8. No modern slang, no winks at the player. Format and POV rules for the book: [`STYLE.md`](../../story/book/STYLE.md).

---

## 2. The world in one page, and the Bargain's truth

**The Vale** (*settled*): a ring of valleys around an old mountain, six towns on one road, thin soil, a hungry winter every few years. Dates are **pact years (PY)** (*proposal*): the Bargain is PY 0, the Long Night PY 50, the game opens PY 60, the finale is high summer PY 61.

**The Bargain** (*settled* frame). After a famine (PY -1) a prayer at the old harvest fire was answered by **Mawrrow, the Hungering** (*settled* name; STORY's change log keeps it). Rich fields, in return for a yearly tithe carried into the deep. The tithe had to grow every year (a tenth, a quarter, half); the towns cheated and the elders kept quiet. In PY 50 the tithe fell short and Mawrrow took Hollowmere in one night, then the other towns over the following weeks, each to its own debt; then it fell into its glutted sleep.

**The fine print (*proposal*, read in full only at the Orrery dome, 6.13).** The contract is written in Mawrrow's own **script**. Its clauses, as the book gives them:
- The fields are rich, and the tithe grows with every feeding.
- **"Whoever eats at the feast is the table, and is held from that meal until the next."** The tithe is weighed *after* the meal.
- **"If the tithe falls short, the table is forfeit."** The grain was only interest; the people at the table were the collateral. The Tithe Feast was never a thanksgiving: it was each town signing for its own body, once a year, with the first bite.
- "What is owed will be taken, and what is given will be had, and nothing else." (So the demon can take only what is owed or given. This is why the Gleaners can knock but not seize, and why a rescued person is safe from being *taken*, yet is still *owed*.)
- The signer keeps the table; his heart is held against it and given back "when the last table is closed, and he may sleep." (Vane.)
- **The unbound clause:** "Whoever stands at the table unbound, awake before the Host, may speak against this writing, and what is spoken will end it." Vane hid this line for sixty years: the forfeit form came up from below the first autumn, every ledger-keeper copied its last line at the foot of each tally page *unread*, Vane covered it when the Stillhollow clerk copied the contract, and he set the candles so it burned off Isolde's page.

**The binding rule (*proposal*, clarification C1).** A person is bound from the feast they eat at until the next feast. A person who skips one meal is bound to nothing that year, however many feasts they ate before. **Rescued townsfolk are awake but still bound** (they ate in PY 50 and no feast has been held since), so only the four are unbound. *(book; this is new and touches STORY 7: see amendment 4.)*

**Why the four were uncounted: it was not chance (*proposal*, amendment 5).** At the harvest weighing Isolde Hale saw the tithe would fall short and voted anyway to water the grain. That night four adults each got one child away from the table (a mother, a father, a grandmother, a brother): **Isolde** (Wren: the surveyor's staff and the lantern), **Hob Marsh** (Tobin: the axe, and "take your sister"), **Odile Ostrander** (Pip: the dagger and shield, after planting the tunnel story), **Jory Calder** (Ash: the hunting kit). The plan was five children. Tobin, ten and dragging an axe, left five-year-old **Nell** crying by the trestles because she was slow. Nell ate and was taken. The four adults sat down, ate and were taken, because a missing adult was a missing signature. Every other child in Hollowmere ate beside them. The heroes are alive because four adults chose them over everyone else's children. The grey edge: Isolde could read the whole contract, so Wren will never know whether she was saved as a daughter or made as a weapon ("Both answers are true. Pick the one you can live with", 5.13).

**The tally.** Before each feast the ledger-keeper wrote every name in plain ink and the page went down with the cart. At the first bite the demon's ember burned over each eater's name. In the dream (Undercroft floor 8, 2.7) every name is burning and four stand in plain ink (the "blank" names of STORY 8.4), with five small ticks in Isolde's hand beside them, the fifth struck through (Nell).

**What each town did** (*proposal* detail, Part A 2.6 and Part B): Hollowmere watered grain and hid casks; Saltwake kept two ledgers and sold the surplus downriver; Cinderholt "dug toward Mawrrow on purpose to steal back some of its power", and that theft made the last tithe short (*settled*, STORY 6; the book's detail of following warm rock down and putting a pipe in, 4.7, is *proposal*); Thistledown's abbey preached the feast as a sacrament; Stillhollow charted the failing year, told no one and packed mules; the Hearth skimmed a "keepers' share" and swore every elder to the clause. Ordinary people watered their own sacks and did not ask.

**The script and its cost (*proposal*).** The script is the demon's handwriting; a human hand writing it makes small real things (wards, lights, a collar charm, Wren's spells). Every written mark takes warmth from the writing hand; a big one turns it to stone (Isolde's right forefinger, then Wren's). When the Bargain ends, the alphabet ends (amendment 25).

**Mawrrow** (*proposal* detail): can eat only what is owed, so it makes people give; contracts are its teeth. It wants every seat filled and the contract closed, then the lands beyond the mountain. It wants the four because an open debt is a flaw in its law and an unbound person at its table can speak against it.

**The children's table (*proposal*, amendment 14).** Every taken child in the Vale (ninety-three) sleeps at one long table at the bottom of the Hearthdeep, fed sweets in the dream. This is what "sleep sweet" means in the Gleaners' second bargain. No child comes home until the last dive. Nell alone was lifted out, as Vane's first bait for Tobin.

---

## 3. Cast

*settled* = in STORY/spec (names still placeholders unless stated). All other rows are *proposal*. Heirloom owners and hero ages are settled in STORY 3. Fuller tables: [Part A section 3-5](story-bible-part-a.md), [Part B section 1](story-bible-part-b.md).

### 3.1 The four

| Hero | Want / wound / flaw | Arc by chapter (book) | Heirloom (owner, home ch) and final form | End |
|---|---|---|---|---|
| **Wren Hale**, 18 *(settled)* | Want: everyone back exactly as they were *(settled)*. Wound: her mother chose her. Flaw: control; plans others' lives and calls it care. Secret: her magic is the demon's own script. | 1-2 wall, wakes Tuppence, reads the tally. 3 teaches Pip the ledgers. 4 keeps Ash's secret. **5 owner:** Isolde home, the page, hides it. 6 silences Pip with the script (6.7), paints out Stroud (6.14). 7 reads the clause and writes "the table is empty" (7.13). | Staff, Isolde (home ch5). Forms: *Plumb Line*, *The Collar*, *The Vote*; last memory *Hold this for me*; **True North**, burns the yellow of the lantern. | Forefinger stone to the root, no script, no magic, no mother. Keeps house, wall, cat. Surveyor. |
| **Tobin Marsh**, 20 *(settled)* | Want: to stop being the grown-up *(settled)*. Wound: mother Jessamy died PY 47. Flaw: martyrdom with a temper. Secret: he left Nell. | **1-2 owner:** Hob home, Nell home, lies to Marta, Hob and Nell, tells Wren (2.15; she answers "I know", a lie). 3 lies on. 4 breaks Ash's nose (4.14). 5 steadies Wren. 6 lights the lamp and tells all four (6.9). 7 chains Vane, tells Nell the truth, carries Nell and the axe to the gate (7.11), carries Col. | Axe, Hob (home ch2). Forms: *Yard at Dawn*, *The Bell-Frame*, *The Loft*; last memory "Take your sister"; **Marshfell**, splits the table. | Hob takes Nell in; Tobin sleeps till noon once. Carries Nell everywhere. |
| **Pip Ostrander**, 17 *(settled)* | Want: to prove she isn't just the kid *(settled)*. Wound: mother left, Gran raised her; learns the tunnel story was planted. Flaw: says the true thing at the worst moment and enjoys it. | 1-2 jokes through the feast hall. **3 owner:** Odile home, the truth, reads Gideon's confession to the quay (3.15). 4 keeps Ash's secret, gets Dagny's whistle. 5 knuckles rapped. 6 finds the page, gives Wren a night, is silenced (6.6-6.7). 7 whistle stops the burning teams, takes Davy, sits in Odile's chair. | Dagger and shield, Odile (home ch3). Forms: *Gate Watch*, *The Thief*, *The Tale*; last memory *Packing the bag*; **Last Watch**, breaks taking the blow meant for Wren. | Takes the watch. Dun mends the shield, seam showing; she already knew he was her father. |
| **Ash Calder**, 19 *(settled)* | Want: who made the Bargain, and why *(settled)*. Wound: the kit was a goodbye. Flaw: curiosity that treats people as answers. Secret: the warm fissure in the scree where the dreaming voice talks to him (about four years by the finale); his warm hands are the tell. | 1-3 hears the bell in his teeth; learns the voice answers about the table (3). **4 owner:** trades a name for the ward word (4.8), Jory home, the nose; learns Vane is his great-grandfather via "Bettany" (4.13). 5 Vane confirms in the orchard (5.11). 6 faces Vane as kin, four hands push him off his stone (6.13). 7 refuses the voice's offer, returns Vane's heart, buries him. | Bow and sword, Jory (home ch4). Forms: *First Hare*, *The Survey*, *The Fissure*; last memory "Since you're always stealing it"; **Brother's Due**, cuts Vane's heart free. | Voice gone, hands cold; misses it and tells only Wren. Visits the grave yearly. |

Relationships (Part A 3.7) stand: Tobin has loved Wren silently for years; Wren and Ash kissed last winter and never spoke of it. Resolution is open question 18.

**Tuppence** (placeholder, *settled*): Wren's cat, freed from the stone sleep by her first spell; his fur stands up at script. His collar is a collar after the ending (7.13).

### 3.2 Antagonists

| Who | Truth | Status |
|---|---|---|
| **Corwin Vane**, Tithe-Keeper (*settled* name; STORY's change log keeps it) | Signed the Bargain in PY 0 after burying his wife (Ellen) and youngest son; read the forfeit clause and signed anyway. 104 years old in a body that stopped at 44, no heart, no sleep. Stood still under Stillhollow ten years; the bell opened his eyes (2.11, unnamed). Wants rest and his family sleeping sweet. Hand over the unbound clause for sixty years. Reveals himself in Thistledown's orchard (5.11). His command works only on the bound, so it slides off Ash (6.13). Dies at dawn on the scree with his heart back (7.14). | *settled* role; detail *proposal* |
| **Mawrrow** | Voice: a host. Offers each hero a fair price in dreams. Ends as a warm red stone on Hollowmere's cobbles (7.13). | *settled* |
| **The Gleaners** | Taken people offered a second bargain: serve, and your loved ones "sleep sweet" and come home first. They try to *feed* the four, never kill (grab with a struggle-out; a failed struggle is an ordinary revivable down, never binding). Sheaf-masked, demon-made; lieutenants unmask to human faces. They serve plates and do not attack in the Hearthdeep. When the Bargain ends they lie down across the Vale and wake as scorched ordinary people (7.13). | gameplay rule *proposal* (amendments 19, 21) |

### 3.3 Lieutenants

| Name | From | Why they serve | Chapter / outcome |
|---|---|---|---|
| **Abner Stroud**, tithe-reeve; Marta's husband; twin boys Davy and Col | Hollowmere | His boys sleep sweet | Masked whistler from ch2 (floor 9). Unmasks on Saltwake floor 9 ("Don't tell her", 3.10). Betrays Vane at the Orrery and is killed by one spoken word (6.12-6.13). Wren paints him out (6.14). *Proposal.* |
| **Ivo Sallow**, harbour clerk | Saltwake | The dream balances his books | Unmasked floor 9; explains the second bargain and the table; comes home to a town that spits on him; keeps the first honest ledger (3.10, 3.15). *Proposal.* |
| **Dagny Ferrule**, shift-boss | Cinderholt | Fury at a Vale that blamed the miners; wants her father (Ebben) to sleep sweet | Led the dig. Gives Pip the end-of-shift whistle (4.11), comes home, names Osric Varn to the guild and hands herself over (4.15). Drives a cart at the harvest. *Proposal.* |
| **Cantor Lisle**, choirmaster | Thistledown | True believer; served "for everyone at the table", asked nothing for himself | Unmasks, walks down singing (5.9). Serves one course on Hearthdeep floor 6, walks on, is found eaten and at peace inside the maw (7.7, 7.12). *Proposal.* |

### 3.4 Residents by town
Placeholders. "Floor" = where the sleeper is found (set-piece floors hold only their own rescue; others come from generated floors 2, 4, 6, 7, spec 4.4). Full tables: Part A 5.2, Part B section 1.

| Town | Resident (wish, service) | Floor | Status |
|---|---|---|---|
| **Hollowmere** | **Hollis Rye**, baker (oven on the square facing east; bread ladder) | 3 | *settled* role |
| | **Marta Fenn**, innkeeper (inn across from the fountain; cooking). Saw Tobin leave Nell. | 5 | *settled* role |
| | **Dun Harrow**, smith (forge by the stream; weapon services); Pip's father, never claimed her | 4 | proposal; floor in the book (2.1) |
| | **Nell Marsh**, 5 forever (lives at Tobin's, near the cat); in a coal bin | 7 | proposal; home since ch2 (2.14) |
| | **Amos Gill**, sexton and bell-ringer (rings the bell; rings it in the finale) | with the bell, boss | proposal |
| | **Hob Marsh**, timber man (floor 9, rescued after the boss: *settled*, STORY 8.4). That he is **the builder** who restores the heroes' houses and opens decoration is a *proposal* (amendment 11) | 9, after the boss | placement *settled*; builder role proposal |
| | Tansy and Merrin Brook (herbwife, dyer), Bram Tolley (schoolmaster, maps), Aldous Sedge (miller), **Wynn Cobb** (lamp-keeper, collapse seconds) | generated floors 2, 4, 6, 7 | proposal; minor in the book |
| **Saltwake** | **Teague Pellow**, fisherman (opens **fishing**) | 3 | proposal |
| | **Gideon Hask**, reeve and elder (downriver trade); ordered the second ledger; confesses to Pip; driven out lame, plot empty for the whole game | 5 | proposal |
| | Maud Tiller (salter), Kip Rudd (ferry), Dorrit Hask (takes the trade) | 2, 4, 6 | proposal |
| | **Agathe Voss**, lighthouse keeper (beam shows the tide; finale light) | after the boss | proposal |
| | **Odile Ostrander**, watch sergeant, Pip's grandmother (the watch service: reports the starting stir) | after the boss (bait in the Lamp-Lure) | proposal |
| **Cinderholt** | **Hilde Saxby**, foundress (best forge upgrades; bore-cannons) | 3 | proposal |
| | **Osric Varn**, guild master and elder (contracts) | 5 | proposal |
| | Ludo Fitch (powder-man), Bette Rook (bathhouse, healing), Corra Varn (assay) | 2, 4, 6 | proposal |
| | **Jory Calder**, Ash's brother (surveying); sealed in the Old Seam | 8 (story-gated) | proposal |
| | **Ebben Ferrule**, the Foreman (ore line) | after the boss | proposal |
| **Thistledown** | **Sister Clemency** (honey, wax, the swarm) | 3 | proposal |
| | **Abbess Morwenna Pyle**, elder (blessings); never confesses, explains | 5 | proposal |
| | Silas Crewe (grafting), Hep Lowe (cider; grandson Dickon is at the table), Brother Fennick (alms) | 2, 4, 6 | proposal |
| | **Isolde Hale**, Wren's mother (script lore); at the wax lectern writing the tally | 8 | proposal |
| **Stillhollow** | **Edda Quill**, lens-wright (the lens; first piece out of the Orrery) | 3 | proposal |
| | **Provost Ambrose Wyck**, elder; flashes the signal; later blind from holding the lens | 5 | proposal |
| | Jasper Tock (clockwork), Prudence Ames (archive, lore pages), Sorrel Kett (mule train) | 2, 4, 6 | proposal |
| | **Hester Calder**, Ash and Jory's mother, Vane's granddaughter (mending); found by two worn hollows where Vane stood | 8 | proposal |
| **The Hearth** | **Gerda Brand**, keeper and elder (the hearth; the last tithe) | 5 | proposal |
| | Ned Tallow (carts), Wynn Cobb (second visit; the lamp lane relights), Wat Hesketh (draught teams), Old Bryn (charcoal), Linnet Brand (feast cook) | 1-4, 6 | proposal |

Isolde (ch5) and the other three conspirators also appear in section 4.

---

## 4. Chapter by chapter

Book chapters link to [`docs/story/book/`](../../story/book/README.md). "Key scenes" are the beats a designer will want; full lists are in each chapter. The dungeon spec's grammar stands (settled): floors 2, 4, 6, 7 generated, authored 1, 3, 5 (guardian, rescue, bank), 8, 9, boss on 10; safe on pickup; the bank after floor 5 is the only exit; per-player loot, shared keys, pieces and people; stir meter.

### 4.1 Beats

| Ch | Town, dungeon, boss | Hero arc | Truth revealed | Twist | Dies / stays gone | Key scenes |
|---|---|---|---|---|---|---|
| **1** [Before the Meal](../../story/book/01-before-the-meal.md) | Hollowmere; Undercroft floors 1-3 | Prologue: Tobin leaves Nell; ten years; Wren wakes Tuppence | The feast was a feast; four adults each hand over a weapon, no reason given | The storm and the open trapdoor (warded in the script) | Nobody yet. Hob, Odile, Jory, Isolde, Nell and every child are taken | 1.1 feast; 1.3 lanterns out; 1.7 Tobin's "Da had her" lie; 1.9-1.11 spell, storm, ward; 1.14-1.15 Hollis |
| **2** [The Tally](../../story/book/02-the-tally.md) | Hollowmere; floors 4-10; **Bell-Warden** (guardian on 5: **Brazen Ox**) | Tobin: Hob home; Nell found; confession to Wren | The feast was a contract; the four are uncounted *(settled)*; five ticks seeded | The man who should be dead opens his eyes (2.11, unnamed, "Four places.") | Nell is left on the first dive (2.10 "She's still down there"), found on a later one (2.14). Hob home | 2.3 Marta; 2.4 bank; 2.7 tally; 2.9 bell; 2.10 Hob; 2.11 vignette; 2.13 "Not mine to tell"; 2.14 Nell; 2.15 confession |
| **3** [Short Weight](../../story/book/03-short-weight.md) | Saltwake; Drowned Arcade; **Lamp-Lure** (guardian **Weigh-Crab**) | Pip: Odile home; reads Gideon's confession | Two ledgers, the tithe growing; who the Gleaners are; the second bargain; **the children's table** | Stroud unmasks: the whistler is Marta's husband (3.10) | Gideon beaten lame and driven out; his plot stays bare all game. Odile stays home | 3.4 low-tide rescue; 3.6 Weigh-Crab; 3.9 counting-house; 3.10 Ivo and Stroud; 3.11 the Lure; 3.12 Odile's truth; 3.14-3.15 confession, reading |
| **4** [The Old Seam](../../story/book/04-the-old-seam.md) | Cinderholt; Glow Seams; **Foreman's Engine** (guardian **the Bore**) | Ash: trades for the ward word; Jory home; Tobin breaks his nose | The dig made the last tithe short (4.7); Ash is Vane's blood (4.13) | The steward speaks from the dark, names Hester (4.12) | **Wynn is taken back**; the lamp lane stays dark until ch7 (4.8-4.9). Dagny sentenced | 4.3 pouring floor; 4.7 breach; 4.8 the trade; 4.10 the Old Seam; 4.12 the Engine; 4.13 Jory; 4.14 the nose; 4.15 Dagny |
| **5** [Held in the Hand](../../story/book/05-held-in-the-hand.md) | Thistledown; Hive Choir; **Queen (Mother Bounty) and Choir** (guardian **the Censer**) | Wren: Isolde home; the page; the hidden truth | Vane is alive, the signer, the steward; the abbey's sacrament; "exactly as they were" is impossible | Vane walks up the orchard in daylight and takes an axe to the chest (5.11) | Lisle walks down. Wren's forefinger goes grey to the first knuckle (5.10) | 5.4-5.5 Censer; 5.8 scriptorium; 5.9 Lisle; 5.10 queen; 5.11 Vane; 5.12 Isolde; 5.13 "Both answers are true" |
| **6** [Whoever Stands](../../story/book/06-whoever-stands.md) | Stillhollow; Orrery; **Vane and the Gleaners** (guardian **the Curve**) | All four. Pip finds the page; Wren silences her; the empty meeting; Tobin tells Nell's story; Ash faces Vane | The terms in full; the unbound clause; the forfeit sums | Four hands push Vane off his stone; the stars become the contract (6.13) | **Stroud dies** (6.12-6.13). Wren's finger grey to the second knuckle (6.7-6.8). Vane captive | 6.4 signal; 6.5 night attack; 6.7 silencing; 6.9 Tobin lights the lamp; 6.10 Hester; 6.11 mirror floor; 6.12-6.13 boss; 6.14 painted out |
| **7** [Clearing the Table](../../story/book/07-clearing-the-table.md) | The Hearth; Hearthdeep, seven floors, no boss (guardian **the Keepers' Wagon**); the finale: **Mawrrow**, three stages | All four. Wren speaks as the unbound; heirlooms strike once each | The children were the best course; carrying them out is what wakes it | Only Isolde's church ward is a roof the waking demon cannot reach, so the rescued are run down to Hollowmere | **Odile dies** holding the feast gate (7.11). **Vane**, **Lisle**, **Stroud** stay dead. Isolde leaves for good (7.16). Wren's magic ends | 7.5 Nell's "I know"; 7.7 pantry of names; 7.8-7.9 the table; 7.10 fight in the open; 7.11 gate; 7.12-7.13 maw; 7.14 scree; 7.17 harvest |

(Ch1 and ch2 together are the Undercroft: ch1 floors 1-3 and the descent, ch2 floors 4-10 and the aftermath.)

### 4.2 Dive hooks by chapter

| Ch | Stir | Pieces and people | Bank (floor 5) | Boss / guardian | Per-player loot |
|---|---|---|---|---|---|
| 1-2 | Baseline: floors close in as the collapse timer (floor 7) *(spec-proposal)*; Tobin's second swing at the root wall jumps it (2.8) *(bible-proposal, scripted)* | Hollis f3 (the first person home); Dun f4; Marta f5; Nell f7; Hob and Amos after the boss *(spec-proposal; floors as STORY 8.4)* | Tithe-reeve's counting room: yellow fire, cold basin (full heal), strongboxes as slots, hoist (home) *(spec-proposal)*; the iron door that locks behind a pushing party *(bible-proposal)* | Brazen Ox (sluice quenches; one hero as lure); Bell-Warden (tolls slow time, 120 name-tolls, 4 silent swings) *(bible-proposal, boss mechanics)* | Per-player loot as spec (decision 39); sluice key and bellows; the tithe gate becomes the shortcut to the bank (*settled*, decision 13) |
| 3 | Tide is a second clock (bank on a falling tide) *(bible-proposal)* | Teague f3 (one low tide); Gideon in the Weigh-Crab; Ivo f9; Odile after | Counting-room variant *(spec-proposal)* | Weigh-Crab (ring the tide up the grates to flip it); Lamp-Lure (fight by the shadows) *(bible-proposal)* | Tide bell; fishing opens; ferry; Odile's watch shows the stir before a dive *(bible-proposal)* |
| 4 | Heat raises stir faster *(spec-proposal)*; Jory's tunnel is scripted *(bible-proposal)* | Hilde f3; Osric f5; Jory f8 (story-gated, amendment 18); Dagny f9 | Same | The Bore (park an ore cart in front of its hole); Engine (switch the points, cut the seat weld) *(bible-proposal)* | Blasting charge; best forge upgrades; Wynn's seconds gone |
| 5 | Sweetness draws enemies to the haul-carrier *(spec-proposal)* | Clemency f3; abbess f5; Isolde f8 | Same | Censer (throw a haul under its swing, half lost); Queen (the haul-carrier is the lure) *(bible-proposal)* | Each player keeps their own haul (decision 39); only the carrier's haul is exposed to the Censer and the Queen; script lore opens sealed alcoves |
| 6 | Floor turns between dives; last route gone *(bible-proposal)* | Edda f3; Provost f5; Hester f8 | Same | Curve (grows a scale per turn); Vane's laid place and ring-run *(bible-proposal)* | Star key; mule train shares storage; pass charts open the road |
| 7 | No tool, all gimmicks; stir rises with every child carried and the hall grows colder *(spec-proposal; amendment 28)* | Ned f1, Wynn f2, Wat f3, Bryn f4, Gerda f5, Linnet f6, **93 children by hand** f7 (Col last) *(spec-proposal)* | Floor 5 (after the Wagon) | Keepers' Wagon (hold four stopped rings), a guardian, not a boss: see below; finale stages | Per-player loot as spec (decision 39) |

**Legend.** *settled* = in STORY or the spec. *(spec-proposal)* = the bible asks the dungeon spec to add or change a system (stir, bank, counter, people). *(bible-proposal)* = new in the bible and book only (set pieces and scripted beats); the spec needs nothing for it. Cell tags are a first sorting of Part A/B and the book, not new design; the reviewer may re-tag.

**Hearthdeep has no boss in the spec sense** *(proposal, amendment 28)*. The post-boss shortcut (decision 13) is replaced by opening the Hearth's shortcut when the Keepers' Wagon is stopped on floor 5 (the guardian stands in for the boss; the bank follows it). The paid boss retry (decision 25) is N/A: the Wagon is a guardian, and the finale is three stages with no boss door; what a wipe costs there is open.

---

## 5. The ending

**The waking (7.9).** Tobin lays Col Stroud on the last tithe cart at the top of the hall; the hall goes dark; Mawrrow wakes with nothing in it but sixty years of hunger ("Oh. You've cleared the table."). The collapse runs the tithe road backward and the carts ride it out. **The fight in the open (7.10-7.11).** Mawrrow rises into a clear sky above the rebuilt towns; each town contributes: the Hollowmere bell slows it, Saltwake's beam holds its eyes, Cinderholt's two bore-cannons fire, Thistledown's swarm enters its mouths, Stillhollow's lens is held by the Provost until it blinds him, the Hearth rolls the last tithe carts, loaded with fire, into its mouths. A fuller Vale helps; the outcome never changes. The hand closes on the feast gate and on **Odile**, who held it for the last cart and for Tobin carrying Nell and the axe; Mawrrow says "Thank you" to the gate. Pip sees it from the bell tower. **Inside the maw (7.12-7.13).** A first feast hall; Lisle seated, at peace; the ember heart with Vane's heart sealed in it. **This stage is a playable fight with the heart** (STORY 7 stage 3; book 7.12): it sends courses down the tables, every beaten nightmare returning on a plate, and the heirloom strikes and Wren's speech are phases of that fight, not a cutscene. Each hero is offered the one thing they want; all refuse, Ash last. Each heirloom strikes once: Marshfell splits the table, Last Watch takes the blow meant for Wren and breaks, Brother's Due cuts Vane's heart free, True North burns lantern-yellow. Wren reads the forfeit clause and the unbound clause aloud, then writes and says **"the table is empty."** The light goes out of the letters halfway through the last stroke; the spoken word ends the Bargain. Her forefinger turns to stone to the root; she can no longer read the script. The church lanterns, the ward and the collar charm fail. Mawrrow shrinks to a warm red stone on the cobbles where the long tables stood: "Thank you. It was a lovely meal." The Gleaners lie down and wake as ordinary scorched people. The deal-grown barley settles thin that evening.

**Epilogue facts (*proposal*).**
- **Dawn on the scree (7.14).** Ash gives Vane his heart; Vane watches the sun, asks Ash to "tell Hester nothing kind about me", and dies. Ash buries him beside the cold fissure under no stone; Ash throws the red stone in the lake. Bram Tolley breaks Vane's memorial in the churchyard with a hammer.
- **Weeks after (7.15).** Odile buried on the third day. Dun mends Last Watch at his anvil with Hilde's alloy and tells Pip he is her father; she says she has known since the cistern. Marta buries an empty coffin for Stroud ("He chose"). Wren's wall: every name ticked, ninety-three in the "at the table" column, Stroud painted out, Odile's tick ringed in black.
- **End of summer (7.16).** Ash tells only Wren he misses the voice. Isolde leaves over the pass with a satchel and a stick; Hob watches from the yard; Wren works out Hob and Isolde and tells nobody. She never returns.
- **Harvest, autumn PY 61 (7.17).** Thin, on time. Every town brings what it has to Hollowmere's square; the Provost sits blind on a mule and Stillhollow talks to him again; Amos rings the bell; the children are the loudest at the table; Hollis confesses the chalked loaves and nobody cares; Marta feeds Tobin first; Nell on his knee; Wynn lights the lamps by hand an hour early; the church lanterns are dark. The last scene: the nightly meeting, the cat flat-furred, "Eat. Then plant." "It'll be a hard winter." "We've had worse." Nobody makes a joke.

---

## 6. Game versus lore

"Cutscene" = scripted scene; "Floor" = told on a dungeon floor through props or a short line; "Lore" = journal or archive page (Prudence's archive); "Book only" = reference.

| Beat | Where |
|---|---|
| Famine, Vane's wife and son; the signing at the Hearth | Lore (Vane's letters, Wyck's copy); one shot in the Orrery dome |
| The keepers' share; the elder's oath by word of mouth | Lore, Gerda's floor-5 dialogue |
| The feast prologue, the lanterns, the heirloom handings | Playable prologue + montage |
| Ten years, the spell, the storm, the trapdoor | Cutscene (STORY 8.5, settled) |
| The tally, the five ticks, the blank names | Floor 8 set piece (2.7) |
| Tobin leaves Nell (1.1); his lies | Cutscene / flashback; whether the player sees it at the start is open question 19 |
| Bell-Warden counting names; four silent swings | Playable boss |
| The man who should be dead | Cutscene, no name (2.11) |
| Ivo, the second bargain, the table; Stroud unmasks | Cutscene, Arcade floor 9 |
| Gideon's confession; Pip reads it; the beating | Cutscene; the beating off-screen (shouting, men pulled off, the empty plot) |
| The breach and heat curve | Floor 8 + Corra's lore page |
| Ash's trade; the lamps going out | Cutscene (Ash on the scree) |
| Tobin breaks Ash's nose | Cutscene, one punch, no gameplay |
| The steward's voice after the Engine | Cutscene, voice only |
| Isolde's vote, the five ticks, the oil | Cutscene (5.12-5.13) and the page as a lore item |
| Vane in the orchard; the axe; "great-grandfather" | Cutscene |
| The abbey's sermon, the pageant | Lore |
| The Provost's signal; the night attack | Cutscene, then **playable defence** in town |
| The page, the silencing, the empty meeting, Tobin's confession; Stroud's death; the terms in the dome; the painted-out name | Cutscenes, all four |
| The mirror floor | Playable, demon-made copies (never the real four) |
| Carrying the children (Hearthdeep floor 7) | Playable; the last child a cutscene |
| The fight in the open (stage 2); the fight with the heart inside the maw (stage 3, 7.12); the offers and Wren's speech | Playable + cutscene (STORY 7): the heart fight is playable, with the strikes and the speech as phases; the offers are cutscene beats |
| Odile's death | Cutscene, no body shown (Pip in the tower, then her chair) |
| Vane's end; the stone; Bram's hammer | Cutscene |
| Resident endings | Epilogue dialogue and post-game town lines |
| Hester, Bettany, Wyck as the PY 0 witness | Lore + one scene (4.13) |
| Who exactly sleeps at the table, town by town; the Peelman; Sedge boy's pageant beard | Book only plus Wren's wall counts |
| The 2.11 vignette (non-hero POV) | Book only; the game uses a cutscene or Ash at the fissure |

---

## 7. Placeholder note
Names are placeholders. A full table is in section 10.

---

## 8. Proposed STORY.md amendments

Consolidated from parts A and B (original IDs in brackets) and the book canon notes. **Flag** = TS touches settled STORY text; SPEC touches the dungeon spec; DOC doc-only. Nothing is applied.

| # | Amends | Change | Reason | Flag |
|---|---|---|---|---|
| 1 | STORY 1 [A1] | Darkness moves from undertow to centre in the writing; art and play unchanged (section 1). | The user asked for adult-grade people. | TS |
| 2 | New | Calendar in pact years; Long Night PY 50, game PY 60, finale PY 61. | Keeps the dates straight. | DOC |
| 3 | STORY 2 [C1] | Binding rule: bound from the meal you eat until the next; the tithe is weighed after the meal. | Explains how four who ate at earlier feasts were "uncounted". Changes the stated reason in STORY 2 ("they never ate"). | TS |
| 4 | STORY 6 ch5, 7.3, 7.4 *[book]* | The forfeit clause and the unbound clause are written out (section 2). **Rescued townsfolk are awake but still bound** (they ate in PY 50); only the four are unbound. Consequence: the church ward is the only roof the waking demon can't reach; the Gleaners can knock but not seize. | Gives "whoever stands before Mawrrow awake, unbound" a mechanism. | TS |
| 5 | STORY 2 "slipped away" [A7] | The four were saved on purpose by Isolde, Hob, Odile and Jory (a mother, a father, a grandmother, a brother); the plan was five children; Nell ate. Also covers: **Odile planted the tunnel story** with Pip, and **the heirlooms were handed over at the feast**. That touches STORY 2 ("Pip had heard spooky tales ... and talked the others into going") and the playable prologue (STORY 8.5 step 1, "slip out of the Tithe Feast"). The prologue can still play as written; what the player learns later is that it was set up. | Gives the heirlooms their meaning and Tobin his secret. | TS |
| 6 | STORY 2, 8.5 "nothing ever explains it" [A2] | One ambiguous line from Isolde ("I filled the oil. That's all I did."); the finale staff burns the same yellow. It fails with the magic. | Keeps it a lighter mystery. | TS |
| 7 | STORY 8.2 [A3] | The church and tower stand because Isolde warded the cellar; the storm broke the wards; the deep lit the lanterns. The bell, above the wards, was taken. | Reconciles "every building wiped". | TS |
| 8 | STORY 3, spec 4.4 [A4, B4] | Heirlooms survived because the conspirators gave them to the children; Odile, Jory and Isolde were laid in later regions as bait (Nell for Tobin); owners come home in ch3, 4, 5 and their legendary forms are earned then. | "Form earned in the owner's homecoming chapter" (settled) needs somewhere to happen. | TS |
| 9 | STORY 6 ch4, 8.4 [A8, B10] | The man who should be dead is Vane, alive and the steward; he reveals himself in Thistledown's orchard after the boss (5.11), not in a document. | A person, not a page. | TS |
| 10 | New [A10] | Ash is Vane's great-grandson; he learns the kinship at ch4 (the ward word "Bettany") and the rest at ch5. | The hunter and the hunted are family. | TS |
| 11 | STORY 8.2 [A9] | STORY 8.2 says houses stay shabby "until a builder is woken". Proposal: that builder is Hob Marsh (his floor-9 placement is already *settled*, STORY 8.4); his waking restores the heroes' houses and opens decoration. | STORY leaves the builder unnamed. | TS |
| 12 | STORY 2, 6, 7.2 [B1] | **The Hearth is the sixth town:** the village where the Bargain was made and the oath sworn, swallowed last and rebuilt into the lair's mouth; ch7 rebuilds it. Each town still carts its tithe down its own mine-mouth; every *oath* ended at the Hearth. | Makes "six towns" literally true. | TS |
| 13 | STORY 6, 8.4; spec 8, decision 29 [B2] | Saltwake, Cinderholt, Thistledown and Stillhollow keep the ten-floor grammar and run longer by richness; the Hearthdeep has seven authored floors. "Shorter" means play time. **Undercroft shorter than later dungeons** is then a time claim only. | The three texts disagree as written. | TS, SPEC |
| 14 | STORY 5, 8.4; spec 4.4 [B3] | Every taken child sleeps at one table in the Hearthdeep and comes home only in ch7. Wren's wall gains an "at the table" column. When the Undercroft counter reaches zero, **28** Hollowmere names stay unticked (23 children, the three heirloom owners, Hester, Stroud), not three. | A quest line felt in every town; fixes the counter arithmetic. | SPEC |
| 15 | Spec decision 10 and 26, 4.4 [A11] | **Wynn Cobb is re-taken** in ch4 by Ash's trade ("Not taken. Given."), a one-time scripted story event between dives, never caused by a dive outcome; he returns on Hearthdeep floor 2. **Safe on pickup still holds for every dive.** If the user won't bend 4.4 even for story, move the cost to a building or service instead of a person. | The one consequence that sticks to a person. | SPEC |
| 16 | Spec 4.4 *[book canon note 1]* | Freed sleepers are **breathed up a shaft to their own plot**; the shaft closes behind them (so Hollis sees the square, and the party can't use it). One rule must also cover generated-floor pickups (Nell in a coal bin) and the Hearthdeep children, who are carried by hand because the warm won't breathe them up. | Needed: the dive has no exit before the bank. | SPEC |
| 17 | Spec 4.4 people-left counter *[book]* | The counter gets an in-world face per dungeon: **feast lanterns** (Undercroft), **glass net-floats** (Saltwake), **pit checks** (Cinderholt), **a capped comb frame** (Thistledown), **a celestial globe** (Stillhollow), **a dresser of plates** (Hearth). | Each dungeon speaks in its own town's light; UI still reads the same number. | SPEC |
| 18 | Spec 4.4 "nothing missable" *[book 4]* | Cinderholt's Old Seam is a scripted gate: the counter shows Jory from floor 8, nothing opens it, the first floor-8 visit ends in a failed dive on floor 9 (lost satchel), the trade happens between dives, and the road to Thistledown stays shut until Jory is home. | Part B's story gate, made specific. | SPEC |
| 19 | STORY 4 "fully demonic" [A6] | Gleaners: demonic bodies with human faces under sheaf masks (lieutenants unmask); they feed, never kill; a failed struggle is an ordinary down. Gameplay rule (grab with struggle-out, no kill) touches the spec's enemy and revive rules. **Hearthdeep floors:** per the book, Gleaners stand along the walls with plates and do not attack (7.2: "didn't come"); the floors still hold demon-made kitchen nightmares (ladles on legs, a cauldron of teeth, iron fire-dogs), and Gleaners fight only on the Keepers' Wagon. | A face for each chapter. | TS, SPEC |
| 20 | New [B5] | Lieutenants: Ivo and Dagny come home; Lisle refuses and reappears; Stroud dies. Ivo and Dagny become residents with services, which adds names to the spec's resident lists. | Four different outcomes. | TS, SPEC |
| 21 | STORY 8.4, spec 4.4 [B6] | The Gleaners reach a rebuilt town **once** (Stillhollow, 6.5) and take no one, because the deep can take only what is owed or given. | A surface set piece without breaking "safe on pickup". | SPEC |
| 22 | STORY 7.4 "every resident home" [A5, B7] | Becomes "everyone who could come home". Dead and gone: Stroud, Odile, Vane, Lisle, Gideon (driven out), Isolde (leaves). | Consequences that stick. | TS |
| 23 | STORY 7 stage 2 [B7] | Odile dies holding the feast gate, unconditional; Pip takes the watch. Mawrrow thanks the gate (7.11). | The finale costs a person the player rescued. | TS |
| 24 | STORY 4 [B8] | Vane gets his heart back after the contract ends and dies at dawn on the scree. | Rest was his motive. | TS |
| 25 | STORY 7.3-7.4, 8.2, 8.5 [B9, *book*] | Ending the Bargain ends the script: Wren's magic, every ward, the collar, the church lanterns. Her forefinger is stone to the root and she can't read the script. **Gameplay:** post-game does the staff keep its combat spells as "the staff's memory" (open question 5)? | A price for the hero who used the demon's alphabet. | TS |
| 26 | STORY 7.4 *[book]* | Mawrrow ends as a warm red stone on **Hollowmere's cobbles** where the long tables stood (Part B said a cave floor); Ash throws it in the lake; Gleaners wake as ordinary people; the barley thins. | Closes the table where it was laid. | TS |
| 27 | Spec 3.2, Orrery floor 9 | Mirror floor: playable fights against demon-made copies of the other heroes. The silencing (6.7) and the nose (4.14) are cutscenes. The four never fight each other as a duel. Adds a playable floor type to the spec's Orrery floor list and a set piece to STORY 4. | Violence between people is never cool. | TS, SPEC |
| 28 | Spec 3.2 / 9, Hearthdeep *[book]* | Seven authored floors, no tool, all gimmicks; floor 5 bank; floor 7 is "carry ninety-three children by hand" (a carry mechanic not in the spec: spec 4.4 and 4.5 govern pickups and the stir). *Proposals:* a child is **safe once lifted** (it keeps the spec's safe-on-pickup, 4.4) and **carrying is a stir input** (each child carried raises the stir, 4.5). No boss in the spec sense: the Keepers' Wagon is a guardian on floor 5; the shortcut (decision 13) opens when it falls, the paid boss retry (decision 25) is N/A, and the finale's wipe rule is open (section 4.2). | Gives the finale a playable cost. | SPEC |
| 29 | New *[book]* | Elders learned the forfeit clause by word of mouth at the Hearth fire; keepers' share; Mother Bounty (the abbey's gilt procession figure) as the hive queen. | Lore consistency. | DOC |
| 30 | STORY 3 solo [Q1] | Every story scene shows all four heroes in solo and co-op; solo's town jobs stay a gameplay frame. | Keeps scenes writable. | TS |
| 31 | Spec 4.4 "safe on pickup", decision 10 *[book 2.10]* | **Nell is left behind on the first dive** (the floor closes before the party finds her; she is later found in a floor-7 coal bin), comes home on a later dive (2.14). A scripted exception to "nothing missable" and safe-on-pickup, alongside Wynn (15) and Cinderholt's gate (18). Scripted, once, never caused by a dive outcome. | Hob comes home to "She's still down there"; Tobin's guilt needs it. | SPEC |
| 32 | STORY 2 road survivors (hermit, trader, shepherd) | **Drop them** (recommended), or place each as someone who ate at a PY 50 feast and fled later (still bound, harmless) or was outside the Vale. The book has none. Under the binding rule (amendment 3), anyone who skipped the PY 50 meal is unbound and could speak against the contract, which would break "only the four". | Keeps the unbound clause for four people. | TS |

---

## 9. Continuity slips to fix, and open questions

### 9.1 Slips (for the annotation pass)
1. **Pip's boots.** She banks her worn boots (2.4) but wears "her new boots" on floor 7 (2.6).
2. **Wall counts.** 3.16 moves 23 names to "At the table"; 7.15 ticks *ninety-three* in that column. The wall is Hollowmere's, but ninety-three is the Vale's child count. Either the column grows as other towns' children are named, or 7.15 is a typo. Also 28 names are left at the Undercroft's end (23 + 3 + Hester + Stroud).
3. **Marshfell.** Tobin's legendary form is named only in 7.12; Part A has it earned at Hob's homecoming (ch2). Name it at ch2 or note that it unlocks later.
4. **Part A/B text the book superseded:**
   - Saltwake's confession comes *after* the boss (3.14-3.15), not before.
   - Isolde's "the letters end when the demon ends" is ch6 (6.8), not ch4.
   - Ash learns the Vane kinship at ch4 (4.13); Vane stays alive-and-steward until 5.11.
   - Ash's voice: "about four years" by the finale (Part B 3.3 says three); Pip has kept it two years (Part A says three).
   - Mawrrow ends on the cobbles, not a cave floor.
   - Vane is not led in as a free guide: Tobin keeps him chained at the cave mouth (7.1).
   - The clause hid in plain sight at the foot of every tally page (6.4-6.13), not only on the burned-off page.
   - Nell was already home since 2.14, so there is no "Nell last".
5. **Pip's skinned knuckles** (ch3 open item 2) and **Wren's first two staff memories** off-page in ch3-4 (ch5 open item 2): decide.
6. **Tobin's 2.14 claim** ("I came back for you") and Pip's knowledge that he never did is intended dramatic irony, not a slip; keep.
7. **The 2.11 vignette** is the book's only non-hero POV (STYLE says one hero POV per scene).

### 9.2 Open questions for the user (ranked)
1. **Tone amendment** (amendment 1): darkness at the centre, art unchanged. Accept?
2. **The children's table** (14): no child comes home until ch7. Is that the hook, or too heavy for six chapters?
3. **Wynn re-taken** (15): keep as a scripted story event, or move the cost to a building?
4. **Odile's death** (23): keep it, swap to Amos at the bell, or no finale death beyond Vane and Lisle? (Also "Thank you" to the gate: too cruel?)
5. **Wren's cost** (25): permanent stone finger, no script, no magic. Do her staff spells keep working in the post-game?
6. **The Hearth as the sixth town; Hearthdeep seven floors** (12, 13): accept?
7. **One freed-sleeper rule** (16): breathed up a shaft on dives, carried by hand at the table. Accept?
8. **Cinderholt's scripted failed dive and story gate** (18): accept?
9. **Vane's end** (24): dies at dawn with his heart, or a cell, alive and heartless?
10. **Rescued people are still bound** (4): accept as the reason only the four can speak against it?
11. **Solo scenes show all four** (30).
12. **Mirror floor playable** (27); and the silencing as the ugliest moment between the four: keep?
13. **Lieutenants come home** (20): Ivo and Dagny become residents with services; accept?
14. **Pip reads Gideon's confession** (3.15): too cruel for the hero the player may be playing?
15. **Diegetic people-left lines** (17): one token per dungeon, or lanterns everywhere?
16. **Nell home from ch2** and Col as the last sleeper: accept?
17. **Isolde leaves and stays gone**; Hob seeds (Hep's "big fellow", "He would", the desk): enough or too much?
18. **The triangle:** leave Tobin's love and the Wren/Ash kiss unresolved, or one line in the epilogue?
19. **Dramatic irony of Nell** (1.1): the player sees Tobin leave Nell from the first scene.
20. **The script's cost** (stone hands): story only, or touches play?

---

## 10. Placeholder names (the user renames)

| Group | Names |
|---|---|
| Heroes and pets | Wren Hale, Tobin Marsh, Pip Ostrander, Ash Calder (default names; renameable in play, STORY 3), Tuppence |
| Antagonists | Mawrrow the Hungering, Corwin Vane (names kept by the user, STORY), Ellen Vane, Bettany, the Gleaners |
| Hollowmere | Hollis Rye, Marta Fenn, Isolde Hale, Hob Marsh, Jessamy Marsh, Nell Marsh, Odile Ostrander, Jory Calder, Hester Calder, Abner Stroud, Davy and Col Stroud, Dun Harrow, Tansy and Merrin Brook, Bram Tolley, Wynn Cobb, Aldous Sedge, Amos Gill, Tilly Rowe, the Pell twins, the Sedge boy |
| Saltwake | Gideon and Dorrit Hask, Ivo Sallow, Teague Pellow, Maud Tiller, Kip Rudd, Agathe Voss; Weigh-Crab, Lamp-Lure; gutting-gulls, barnacle-men, scale-mites |
| Cinderholt | Osric and Corra Varn, Dagny and Ebben Ferrule, Hilde Saxby, Ludo Fitch, Bette Rook; the Bore, the Foreman's Engine; slag-hounds, lamp-heads, tallymen |
| Thistledown | Abbess Morwenna Pyle, Sister Clemency, Silas Crewe, Hep Lowe (grandson Dickon), Brother Fennick, Cantor Lisle; the Censer, the Queen (Mother Bounty) and Choir; comb-drones, candle-nuns, windfalls |
| Stillhollow | Provost Ambrose Wyck, Edda Quill, Jasper Tock, Prudence Ames, Sorrel Kett; the Curve; gnomon-knights, astrolabe-spiders, frost-clerks |
| The Hearth | Keeper Gerda Brand, Linnet Brand, Ned Tallow, Wat Hesketh, Old Bryn; the Keepers' Wagon |
| Undercroft and Brazen | Brazen Ox, Bell-Warden (STORY); the Peelman (floor 3 guard, book); spitjacks, scuttlers, kettle-imps, flue-wraiths |
| Legendary forms | Marshfell, Last Watch, Brother's Due, True North; the memory names in section 3.1 |
