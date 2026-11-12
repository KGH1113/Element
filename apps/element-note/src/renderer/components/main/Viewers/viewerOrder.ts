export const moveTabId = (
  ids: string[],
  movingId: string,
  targetId: string,
): string[] => {
  if (movingId === targetId) return ids;
  const from = ids.indexOf(movingId);
  const to = ids.indexOf(targetId);
  if (from < 0 || to < 0) return ids;
  const next = [...ids];
  next.splice(from, 1);
  next.splice(to, 0, movingId);
  return next;
};

export const moveTabIdByOffset = (
  ids: string[],
  movingId: string,
  offset: -1 | 1,
): string[] => {
  const index = ids.indexOf(movingId);
  const target = index + offset;
  if (index < 0 || target < 0 || target >= ids.length) return ids;
  return moveTabId(ids, movingId, ids[target]);
};
