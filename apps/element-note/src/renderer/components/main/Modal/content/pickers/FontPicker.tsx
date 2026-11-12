import React, { useState } from 'react';
import { useTranslation } from '@contexts/useTranslation';
import { useFontStore } from '@stores/useFontStore';
import type { CustomFont } from '@src/types/settings/fonts';
import CommonListPickerPopup from './CommonListPickerPopup';

interface FontPickerProps {
  open: boolean;
  referenceRef: React.RefObject<HTMLElement>;
  panelElement?: HTMLElement | null;
  selectedFont: string | null;
  onFontSelect: (fontName: string | null) => void;
  onClose: () => void;
  onOpenManager: () => void;
  interactiveRefs?: Array<React.RefObject<HTMLElement>>;
}

type FilterType = 'all' | 'builtin' | 'local' | 'web';

const FontPicker = ({
  open,
  referenceRef,
  panelElement = null,
  selectedFont,
  onFontSelect,
  onClose,
  onOpenManager,
  interactiveRefs = [],
}: FontPickerProps) => {
  const { t } = useTranslation();
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState<FilterType>('all');

  const { builtinFonts, customFonts } = useFontStore();

  // 필터링된 폰트 목록
  const filteredFonts = (() => {
    let fonts: CustomFont[] = [...builtinFonts, ...customFonts].filter(
      (f) => f.enabled,
    );

    // 타입 필터
    if (filterType !== 'all') {
      fonts = fonts.filter((f) => f.type === filterType);
    }

    // 검색 필터
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase();
      fonts = fonts.filter(
        (f) =>
          f.displayName.toLowerCase().includes(query) ||
          f.name.toLowerCase().includes(query),
      );
    }

    return fonts;
  })();

  const enabledFontNames = (() => {
    const names = [...builtinFonts, ...customFonts]
      .filter((font) => font.enabled)
      .map((font) => font.name);
    return new Set(names);
  })();

  const effectiveSelectedFont =
    selectedFont && enabledFontNames.has(selectedFont) ? selectedFont : null;

  // 필터 옵션
  const filterOptions = [
    { value: 'all', label: t('fontPicker.filterAll') || '전체' },
    { value: 'builtin', label: t('fontPicker.filterBuiltin') || '내장' },
    { value: 'web', label: t('fontPicker.filterWeb') || '웹' },
    { value: 'local', label: t('fontPicker.filterLocal') || '로컬' },
  ];

  const handleFontClick = (font: CustomFont) => {
    onFontSelect(font.name);
  };

  return (
    <CommonListPickerPopup<CustomFont>
      open={open}
      referenceRef={referenceRef}
      panelElement={panelElement}
      interactiveRefs={interactiveRefs}
      onClose={onClose}
      chooserTitle={t('sidebar.pickFont')}
      searchQuery={searchQuery}
      onSearchQueryChange={setSearchQuery}
      searchPlaceholder={t('fontPicker.searchPlaceholder') || '검색...'}
      filterOptions={filterOptions}
      filterValue={filterType}
      onFilterChange={(value) => setFilterType(value as FilterType)}
      items={filteredFonts}
      getItemKey={(font) => font.id}
      getItemLabel={(font) => font.displayName}
      // Each font's name is written in that font: it shows what it is.
      getItemLabelStyle={(font) => ({ fontFamily: font.name, fontSize: 15 })}
      isItemSelected={(font) =>
        effectiveSelectedFont
          ? effectiveSelectedFont === font.name
          : font.name === 'SUIT-Regular'
      }
      onItemChoose={handleFontClick}
      emptyText={t('fontPicker.noFonts') || '폰트 없음'}
      onAdd={onOpenManager}
      addLabel={t('fontPicker.add')}
    />
  );
};

export default FontPicker;
