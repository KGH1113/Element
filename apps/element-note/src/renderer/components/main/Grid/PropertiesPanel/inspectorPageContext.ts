import { createContext, useContext } from 'react';

/** What the sidebar tells the settings of the part it has open. */
export interface InspectorPageContextValue {
  /** The open part. Pickers close when it changes. */
  activePage: string | null;
  /** Setting groups to show; null shows every group of the part. */
  groups: readonly string[] | null;
}

export const InspectorPageContext =
  createContext<InspectorPageContextValue | null>(null);
export const useInspectorPages = () => useContext(InspectorPageContext);
