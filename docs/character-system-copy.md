# Character-system description review

TODO: Whenever attribute formulas, skill prerequisites, social interactions, loot,
movement, combat, or other character systems change, review
[`Config/attributes.json`](../Config/attributes.json) and
[`Config/skills.json`](../Config/skills.json) in the same change.

The Level Up Attributes tab displays the text in `Config/attributes.json`.
Check each entry against the implemented formula and remove provisional wording
when a system gains real behaviour. The Skills tab displays catalogue
descriptions and prerequisites; update those when abilities gain effects.
Charisma and Luck currently only gate skills, and the selectable skills have no
gameplay effects.
