// DEPENDENCY RULE: Source code dependencies point only inward. Interface adapters may
// depend on use cases and entities, but never on frameworks & drivers. They convert
// data between the shape the use cases want and the shape the outside world uses, and
// implement the ports the use cases define.
