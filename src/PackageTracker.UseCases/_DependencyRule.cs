// DEPENDENCY RULE: Source code dependencies point only inward. Use cases may depend
// on entities (and the base library, System.*) but never on interface adapters or
// frameworks & drivers. Outer needs, like persistence, are expressed as interfaces
// (ports) defined here and implemented by outer layers.
