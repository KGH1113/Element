import { useMemo, useState, type ReactNode } from 'react';
import { ChooserScopeContext, type ChooserScope } from './chooserScopeContext';

/** Knows where the sidebar is; wraps the whole editor window. */
export function ChooserScopeProvider({ children }: { children: ReactNode }) {
  const [element, setElement] = useState<HTMLElement | null>(null);
  const scope = useMemo<ChooserScope>(
    () => ({ element, setElement }),
    [element],
  );
  return (
    <ChooserScopeContext.Provider value={scope}>
      {children}
    </ChooserScopeContext.Provider>
  );
}
