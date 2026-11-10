// Mirrors KeyboardManager's canonical-slot transitions for deterministic
// exports. Keep raw native slots: parsing a projected "A+B" label would confuse
// a real single key named "+" with an all-match binding.
export const slotMembers = (slot) =>
  typeof slot === 'string' ? (slot ? [slot] : []) : slot.keys;

export const slotCanonical = (slot) =>
  typeof slot === 'string'
    ? slot
    : slot.keys.join(slot.match === 'all' ? '+' : '|');

export function createRenderKeyRouter(slots) {
  const compiled = slots.map((slot) => ({
    canonical: slotCanonical(slot),
    members: slotMembers(slot),
    any: typeof slot !== 'string' && slot.match === 'any',
  }));
  const memberSlots = new Map();
  const canonicalSlots = new Map();
  for (const slot of compiled) {
    const equivalents = canonicalSlots.get(slot.canonical) ?? [];
    equivalents.push(slot);
    canonicalSlots.set(slot.canonical, equivalents);
    for (const member of slot.members) {
      const affected = memberSlots.get(member) ?? [];
      affected.push(slot);
      memberSlots.set(member, affected);
    }
  }
  const held = new Map();
  const labelCounts = new Map();
  const active = new Set();
  const activatedBy = new Map();
  const isSlotActive = (slot) =>
    slot.members.length > 0 &&
    (slot.any
      ? slot.members.some((key) => labelCounts.has(key))
      : slot.members.every((key) => labelCounts.has(key)));

  return {
    active,
    process(physicalKey, candidates, down, timeMs) {
      let entry;
      if (down) {
        if (held.has(physicalKey)) return [];
        entry = {
          label: candidates.find((candidate) => memberSlots.has(candidate)),
          timeMs,
        };
        held.set(physicalKey, entry);
        if (entry.label)
          labelCounts.set(entry.label, (labelCounts.get(entry.label) ?? 0) + 1);
      } else {
        entry = held.get(physicalKey);
        if (!entry) return [];
        held.delete(physicalKey);
        if (entry.label) {
          const count = (labelCounts.get(entry.label) ?? 1) - 1;
          if (count > 0) labelCounts.set(entry.label, count);
          else labelCounts.delete(entry.label);
        }
      }
      const pending = new Map();
      for (const slot of memberSlots.get(entry.label) ?? []) {
        pending.set(
          slot.canonical,
          (pending.get(slot.canonical) ?? false) ||
            slot.any ||
            slot.members.length === 1,
        );
      }
      return [...pending].map(([key, pressOnFreshDown]) => {
        const wasActive = active.has(key);
        const isActive = canonicalSlots.get(key).some(isSlotActive);
        const transition = wasActive === isActive ? null : isActive;
        let holdDurationMs;
        if (transition === true) activatedBy.set(key, physicalKey);
        else if (transition === false) {
          if (activatedBy.get(key) === physicalKey)
            holdDurationMs = Math.max(0, timeMs - entry.timeMs);
          activatedBy.delete(key);
        }
        if (isActive) active.add(key);
        else active.delete(key);
        return {
          key,
          transition,
          press: down && (pressOnFreshDown || transition === true),
          holdDurationMs,
        };
      });
    },
  };
}
