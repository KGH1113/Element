import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from 'vitest';
import { useGridSelectionStore } from '@stores/grid/useGridSelectionStore';
import { useKeyStore } from '@stores/data/useKeyStore';
import {
  onViewerEditRequest,
  requestViewerEdit,
  selectViewerTab,
} from './viewerEditRequests';

describe('opening a viewer the game asked to edit', () => {
  let select: Mock<(mode: string) => void>;
  let clear: Mock<() => void>;
  const initialKeys = useKeyStore.getState();
  const initialSelection = useGridSelectionStore.getState();

  beforeEach(() => {
    select = vi.fn<(mode: string) => void>();
    clear = vi.fn<() => void>();
    useKeyStore.setState({
      isBootstrapped: true,
      selectedKeyType: 'hand-default',
      selectedViewerTabs: { hand: 'hand-default', foot: 'foot-custom' },
      setSelectedKeyType: select,
    });
    useGridSelectionStore.setState({ clearSelection: clear });
  });

  afterEach(() => {
    useKeyStore.setState(initialKeys, true);
    useGridSelectionStore.setState(initialSelection, true);
    document.body.innerHTML = '';
    window.__dmn_isKeyListening = false;
  });

  it('selects the tab that viewer shows and drops the old selection', () => {
    expect(selectViewerTab('foot')).toBe(true);
    expect(select).toHaveBeenCalledWith('foot-custom');
    expect(clear).toHaveBeenCalled();
  });

  it('leaves an already open tab and its selection alone', () => {
    expect(selectViewerTab('hand')).toBe(true);
    expect(select).not.toHaveBeenCalled();
    expect(clear).not.toHaveBeenCalled();
  });

  it('keeps an open dialog, a key being recorded and unloaded tabs', () => {
    const backdrop = document.createElement('div');
    backdrop.dataset.dmnModalBackdrop = 'true';
    document.body.append(backdrop);
    expect(selectViewerTab('foot')).toBe(false);
    backdrop.remove();
    window.__dmn_isKeyListening = true;
    expect(selectViewerTab('foot')).toBe(false);
    window.__dmn_isKeyListening = false;
    useKeyStore.setState({ isBootstrapped: false });
    expect(selectViewerTab('foot')).toBe(false);
    expect(select).not.toHaveBeenCalled();
  });

  it('delivers requests until the listener leaves', () => {
    const listener = vi.fn();
    const stop = onViewerEditRequest(listener);
    requestViewerEdit('hand');
    stop();
    requestViewerEdit('foot');
    expect(listener).toHaveBeenCalledTimes(1);
    expect(listener).toHaveBeenCalledWith('hand');
  });
});
