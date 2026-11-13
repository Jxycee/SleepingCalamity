# Area 1 and dungeon 1: needs list and asset picks

**Status:** from Fable Run 3, 2026-11-13. Goes with `area1-hollowmere.md` and `dungeon1-cellars.md`, which cite the codes below. Only what is named here gets downloaded.

## How to read this
- **MS** = Quixel Megascans on Fab. **HMF** = Megascans "Historic Medieval Farm" set. **HB** = Megascans "Historic Bazaar" set. **HM** = other Megascans "Historic Medieval" sets.
- **Free:** every pick had a licence tier priced at 0 in Fab's search data on 2026-11-13 (the pipeline drops paid listings). The session that downloads a pick opens its page once to confirm the licence and the format, and records both in the folder's `SOURCES.md` (`.claude/rules/art-assets.md`).
- **Format:** Megascans picks are FBX or glTF with texture sets. Take the 2K tier (1K for small props). Nothing above 2048 px.
- **Downloads need the user's Fab sign-in** in the browser pane. A build session cannot sign in.
- **Sheets** are in `Reference/private/asset-search-run3/sheets/` (gitignored, main checkout only). `_p2` sheets are the corrected second pass.
- **Judged by:** "sheet" means the pick was chosen by looking at the contact sheet. "title" means it was chosen from the listing title and set only, so the intake session looks at it before adopting.
- "ours" = already in the repo. "build" = make it ourselves (Blender generators, `sc-asset-craft`, `sc-foliage`).

## Hollowmere

| Code | Need | Pick | URL | Judged by | Sheet |
|---|---|---|---|---|---|
| K01 | Sky, morning and evening | Poly Haven CC0 "puresky" HDRIs: a partly cloudy midday (candidate `kloofendal_48d_partly_cloudy_puresky`) and a low sun (candidate `belfast_sunset_puresky`). Confirm the names at fetch | polyhaven.com | not searched (Poly Haven is outside the Fab pipeline) | - |
| T01 | Square and lane cobbles | MS Cobblestone Floor (pale rounded setts, square) | https://www.fab.com/listings/a12fdfb6-6448-4770-912c-09891a18e248 | sheet | T01_cobble_p2.jpg |
| | | MS River Cobblestone (gutters, lanes, edge bands) | https://www.fab.com/listings/35dddae4-2c52-4551-94d2-b11832ae0f08 | sheet | same |
| | | MS Cobblestone (chunky setts, spare) | https://www.fab.com/listings/c156689a-7686-4a90-9433-a18267a136bd | sheet | same |
| T02 | Dry-stone wall | MS Mossy Stone Wall (3D run) | https://www.fab.com/listings/2c7de1fc-7fe6-4598-90b4-eddf38c0507f | sheet | T02_drystone_p2.jpg |
| | | MS Japanese Stone Wall (low mossy run) | https://www.fab.com/listings/1ecfa17e-684c-42be-99de-65bd3183ba6e | sheet | same |
| T03 | Plaster with brick showing | MS Damaged Plaster (material) | https://www.fab.com/listings/6997de28-e2c8-42cc-9142-b604c70a75e1 | sheet | T03_plaster_p2.jpg |
| T04 | Scaffold, ladder | build: pole-and-lashing scaffold kit (nothing medieval on Fab; sheet shows only steel) | - | sheet | T04_scaffold_p2.jpg |
| | | MS Urban Miscellaneous Plank Scaffold Wood Weathered (deck boards) | https://www.fab.com/listings/68b560bd-4bb6-4d94-8d9f-ecc50f86368a | sheet | same |
| | | MS Military Trenches Equipment Ladder Wood S | https://www.fab.com/listings/f51f4188-1b62-4c06-a424-dd3a06c5cccc | title | - |
| T05 | Ruin debris | MS Roof Tile | https://www.fab.com/listings/d2d20427-ce69-4e41-bc55-55fcb9fe11fe | sheet | T05_rubble_p2.jpg |
| | | MS Limestone Rubble | https://www.fab.com/listings/6a6bfa70-8307-4b76-b55f-7fad9cbfcdea | sheet | same |
| | | MS Urban Ruin Scatter Debris Wood M 03 | https://www.fab.com/listings/469376ae-f9cd-4c19-97a4-9ea51a5d5011 | sheet | same |
| | | MS Urban Ruin Plank Wood M | https://www.fab.com/listings/13d0e61b-6030-4449-96dd-f6f3c43bc2d0 | sheet | same |
| T06 | Firewood | HMF Log Pile M | https://www.fab.com/listings/f601c8e2-e592-4ce5-bb95-ff7310a4db1e | title | T06_firewood_p2.jpg |
| | | MS Firewood | https://www.fab.com/listings/3152693d-926c-40e1-a427-2e3e119e7ed3 | title | same |
| T07 | Market stalls | ours (Ring1/Stalls, B8 MarketStall). No free listing is better | - | sheet | T07_stall_p2.jpg |
| T08 | Produce, bread, baskets | MS Cabbage | https://www.fab.com/listings/adb56322-78ea-41d1-acf5-2b40c8077488 | title | T08_produce_p2.jpg |
| | | MS Pumpkin | https://www.fab.com/listings/ef62cf24-abf7-48b8-96d6-552b4ebfa057 | title | same |
| | | MS Potato | https://www.fab.com/listings/769e97c5-5828-4853-8503-ecee1759230e | title | same |
| | | MS Red Apple | https://www.fab.com/listings/2646b42a-7be7-4504-abb9-92171d149ea4 | sheet | T10_appletree_p2.jpg |
| | | MS Bread Roll | https://www.fab.com/listings/00e7e55b-47db-4359-9a52-bc75e835660f | title | same as T08 |
| | | MS Half Walnut Bread | https://www.fab.com/listings/49705e8b-86f4-4db5-8bdb-af0c227336a9 | sheet | C10_oven_p2.jpg |
| | | HMF Basket Twig 01 | https://www.fab.com/listings/b20d9eac-bfcc-46e5-872d-9af61de59761 | title | - |
| T09 | Cloth | ours (WashingLine, Bunting, BannerPole, stall cloth) | - | - | T09_cloth_p2.jpg |
| | | HB Pile Fabric Old (cloth stall goods) | https://www.fab.com/listings/d8df1815-d1cc-4e3b-a469-60bd055aa049 | title | same |
| T10 | Orchard tree | build with `sc-foliage`. No free apple tree on Fab | - | sheet | T10_appletree_p2.jpg |
| T11 | Shore boulders | MS Mossy Forest Boulder | https://www.fab.com/listings/2079c412-c4d2-43e8-9bd1-125d6d988ed9 | title | T11_cliff.jpg |
| | | MS Nordic Beach Rock Formation | https://www.fab.com/listings/26f8fb5c-aa8e-4911-9db8-82805cee35f9 | title | same |
| T12 | Ferns, ivy, grass | MS Fern | https://www.fab.com/listings/bc67dbc4-912f-46ca-ab17-197211fa5d3f | sheet | T12_plants_p2.jpg |
| | | MS Lady Fern | https://www.fab.com/listings/3eead001-af7f-4b65-afcc-89c368f089bc | sheet | same |
| | | MS English Ivy | https://www.fab.com/listings/3b14e8ce-d4fe-4efa-af61-16335108b36f | sheet | same |
| | | MS Grass Clumps | https://www.fab.com/listings/e6d0a66a-3db0-4049-a1db-a1cfe1c1073b | sheet | same |
| | | MS Tufted Grass | https://www.fab.com/listings/0b787346-d36d-41c4-9f33-9662b433e076 | sheet | same |
| T13 | Boat | HM Harbour Boat Wood Old | https://www.fab.com/listings/b7ea9cbf-fd48-462b-8e7e-14eb87395739 | title | T13_jetty_p2.jpg |
| T14 | Smithy and builder's tools | MS Blacksmith Anvil | https://www.fab.com/listings/47e3c8f3-9cbe-4565-82e7-6527ba061be7 | title | T14_smithy_p2.jpg |
| | | MS Blacksmith Tongs | https://www.fab.com/listings/7e6a52a5-1eca-47b7-8724-2399dfe622d2 | title | same |
| | | HMF Toolbox Wood | https://www.fab.com/listings/f59ee803-a2ca-404e-9fc2-bfbb84f39891 | title | - |
| | | HMF Plane Wood | https://www.fab.com/listings/a6a688d7-979c-4f5e-8ccb-6de391fbe625 | title | - |
| T15 | Leaf litter | MS Autumn Leaves (decal) | https://www.fab.com/listings/c303884b-503d-47c8-b906-26157f6e5ae0 | title | T15_leaves_p2.jpg |
| | | MS Fallen Leaves (decal) | https://www.fab.com/listings/b5f169f0-1b20-49aa-b363-df9c0f5d35dd | title | same |
| T16 | Farm and yard kit (one scan family, for cohesion) | HMF Wagon Wood 02 | https://www.fab.com/listings/4908a992-5818-4632-ad97-3ab3ba550796 | title | - |
| | | HMF Cart Wood Worn | https://www.fab.com/listings/2d8a2fde-c4c5-449b-8458-cc556c11baba | title | - |
| | | HMF Wheelbarrow Wood 01 | https://www.fab.com/listings/8c26b0e8-ff72-498b-bba2-f43c98643f77 | title | - |
| | | HMF Barrel Wood 04 | https://www.fab.com/listings/501609b5-2fb2-4de2-ae91-13e98f4dc9a6 | title | - |
| | | HMF Bench Wood M 02 | https://www.fab.com/listings/c685dac0-242c-4374-99e7-6cb06f539089 | title | - |
| | | HMF Pen Wall 01 | https://www.fab.com/listings/40dac89b-63c1-46e9-8f78-37368642863a | title | - |
| | | HMF Wheel Wood 03 | https://www.fab.com/listings/c182ddb7-46fb-447b-9b89-5676d9952ab5 | title | - |
| | | HMF Stand Brazier | https://www.fab.com/listings/962e6b9a-0423-4c00-ae05-9f192a4311aa | title | - |
| | | HMF Table Wood S | https://www.fab.com/listings/2b2ce9d8-fa62-41ae-8535-8692dc93cd26 | title | - |
| | | HMF Stool Wood | https://www.fab.com/listings/1c3fefdc-2217-4f37-988f-0365f2d099f4 | title | - |
| | | HMF Rake Metal, Shovel Metal, Scythe Wood | https://www.fab.com/listings/580b3f90-f40b-4edb-ad9b-36bc6986a50d https://www.fab.com/listings/ca1c1dc2-f18a-4015-babc-b46ad869ff0d https://www.fab.com/listings/af97917b-cc5c-4978-a6c5-8f9b753f8953 | title | - |
| L04 | Flower pots and boxes | user's library: Stylized Plants in Pots (FBX). Check the style in the intake sheet | https://www.fab.com/listings/4c300dab-9fa5-4bdf-9509-6b0d934622a5 | library sheet | LIB_town.jpg |
| A01 | Hens, rooster | none usable on Fab (static or low-poly). The user's planned animal models, or the local AI pipeline | - | sheet | A01_chicken_p2.jpg |
| A02 | Cat | same as A01 | - | title | A02_cat_p2.jpg |
| A03 | Pigeons, sparrows, crows | ours (card birds; add a pigeon and a sparrow card). Spare: Game ready crow, Charlie catling (FBX) | https://www.fab.com/listings/21cd62fa-246d-41e6-a94f-55b8a8d962c7 | sheet | A03_birds_p2.jpg |
| A05 | Inn dog | user's library: Realistic Labrador, rigged, 19 animations (FBX) | https://www.fab.com/listings/8da333bc-5ebc-47c6-a6ab-aa711a8f36d3 | library sheet | LIB_town.jpg |
| S01 | Town and cellar sound | Essential Ambience SFX, Sound Kajiya | https://www.fab.com/listings/c4e9f60e-f1c1-433c-8b1b-fb5867da0d64 | title | - |
| | | Fantasy Audio Pack, Ambience + SFX, 3DDisco | https://www.fab.com/listings/f7805ee2-a4a3-4e31-84f3-f591112dbc64 | title | - |
| | | Caves and Dungeons, Sonus Novum | https://www.fab.com/listings/9ef17452-1566-4cb8-a8a3-d41d2c9549ed | title | - |
| | | user's library: HomkaH SFX packs (foley, loot, magic) | - | - | - |
| | | Still open: belfry wind, bell rope, hens, fire loops, cask and hook creaks. Fab's free audio is thin; look at Kenney and freesound CC0 | - | - | - |

## The Cellars

| Code | Need | Pick | URL | Judged by | Sheet |
|---|---|---|---|---|---|
| C01 | Walls and vaults | MS Old Brick Wall (pale tan brick: vaults, arches) | https://www.fab.com/listings/36da380f-fdb5-4904-81b3-c8dd4a1c7b9f | sheet | C01_wall_p2.jpg |
| | | MS Brick Wall (rough pale brick: walls, floors 1-4) | https://www.fab.com/listings/c2061c4a-c730-45b3-b0bb-6f05e05103f7 | sheet | same |
| | | MS Black Brick Facade (soot brick: floors 6-7) | https://www.fab.com/listings/6a8dcd47-e97f-43fb-9de2-76906e860f7e | sheet | same |
| | | MS Coal Mine Wall (bunker walls) | https://www.fab.com/listings/62ac840f-02f7-4f13-8c17-1162f4d81c98 | title | - |
| | | ours: vault_stone, limewash, old_brick, sandstone | - | - | - |
| C02 | Floors | MS Stone Floor (warm squared flags: main) | https://www.fab.com/listings/08fb79f8-c70f-4ecb-ba32-4bf808f3c188 | sheet | C02_floor_p2.jpg |
| | | MS Stone Floor (smooth worn slabs: bank room) | https://www.fab.com/listings/f1c4b161-760c-4801-b2fd-b3088ac0e85d | sheet | same |
| | | MS Mossy Stone Floor (damp patches, floor 1) | https://www.fab.com/listings/530b8be4-955d-48cf-ae5e-637ad37d11a4 | sheet | same |
| C03 | Moss, leak and puddle decals | MS Moss | https://www.fab.com/listings/5a5c5cbf-4ffa-4ac7-81a4-1bf632e6a20d | title | C03_decals_p2.jpg |
| | | MS Moss Patch | https://www.fab.com/listings/b5fad446-5ca0-44b1-96cf-74051936b0e6 | title | same |
| | | MS Industrial Decal Leak 02 | https://www.fab.com/listings/ff00c083-8e31-435e-8dfc-6b2c4528eea3 | title | same |
| | | MS Mud Stain (puddle base) | https://www.fab.com/listings/0c20290d-5a14-4003-aaec-eba1704a4f01 | title | same |
| | | ours: ambientCG Leaking set, B3 trims (drip, grime, moss, crack) | - | - | - |
| C04 | Cobwebs | Free Pack - Cobwebs, PolyOne Studio (FBX, Unity) | https://www.fab.com/listings/c09fb18e-eb8f-4a65-b0b6-c8a664d6b08d | title | C04_cobweb_p2.jpg |
| C05 | Hanging food | MS Smoked Sausage | https://www.fab.com/listings/505e62c8-0419-478c-be3f-cf524e47a262 | sheet | C05_hangfood_p2.jpg |
| | | MS Half Ham | https://www.fab.com/listings/089a9e41-8b23-4dd5-a01f-e1551be88c67 | sheet | same |
| | | MS Smoked Meat | https://www.fab.com/listings/84cadf6c-5a98-4e70-b931-4c31df3a7e47 | sheet | same |
| | | MS Garlic | https://www.fab.com/listings/754b8ce7-99a2-4f69-8aee-2651b9ae3854 | sheet | same |
| | | HB Plum Mixed Dried 02 (a string of dried fruit) | https://www.fab.com/listings/5e39a45c-e72f-4f58-b88a-3beff125e594 | sheet | same |
| | | herb bundles: build (foliage cards) | - | - | - |
| C06 | Crocks and jars | MS Clay Pot | https://www.fab.com/listings/031c9f53-3604-4b84-ac01-9ff78c4bc1c9 | title | C06_crocks_p2.jpg |
| | | MS Ceramic Jar | https://www.fab.com/listings/66259c45-ceed-4487-82fb-7e31ba8ef2bb | title | same |
| | | MS Old Jar | https://www.fab.com/listings/d81aa56c-924e-4f55-ae2d-1c05c41c25b1 | title | same |
| | | HMF Jug Clay S 01, Jug Clay M | https://www.fab.com/listings/6fc32b15-6a6f-4e88-9af5-76425bdbc5ae https://www.fab.com/listings/2a23f500-7469-4e0c-ad07-4f4e36e59899 | title | same |
| C07 | Wine | MS Barrel On Stand | https://www.fab.com/listings/3655c9b4-706d-4189-a821-b35ee3a6c632 | sheet | C07_wine_p2.jpg |
| | | MS Wooden Barrel | https://www.fab.com/listings/33945596-441a-43e5-9405-671a4517b0c2 | sheet | same |
| | | MS Ceramic Bottles Pack | https://www.fab.com/listings/e5cbf324-a0bd-4f9c-8175-ec4c9edade5e | sheet | same |
| | | CC0 - Wine Shelf, plaggy (rack; re-texture in our oak) | https://www.fab.com/listings/5a0267c3-c06f-4499-912d-bd7059063fbd | sheet | same |
| | | HMF Stand Barrel Rough | https://www.fab.com/listings/a2254298-f880-4a98-811b-1a1e786c342f | title | - |
| | | the Great Tun and the screw press: build | - | - | - |
| C08 | Coal | MS Coal Debris (material for our heaps) | https://www.fab.com/listings/6d4f763b-35c6-41be-9568-97923358950d | title | C08_coal_p2.jpg |
| | | MS Coal Ash | https://www.fab.com/listings/83550d6e-50a8-4493-ba20-09dbaa30ac4d | title | same |
| | | MS Burnt Firewood | https://www.fab.com/listings/68655f7a-2586-4090-ac08-0840a0043188 | sheet | C10_oven_p2.jpg |
| | | MS Old Pickaxe | https://www.fab.com/listings/778fb55f-f69d-45e6-83a7-a32b523e7a39 | title | - |
| | | coal heaps in three sizes: build | - | - | - |
| C09 | Carts and rails | MS Mine Cart (two iron tubs) | https://www.fab.com/listings/67026011-cc6a-4b2e-a669-2745e6d8c9ca | sheet | C09_minecart_p2.jpg |
| | | MS Mine Cart (open frame) | https://www.fab.com/listings/e24d13a1-da84-4c54-bffd-b5887a03e912 | sheet | same |
| | | MS Modular Railway Track | https://www.fab.com/listings/bb6f4e3b-bfc6-485b-8f9b-1ee857fa8cac | sheet | same |
| | | MS Mine Cart Wheels | https://www.fab.com/listings/93d7caf4-9d51-41e6-917e-e7464fa79d7f | sheet | same |
| C10 | Ovens and hearths | beehive bread oven, dead and lit, and the furnace door: build. Nothing free fits | - | sheet | C10_oven_p2.jpg |
| | | HMF Oven Clay | https://www.fab.com/listings/1dce3710-b51f-44e5-a4bd-6d8facba5b81 | title | - |
| | | HMF Stove Stone | https://www.fab.com/listings/f08386f3-7cbd-4c51-bfd4-d15a5c6c7150 | title | - |
| | | HMF Fireplace Stone 01 | https://www.fab.com/listings/2a9894f9-f98a-46c2-9a0b-da3d1b2ac212 | title | - |
| C11 | Baking | HMF Tool Baking 02 | https://www.fab.com/listings/1c2b6b73-30c1-43b8-addb-974cc97bebdd | title | - |
| | | HMF Sack Jute | https://www.fab.com/listings/ce29191d-4841-48e2-8c22-6c7ac6213d47 | title | C11_baking_p2.jpg |
| | | Flour bag, N1TAXE (FBX, glTF) | https://www.fab.com/listings/4d74e4f0-5039-48a5-a51d-d21833bcf58e | title | same |
| | | MS Bread | https://www.fab.com/listings/fa23606e-e514-47b8-9d62-2833886ae518 | title | same |
| | | HMF Mortar Wood L (dough trough stand-in) | https://www.fab.com/listings/c6a36cde-1be8-4632-bcc6-8cb15bacc0d2 | title | - |
| | | peel, trays, kneading table, proofing racks: build | - | - | - |
| C12 | Bank room camp | HMF Chest S (bank chests) | https://www.fab.com/listings/51252deb-ccd4-4b34-a113-d7dc00aac08e | title | - |
| | | MS Wooden Chest (iron-bound trunk) | https://www.fab.com/listings/ea9c89c3-ccd5-492d-972b-72d58815e7fc | sheet | C12_camp_p2.jpg |
| | | MS Urban Camping Gear Bag Canvas Large (bedroll) | https://www.fab.com/listings/6cd4ad87-3091-4aaa-9355-ffc0eb84f9a9 | sheet | same |
| | | HMF Candleholder Iron 01 | https://www.fab.com/listings/a0ebcd74-58aa-418e-a058-bc822ba10a36 | title | - |
| | | MS Old Candle | https://www.fab.com/listings/9046b203-59c7-47cf-be0e-7266c63893ca | sheet | C15_lantern_p2.jpg |
| C13 | Kit, doors, gates | kit: ours, rebuilt (no free kit fits; low-poly or Unreal-only) | - | title | C13_kit_p2.jpg |
| | | HM Castle Door Mixed Old 01 | https://www.fab.com/listings/ff6b535a-f1c4-4ae7-8ae9-186b05855df2 | title | - |
| | | HM Church Ruin Gate Metal Fence 01 | https://www.fab.com/listings/7fbe46b7-0501-4932-9621-155d1bcb629c | title | - |
| | | HM Castle Arch Stone Old 02, Doorway Stone Old 02 (shape references for our arches) | https://www.fab.com/listings/8ed661e1-afc6-4279-a335-b81d0e58f62d https://www.fab.com/listings/7829aac2-5cdb-4012-b687-0e3986acb52f | title | - |
| | | user's library: Dungeon Environment / 135+ Assets, PackDev (Unity and Unreal formats). Props only, after a style check | https://www.fab.com/listings/bb39bae4-7f7a-4127-b07e-151cf52db0f6 | library sheet | LIB_cellar.jpg |
| C14 | Roots, fungus | MS Dead Tree Root | https://www.fab.com/listings/1907e06d-41b8-4722-be9a-e7392d3dd005 | sheet | C14_roots_p2.jpg |
| | | MS Thai Beach Roots (long, for walls) | https://www.fab.com/listings/ab37701d-8d9f-42cf-ab99-7ce83fe64fb2 | sheet | same |
| | | MS Bolete Mushrooms | https://www.fab.com/listings/83c5683b-19c3-483f-bf9e-1e2c35de1ba8 | sheet | same |
| | | ours: Roots, Poly Haven root_cluster | - | - | - |
| C15 | Lights | Wall_Sconce, Get Dead Entertainment (iron cage sconce, as C1) | https://www.fab.com/listings/152bf785-ddf6-4057-9a80-8114473132cf | sheet | C15_lantern_p2.jpg |
| | | Medieval Lantern, Get Dead Entertainment (cage lantern, as C3) | https://www.fab.com/listings/b291be9b-b584-4c72-944f-e218835c38c7 | sheet | same |
| | | HMF Lamp Iron Hanging 03 (Megascans alternative) | https://www.fab.com/listings/fac84636-4669-48c0-80dd-d0412feed377 | title | - |
| | | Old Lantern, Sousinho Games (oil lantern, as C4) | https://www.fab.com/listings/17f4af61-5f14-45cf-a33c-3dc810997a21 | sheet | same |
| | | HMF Candle Beeswax Burnt 01 | https://www.fab.com/listings/81966cc5-11f7-44fa-8ce0-ff1213121b2f | sheet | same |
| | | HM Library Chandelier Metal Rusty 01 (floor 5 dome) | https://www.fab.com/listings/51879e64-6d4d-453b-8369-bbb977f9f1b7 | title | - |
| | | ours: CellarLantern (B11) | - | - | - |
| A04 | Rats | Rat Multi Animations (Textured), DaVe inc. (FBX) | https://www.fab.com/listings/ee58a3ee-bf89-494c-9f91-6b1c4ad12a27 | sheet | A04_rat_p2.jpg |
| | | Black Rat, Nestaeric (FBX, GLB; rig not confirmed) | https://www.fab.com/listings/3d8f4ca8-b9cf-4d82-a2b7-69882a0c8815 | sheet | same |

## The user's Fab library (`Reference/private/fab-library-2026-11-13.md`)
Judged from listing thumbnails only (sheets LIB_town.jpg, LIB_cellar.jpg).
| Item | Use |
|---|---|
| Megascans Saloon Interior | props only: barrels, bottles, stools, shelves. Same scan family as the picks above |
| PB Sword & Shield Inn, PB Thunder Hammer Forge (3DForge, Unity format) | not as buildings (we have our own inn and smithy). Mine the forge for tools and bellows if the style holds |
| Stylized Plants in Pots | window boxes and pots [L04] |
| Realistic Labrador | the inn dog [A05] |
| Dungeon Environment / 135+ Assets | small props only (candles, chains, grates), after a style check |
| Cursed Cozy Village | skip for now: stylised gothic, Unity format, likely off-palette |
| Stylized Library | skip: too cartoon for the cellars |
| Underground Tunnel | skip: Unreal-only, not found free |
| Texture sets by ehsan vaezi (wood, painted wood, burlap, chain) | spare tileables if ours fall short |
| Blood Decals | combat, not dressing |

## Notes on the search
- `Tools/asset_search/fab_search.py --type` sent a list and Fab answered HTTP 400. Fixed in this branch (one line). The first pass ran without the filter, and the style rank pushed scene-like thumbnails to the top, so those sheets are noisy. The second pass used the type filter, with style rank for materials and Fab's own relevance order (Megascans first) for objects.
- Searching Megascans by set name ("Historic Medieval Farm", "Historic Bazaar", "Old Mine") lists a whole matching prop family in one query. Use this first for any new need.
- The second-pass script is kept at `Reference/private/asset-search-run3/run3_pass2.py` (private; hard-coded paths).
