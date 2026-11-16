import React, { useEffect, useState } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import type { SoundListItem } from '@src/types/plugin/api';
import CommonListPickerPopup from './CommonListPickerPopup';
import SoundManagerModal from '../managers/SoundManagerModal';

interface SoundPickerProps {
  open: boolean;
  referenceRef: React.RefObject<HTMLElement>;
  panelElement?: HTMLElement | null;
  selectedSound: string | null;
  onSoundSelect: (soundPath: string | null) => void;
  onClose: () => void;
  interactiveRefs?: Array<React.RefObject<HTMLElement>>;
  previewVolume?: number;
}

const SoundPicker = ({
  open,
  referenceRef,
  panelElement = null,
  selectedSound,
  onSoundSelect,
  onClose,
  interactiveRefs = [],
  previewVolume,
}: SoundPickerProps) => {
  const { t } = useTranslation();
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState<'all' | 'local'>('all');
  const [sounds, setSounds] = useState<SoundListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [showManager, setShowManager] = useState(false);

  const normalizedSelectedSound = (selectedSound || '').trim();

  const loadSounds = async () => {
    setIsLoading(true);
    setLoadError('');
    try {
      const nextSounds = await window.api.sound.list();
      setSounds(nextSounds);
    } catch (error) {
      console.error('Failed to load sound list', error);
      setLoadError('사운드 목록 로드 실패');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (!open) return;
    void loadSounds();
  }, [open]);

  const filterOptions = [
    { value: 'all', label: t('soundPicker.filterAll') || '전체' },
    { value: 'local', label: t('soundPicker.filterLocal') || '로컬 사운드' },
  ];

  const filteredSounds = (() => {
    const query = searchQuery.trim().toLowerCase();

    return sounds.filter((item) => {
      if (!item.enabled) return false;
      if (filterType === 'local' && item.source !== 'local') {
        return false;
      }
      if (!query) return true;
      return (
        item.fileName.toLowerCase().includes(query) ||
        (item.displayName?.toLowerCase().includes(query) ?? false)
      );
    });
  })();

  return (
    <>
      <CommonListPickerPopup<SoundListItem>
        open={open}
        referenceRef={referenceRef}
        panelElement={panelElement}
        interactiveRefs={interactiveRefs}
        onClose={onClose}
        chooserTitle={t('sidebar.pickSound')}
        searchQuery={searchQuery}
        onSearchQueryChange={setSearchQuery}
        searchPlaceholder={t('soundPicker.searchPlaceholder') || '검색'}
        filterOptions={filterOptions}
        filterValue={filterType}
        onFilterChange={(value) => setFilterType(value as 'all' | 'local')}
        items={filteredSounds}
        getItemKey={(item) => item.soundPath}
        getItemLabel={(item) => item.displayName || item.fileName}
        isItemSelected={(item) => item.soundPath === normalizedSelectedSound}
        onItemChoose={(item) => onSoundSelect(item.soundPath)}
        emptyText={t('soundPicker.noSounds') || '사운드 없음'}
        isLoading={isLoading}
        loadingText={t('propertiesPanel.loading') || '로딩...'}
        errorText={loadError}
        onAdd={() => setShowManager(true)}
        addLabel={t('soundPicker.add')}
      />

      <SoundManagerModal
        isOpen={showManager}
        selectedSound={selectedSound}
        onSelectSound={(path) => {
          onSoundSelect(path);
          void loadSounds();
        }}
        onClose={() => {
          setShowManager(false);
          void loadSounds();
        }}
        previewVolume={previewVolume}
      />
    </>
  );
};

export default SoundPicker;
