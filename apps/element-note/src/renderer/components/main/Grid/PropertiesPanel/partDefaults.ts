/**
 * The colors an element is drawn with until its own are set. A row that
 * shows an element's colors shows these, so it is never blank.
 */
export const KEY_FACE_COLORS = {
  background: 'rgba(46, 46, 47, 0.9)',
  border: 'rgba(113, 113, 113, 0.9)',
  font: 'rgba(121, 121, 121, 0.9)',
} as const;

export const GRAPH_COLORS = {
  line: '#86EFAC',
  background: 'rgba(17, 17, 20, 0.9)',
  border: 'rgba(255, 255, 255, 0.1)',
} as const;
