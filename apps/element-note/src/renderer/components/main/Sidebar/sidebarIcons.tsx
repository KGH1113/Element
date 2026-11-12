/** Sidebar icons: 16px, one stroke weight. Each names a thing or a part. */
const icon = (path: string, dash?: string) => (
  <svg
    width="16"
    height="16"
    viewBox="0 0 16 16"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.4"
    strokeLinecap="round"
    strokeLinejoin="round"
    strokeDasharray={dash}
    aria-hidden="true"
  >
    <path d={path} />
  </svg>
);

export const PART_ICONS = {
  input: icon('M2 4.5h12v7H2zM4.5 7h.01M7 7h.01M9.5 7h.01M12 7h.01M5 9.5h6'),
  sound: icon(
    'M2.5 6.5v3h2.2L8 12V4L4.7 6.5zM10.5 6a2.8 2.8 0 0 1 0 4M12.3 4.3a5.2 5.2 0 0 1 0 7.4',
  ),
  layout: icon(
    'M2.5 5.5v-3h3M10.5 2.5h3v3M13.5 10.5v3h-3M5.5 13.5h-3v-3M6 8h4M8 6v4',
  ),
  position: icon(
    'M8 2.5v11M2.5 8h11M6.2 4.300 8 2.5l1.800 1.800M6.200 11.700 8 13.500l1.800-1.800M4.300 6.200 2.500 8l1.800 1.800M11.700 6.200 13.500 8l-1.800 1.800',
  ),
  size: icon('M9.5 2.5h4v4M13.5 2.5 9 7M6.5 13.5h-4v-4M2.5 13.5 7 9'),
  rotation: icon('M13 8a5 5 0 1 1-1.600-3.700M13 2.800v2.700h-2.700'),
  visible: icon(
    'M1.800 8S4 3.800 8 3.800 14.200 8 14.200 8 12 12.200 8 12.200 1.800 8 1.800 8zM8 9.800a1.800 1.800 0 1 0 0-3.600 1.800 1.800 0 0 0 0 3.600z',
  ),
  speed: icon('M3 12a5.500 5.500 0 1 1 10 0M8 9.500l2.600-3.300'),
  reverse: icon(
    'M2.500 5.500h9.500M9.500 3l2.500 2.500L9.500 8M13.500 10.500H4M6.500 8 4 10.500 6.500 13',
  ),
  ghost: icon(
    'M4.500 2.500h7a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-7a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z',
    '2.200 2.200',
  ),
  shape: icon(
    'M4.5 2.5h7a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-7a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z',
  ),
  text: icon('M3.5 4.5v-1h9v1M8 3.5v9M6.5 12.5h3'),
  note: icon('M4 2.5v6M8 5v8.5M12 2.5v8'),
  counter: icon('M6 2.5 4.5 13.5M11.5 2.5 10 13.5M2.5 6h11M2 10h11'),
  details: icon('M2.5 4.5h11M2.5 8h11M2.5 11.5h11M6 3v3M10.5 6.5v3M5 10v3'),
  source: icon('M2.5 12.5V8M6 12.5v-9M9.5 12.5v-6M13 12.5V5'),
  image: icon(
    'M2.5 3.5h11v9h-11zM2.5 10.5l3-3 2.5 2.5 2-2 3.5 3.5M10.5 6.2h.01',
  ),
  add: icon('M8 3.5v9M3.5 8h9'),
  feel: icon('M2.5 12.5C8 12.5 8 3.5 13.5 3.5'),
  time: icon('M8 13.5a5.5 5.5 0 1 0 0-11 5.5 5.5 0 0 0 0 11zM8 5v3l2 1.5'),
  code: icon('M5.5 4.5 2 8l3.5 3.5M10.5 4.5 14 8l-3.5 3.5'),
} as const;

/** The kinds of element, each drawn as what it is. */
const SHELF_PATHS = {
  key: 'M3.500 3.500h9a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1h-9a1 1 0 0 1-1-1v-7a1 1 0 0 1 1-1zM6 9.500h4',
  stat: 'M3 12.500h10M4.500 10V7M8 10V4M11.500 10V6',
  graph: 'M2.500 12.500 6 8l2.500 2.500 5-6',
  knob: 'M8 13.500a5.500 5.500 0 1 0 0-11 5.500 5.500 0 0 0 0 11zM8 8 10.500 5',
  sprite:
    'M2.500 3.500h11v9h-11zM2.500 10.500l3-3 2.500 2.500 2-2 3.500 3.500M10.500 6.200h.010',
} as const;

type ShelfKind = keyof typeof SHELF_PATHS;

const shelfKinds = Object.keys(SHELF_PATHS) as ShelfKind[];

export const SHELF_ICONS = Object.fromEntries(
  shelfKinds.map((kind) => [kind, icon(SHELF_PATHS[kind])]),
) as Record<ShelfKind, ReturnType<typeof icon>>;

/**
 * A kind of element with a plus on its corner: one picture that says "add
 * this". The plus is part of the picture, with a little room cut out of the
 * kind around it, and the two together are centered as one.
 */
const addIcon = (kind: ShelfKind) => (
  <svg
    width="20"
    height="18"
    viewBox="0 0 20 18"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.4"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
  >
    <mask id={`en-add-${kind}`} maskUnits="userSpaceOnUse">
      <rect width="20" height="18" fill="white" stroke="none" />
      <circle cx="15" cy="4.5" r="4.6" fill="black" stroke="none" />
    </mask>
    <path
      d={SHELF_PATHS[kind]}
      transform="translate(0 2)"
      mask={`url(#en-add-${kind})`}
    />
    <path d="M15 1.900v5.200M12.400 4.500h5.200" strokeWidth="1.6" />
  </svg>
);

export const SHELF_ADD_ICONS = Object.fromEntries(
  shelfKinds.map((kind) => [kind, addIcon(kind)]),
) as Record<ShelfKind, ReturnType<typeof addIcon>>;
