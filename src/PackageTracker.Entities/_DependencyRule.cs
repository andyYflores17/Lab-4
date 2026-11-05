// DEPENDENCY RULE: Source code dependencies point only inward. Entities are the
// innermost layer: they reference no other project and no framework package, and
// use only the base library (System.*). Everything else may depend on them; they
// depend on nothing.
