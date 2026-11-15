State: NOT merge-ready - usage limit hit before post-review fixes were re-checked
Branch: claude/net-test-gaps-bc1612
Done: 5 client-driven RPC tests, 8 late-join/drop tests, N1/N2 member rows + migration test, N3/N9/N12 sweep. `check` PASS on 516b94d7 (before review fixes).
Post-review edits (uncommitted-checked, need `check` rerun): N10 reverted (NGO always sets reason text; needs design call), Upgrade guard version<1 (hang on huge negative schemaVersion), test tightened.
Open: LateJoinStateTests L1 snapshot live enemies only; ClientDrivenRpcTests finish refusals assert in-range; N9 has no tests; D2 passes on HoldTimeout alone.
Risk review: no blockers.
