import { useEffect, useRef, useState } from 'react';
import Modal from '@components/main/Modal/Modal';
import { Card, CardTitle } from '@components/ui/Card';
import { Input } from '@components/ui/Input';
import { useTranslation } from '@contexts/useTranslation';
import { useKeyStore } from '@stores/data/useKeyStore';
import { imageApi } from '@api/modules/resourceApi';
import { resolveImageSource } from '@utils/core/imageSource';
import { probeImageSize } from '@utils/media/assetProbe';
import { computeSpriteImageStyle } from '@utils/sprite/spriteImageStyles';
import {
  placeSpriteVisual,
  spriteIdleVisual,
  spritePoseVisual,
} from '@utils/sprite/spritePlacement';
import {
  SPRITE_CONSTRAINTS,
  type ReactiveSpritePosition,
  type SpritePose,
  type SpriteTransform,
} from '@src/types/key/sprites';
import {
  applySpriteImage,
  createSpriteDraft,
  createSpritePose,
  normalizeSpriteDraft,
  validateSpriteDraft,
  type SpriteDraftError,
} from './spriteEditorDraft';

interface SpriteEditorModalProps {
  initial: ReactiveSpritePosition | null;
  initialPosition?: { dx: number; dy: number };
  mode: string;
  onSave: (sprite: ReactiveSpritePosition) => Promise<void>;
  onDelete?: () => Promise<void>;
  onClose: () => void;
}

const labels = {
  ko: {
    title: '스프라이트',
    description: '키 입력에 반응하는 이미지와 자세를 설정합니다.',
    preview: '미리보기',
    image: '이미지',
    choose: '이미지 선택',
    clear: '지우기',
    noImage: '이미지를 선택하세요',
    name: '이름',
    layout: '배치',
    width: '너비',
    height: '높이',
    rotation: '회전',
    visible: '표시',
    behavior: '입력 반응',
    whileHeld: '누르는 동안',
    onPress: '누를 때 한 번',
    duration: '재생 시간 (ms)',
    transition: '전환 시간 (ms)',
    easing: '전환 곡선',
    states: '자세',
    idle: '기본 자세',
    add: '자세 추가',
    remove: '자세 삭제',
    triggers: '반응할 키',
    triggersHint: '선택한 키가 함께 눌리면 이 자세가 적용됩니다.',
    noKeys: '이 탭에 키를 먼저 추가한 뒤 자세에 연결하세요.',
    pivot: '회전·크기 기준점',
    followPivot: '기본 기준점 사용',
    transform: '자세 변환',
    scale: '배율',
    poseImage: '자세 이미지',
    poseImageHint: '이미지를 지정하지 않으면 기본 이미지를 사용합니다.',
    save: '저장',
    saving: '저장 중…',
    cancel: '취소',
    delete: '스프라이트 삭제',
    deleteConfirm: '이 스프라이트를 삭제할까요?',
    confirmDelete: '삭제',
    keep: '계속 편집',
    css: 'CSS 클래스',
    zIndex: '레이어 순서',
    inline: '설정 스타일 우선',
    loadError:
      '이미지를 불러오지 못했습니다. 파일을 확인한 뒤 다시 선택하세요.',
    saveError:
      '변경사항을 저장하지 못했습니다. 입력은 유지됩니다. 다시 저장해 주세요.',
    range:
      '입력값의 범위를 확인해 주세요. 크기는 0보다 커야 하며 회전은 −180°부터 180°까지입니다.',
    emptyTriggers: '각 자세에 반응할 키를 하나 이상 선택해 주세요.',
    duplicateTriggers:
      '같은 키 조합을 사용하는 자세가 있습니다. 각 자세의 키 조합을 다르게 선택해 주세요.',
    easingError:
      '전환 곡선에 ease, linear 또는 유효한 cubic-bezier 값을 입력해 주세요.',
  },
  en: {
    title: 'Sprite',
    description: 'Set an image and poses that respond to key presses.',
    preview: 'Preview',
    image: 'Image',
    choose: 'Choose image',
    clear: 'Clear',
    noImage: 'Choose an image',
    name: 'Name',
    layout: 'Layout',
    width: 'Width',
    height: 'Height',
    rotation: 'Rotation',
    visible: 'Visible',
    behavior: 'Activation',
    whileHeld: 'While held',
    onPress: 'Once on press',
    duration: 'Play duration (ms)',
    transition: 'Transition (ms)',
    easing: 'Easing',
    states: 'Poses',
    idle: 'Idle pose',
    add: 'Add pose',
    remove: 'Remove pose',
    triggers: 'Trigger keys',
    triggersHint:
      'This pose applies while the selected keys are pressed together.',
    noKeys: 'Add keys to this tab before assigning a pose.',
    pivot: 'Rotation and scale pivot',
    followPivot: 'Use base pivot',
    transform: 'Pose transform',
    scale: 'Scale',
    poseImage: 'Pose image',
    poseImageHint: 'Leave empty to use the base image.',
    save: 'Save',
    saving: 'Saving…',
    cancel: 'Cancel',
    delete: 'Delete sprite',
    deleteConfirm: 'Delete this sprite?',
    confirmDelete: 'Delete',
    keep: 'Keep editing',
    css: 'CSS class',
    zIndex: 'Layer order',
    inline: 'Prefer configured styles',
    loadError:
      'The image could not be loaded. Check the file and choose it again.',
    saveError:
      'The changes could not be saved. Your edits are kept. Try saving again.',
    range:
      'Check the input ranges. Dimensions must be positive and rotation must be between −180° and 180°.',
    emptyTriggers: 'Choose at least one trigger key for each pose.',
    duplicateTriggers:
      'Two poses use the same key combination. Choose a different combination for each pose.',
    easingError: 'Enter ease, linear, or a valid cubic-bezier value.',
  },
  ru: {
    title: 'Спрайт',
    description: 'Настройте изображение и позы, реагирующие на нажатия клавиш.',
    preview: 'Предпросмотр',
    image: 'Изображение',
    choose: 'Выбрать изображение',
    clear: 'Очистить',
    noImage: 'Выберите изображение',
    name: 'Название',
    layout: 'Расположение',
    width: 'Ширина',
    height: 'Высота',
    rotation: 'Поворот',
    visible: 'Показывать',
    behavior: 'Реакция на ввод',
    whileHeld: 'При удержании',
    onPress: 'Один раз при нажатии',
    duration: 'Длительность (мс)',
    transition: 'Переход (мс)',
    easing: 'Кривая перехода',
    states: 'Позы',
    idle: 'Исходная поза',
    add: 'Добавить позу',
    remove: 'Удалить позу',
    triggers: 'Клавиши активации',
    triggersHint:
      'Поза применяется при одновременном нажатии выбранных клавиш.',
    noKeys: 'Сначала добавьте клавиши на эту вкладку.',
    pivot: 'Центр поворота и масштаба',
    followPivot: 'Использовать основной центр',
    transform: 'Преобразование позы',
    scale: 'Масштаб',
    poseImage: 'Изображение позы',
    poseImageHint: 'Если не задано, используется основное изображение.',
    save: 'Сохранить',
    saving: 'Сохранение…',
    cancel: 'Отмена',
    delete: 'Удалить спрайт',
    deleteConfirm: 'Удалить этот спрайт?',
    confirmDelete: 'Удалить',
    keep: 'Продолжить редактирование',
    css: 'Класс CSS',
    zIndex: 'Порядок слоя',
    inline: 'Приоритет настроенных стилей',
    loadError:
      'Не удалось загрузить изображение. Проверьте файл и выберите его снова.',
    saveError:
      'Не удалось сохранить изменения. Введённые данные сохранены в форме. Повторите попытку.',
    range:
      'Проверьте значения. Размеры должны быть больше нуля, а поворот — от −180° до 180°.',
    emptyTriggers: 'Выберите хотя бы одну клавишу для каждой позы.',
    duplicateTriggers:
      'Несколько поз используют одинаковую комбинацию клавиш. Выберите разные комбинации.',
    easingError: 'Введите ease, linear или допустимое значение cubic-bezier.',
  },
  'zh-cn': {
    title: '精灵',
    description: '设置响应按键的图像和姿势。',
    preview: '预览',
    image: '图像',
    choose: '选择图像',
    clear: '清除',
    noImage: '请选择图像',
    name: '名称',
    layout: '布局',
    width: '宽度',
    height: '高度',
    rotation: '旋转',
    visible: '显示',
    behavior: '输入响应',
    whileHeld: '按住时',
    onPress: '按下时播放一次',
    duration: '播放时长（毫秒）',
    transition: '过渡时长（毫秒）',
    easing: '过渡曲线',
    states: '姿势',
    idle: '默认姿势',
    add: '添加姿势',
    remove: '删除姿势',
    triggers: '触发按键',
    triggersHint: '同时按住所选按键时应用此姿势。',
    noKeys: '请先在此标签页中添加按键。',
    pivot: '旋转和缩放中心',
    followPivot: '使用默认中心',
    transform: '姿势变换',
    scale: '缩放',
    poseImage: '姿势图像',
    poseImageHint: '未指定时使用默认图像。',
    save: '保存',
    saving: '正在保存…',
    cancel: '取消',
    delete: '删除精灵',
    deleteConfirm: '删除此精灵？',
    confirmDelete: '删除',
    keep: '继续编辑',
    css: 'CSS 类',
    zIndex: '图层顺序',
    inline: '优先使用设置的样式',
    loadError: '无法加载图像。请检查文件后重新选择。',
    saveError: '无法保存更改。已保留输入内容，请重试。',
    range:
      '请检查输入范围。尺寸必须大于零，旋转角度须介于 −180° 和 180° 之间。',
    emptyTriggers: '请为每个姿势选择至少一个触发按键。',
    duplicateTriggers: '多个姿势使用相同的按键组合。请为各姿势选择不同的组合。',
    easingError: '请输入 ease、linear 或有效的 cubic-bezier 值。',
  },
  'zh-Hant': {
    title: '精靈',
    description: '設定回應按鍵的圖像和姿勢。',
    preview: '預覽',
    image: '圖像',
    choose: '選擇圖像',
    clear: '清除',
    noImage: '請選擇圖像',
    name: '名稱',
    layout: '配置',
    width: '寬度',
    height: '高度',
    rotation: '旋轉',
    visible: '顯示',
    behavior: '輸入回應',
    whileHeld: '按住時',
    onPress: '按下時播放一次',
    duration: '播放時間（毫秒）',
    transition: '過渡時間（毫秒）',
    easing: '過渡曲線',
    states: '姿勢',
    idle: '預設姿勢',
    add: '新增姿勢',
    remove: '刪除姿勢',
    triggers: '觸發按鍵',
    triggersHint: '同時按住所選按鍵時套用此姿勢。',
    noKeys: '請先在此分頁中新增按鍵。',
    pivot: '旋轉與縮放中心',
    followPivot: '使用預設中心',
    transform: '姿勢變換',
    scale: '縮放',
    poseImage: '姿勢圖像',
    poseImageHint: '未指定時使用預設圖像。',
    save: '儲存',
    saving: '正在儲存…',
    cancel: '取消',
    delete: '刪除精靈',
    deleteConfirm: '刪除此精靈？',
    confirmDelete: '刪除',
    keep: '繼續編輯',
    css: 'CSS 類別',
    zIndex: '圖層順序',
    inline: '優先使用設定的樣式',
    loadError: '無法載入圖像。請檢查檔案後重新選擇。',
    saveError: '無法儲存變更。已保留輸入內容，請重試。',
    range:
      '請檢查輸入範圍。尺寸必須大於零，旋轉角度須介於 −180° 和 180° 之間。',
    emptyTriggers: '請為每個姿勢選擇至少一個觸發按鍵。',
    duplicateTriggers: '多個姿勢使用相同的按鍵組合。請為各姿勢選擇不同的組合。',
    easingError: '請輸入 ease、linear 或有效的 cubic-bezier 值。',
  },
};

const buttonClass =
  'h-8 rounded-md border border-ui-line-subtle bg-ui-fill px-3 text-xs text-ui-fg-secondary transition-colors hover:bg-ui-fill-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ui-accent-soft disabled:pointer-events-none disabled:opacity-40';

function NumericField({
  label,
  value,
  onChange,
  min,
  max,
  step = 'any',
}: {
  label: string;
  value: number;
  onChange: (value: number) => void;
  min?: number;
  max?: number;
  step?: number | 'any';
}) {
  return (
    <label className="grid min-w-0 gap-1 text-xs text-ui-fg-muted">
      <span>{label}</span>
      <Input
        type="number"
        value={Number.isFinite(value) ? value : ''}
        min={min}
        max={max}
        step={step}
        onChange={(event) =>
          onChange(
            event.target.value === '' ? Number.NaN : Number(event.target.value),
          )
        }
      />
    </label>
  );
}

function TransformFields({
  value,
  onChange,
  rotationLabel,
  scaleLabel,
}: {
  value: SpriteTransform;
  onChange: (value: SpriteTransform) => void;
  rotationLabel: string;
  scaleLabel: string;
}) {
  return (
    <div className="grid grid-cols-4 gap-2">
      <NumericField
        label="X"
        value={value.x}
        min={-2000}
        max={2000}
        onChange={(x) => onChange({ ...value, x })}
      />
      <NumericField
        label="Y"
        value={value.y}
        min={-2000}
        max={2000}
        onChange={(y) => onChange({ ...value, y })}
      />
      <NumericField
        label={rotationLabel}
        value={value.rotation}
        min={-180}
        max={180}
        onChange={(rotation) => onChange({ ...value, rotation })}
      />
      <NumericField
        label={scaleLabel}
        value={value.scale}
        min={0.1}
        max={10}
        step={0.1}
        onChange={(scale) => onChange({ ...value, scale })}
      />
    </div>
  );
}

export default function SpriteEditorModal({
  initial,
  initialPosition,
  mode,
  onSave,
  onDelete,
  onClose,
}: SpriteEditorModalProps) {
  const { i18n } = useTranslation();
  const text = labels[i18n.language as keyof typeof labels] ?? labels.en;
  const [draft, setDraft] = useState(() =>
    createSpriteDraft(initial, initialPosition),
  );
  const [poseId, setPoseId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [confirmDelete, setConfirmDelete] = useState(false);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  const positionsByMode = useKeyStore((state) => state.positions);
  const mappingsByMode = useKeyStore((state) => state.keyMappings);
  const positions = positionsByMode[mode] ?? [];
  const mappings = mappingsByMode[mode] ?? [];
  const selectedPose =
    draft.poses.find((pose) => pose.poseId === poseId) ?? null;
  const patch = (changes: Partial<ReactiveSpritePosition>) =>
    setDraft((current) => ({ ...current, ...changes }));
  const patchPose = (changes: Partial<SpritePose>) =>
    setDraft((current) => ({
      ...current,
      poses: current.poses.map((pose) =>
        pose.poseId === poseId ? { ...pose, ...changes } : pose,
      ),
    }));
  const previewVisual = selectedPose
    ? spritePoseVisual(draft, selectedPose)
    : spriteIdleVisual(draft);
  const previewImage = resolveImageSource(previewVisual.src || undefined);
  const previewTransform = selectedPose?.transform ?? draft.idleTransform;
  const previewPivot = previewVisual.pivot;
  const previewScale = Math.min(
    1,
    160 / Math.max(draft.width || 200, draft.height || 200),
  );

  const chooseImage = async (target: 'base' | 'pose') => {
    const targetPoseId = poseId;
    setBusy(true);
    setError('');
    try {
      const result = await imageApi.load();
      if (!mounted.current) return;
      if (!result.success || !result.imagePath) {
        if (
          result.error &&
          result.error !== 'cancelled' &&
          result.error !== 'canceled'
        )
          setError(text.loadError);
        return;
      }
      const source = result.imagePath;
      const naturalSize = await probeImageSize(source);
      if (!mounted.current) return;
      if (
        !naturalSize ||
        naturalSize.width > SPRITE_CONSTRAINTS.imageMetrics.dimensionMax ||
        naturalSize.height > SPRITE_CONSTRAINTS.imageMetrics.dimensionMax
      ) {
        setError(text.loadError);
        return;
      }
      setDraft((current) =>
        applySpriteImage(
          current,
          source,
          naturalSize,
          target === 'base' ? null : targetPoseId,
        ),
      );
    } catch {
      if (mounted.current) setError(text.loadError);
    } finally {
      if (mounted.current) setBusy(false);
    }
  };

  const save = async () => {
    const validation = validateSpriteDraft(draft);
    if (validation) {
      const messages: Record<SpriteDraftError, string> = {
        range: text.range,
        emptyTriggers: text.emptyTriggers,
        duplicateTriggers: text.duplicateTriggers,
        easing: text.easingError,
      };
      setError(messages[validation]);
      return;
    }
    setBusy(true);
    setError('');
    try {
      await onSave(normalizeSpriteDraft(draft));
    } catch {
      if (mounted.current) setError(text.saveError);
    } finally {
      if (mounted.current) setBusy(false);
    }
  };
  const remove = async () => {
    if (!onDelete) return;
    setBusy(true);
    setError('');
    try {
      await onDelete();
    } catch {
      if (mounted.current) setError(text.saveError);
    } finally {
      if (mounted.current) setBusy(false);
    }
  };

  return (
    <Modal
      onClick={() => {
        if (!busy) onClose();
      }}
      ariaLabel={text.title}
      closeOnEscape={!busy}
    >
      <form
        className="flex max-h-[calc(100vh-120px)] w-[min(820px,calc(100vw-40px))] flex-col overflow-hidden rounded-2xl border border-ui-line bg-ui-panel text-ui-fg-primary shadow-2xl"
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
      >
        <header className="border-b border-ui-line-subtle px-5 py-4">
          <h2 className="text-base font-semibold">{text.title}</h2>
          <p className="mt-1 text-xs text-ui-fg-muted">{text.description}</p>
        </header>
        <fieldset
          disabled={busy}
          className="grid min-h-0 gap-4 overflow-y-auto p-4 md:grid-cols-[240px_1fr]"
        >
          <aside className="space-y-3">
            <Card>
              <CardTitle>{text.preview}</CardTitle>
              <div
                className="relative flex h-48 items-center justify-center overflow-hidden rounded-lg bg-ui-inset"
                aria-label={text.preview}
              >
                {previewImage ? (
                  <div
                    className="absolute"
                    style={{
                      width: draft.width || 200,
                      height: draft.height || 200,
                      transform: `scale(${previewScale}) rotate(${draft.rotation || 0}deg)`,
                    }}
                  >
                    <img
                      src={previewImage}
                      alt=""
                      style={computeSpriteImageStyle(
                        { useInlineStyles: true },
                        previewTransform,
                        undefined,
                        placeSpriteVisual(draft, previewVisual),
                      )}
                    />
                  </div>
                ) : (
                  <span className="text-xs text-ui-fg-muted">
                    {text.noImage}
                  </span>
                )}
              </div>
            </Card>
            <Card>
              <CardTitle>{text.image}</CardTitle>
              <button
                type="button"
                className={`${buttonClass} w-full`}
                onClick={() => void chooseImage('base')}
              >
                {text.choose}
              </button>
              <p
                className="my-2 truncate text-xs text-ui-fg-muted"
                title={draft.baseImage || ''}
              >
                {draft.baseImage?.split(/[\\/]/).pop() || text.noImage}
              </p>
              <button
                type="button"
                disabled={!draft.baseImage}
                className={buttonClass}
                onClick={() =>
                  patch({
                    baseImage: null,
                    referenceNaturalSize: draft.referenceNaturalSize
                      ? { ...draft.referenceNaturalSize, source: null }
                      : null,
                  })
                }
              >
                {text.clear}
              </button>
            </Card>
            <Card>
              <CardTitle>{text.states}</CardTitle>
              <div className="grid gap-1" role="group" aria-label={text.states}>
                <button
                  type="button"
                  className={`${buttonClass} text-left ${!selectedPose ? 'bg-ui-accent-soft text-ui-accent-text' : ''}`}
                  onClick={() => setPoseId(null)}
                >
                  {text.idle}
                </button>
                {draft.poses.map((pose, index) => (
                  <button
                    type="button"
                    key={pose.poseId}
                    className={`${buttonClass} truncate text-left ${pose.poseId === poseId ? 'bg-ui-accent-soft text-ui-accent-text' : ''}`}
                    onClick={() => setPoseId(pose.poseId)}
                  >
                    {pose.name?.trim() || `${text.states} ${index + 1}`}
                  </button>
                ))}
                <button
                  type="button"
                  disabled={draft.poses.length >= SPRITE_CONSTRAINTS.maxPoses}
                  className={`${buttonClass} mt-2`}
                  onClick={() => {
                    const pose = createSpritePose();
                    setDraft((current) => ({
                      ...current,
                      poses: [...current.poses, pose],
                    }));
                    setPoseId(pose.poseId);
                  }}
                >
                  {text.add}
                </button>
              </div>
            </Card>
          </aside>
          <div className="space-y-3">
            {!selectedPose ? (
              <>
                <Card>
                  <CardTitle>{text.layout}</CardTitle>
                  <div className="space-y-3">
                    <label className="grid gap-1 text-xs text-ui-fg-muted">
                      {text.name}
                      <Input
                        value={draft.layerName || ''}
                        onChange={(event) =>
                          patch({ layerName: event.target.value || null })
                        }
                      />
                    </label>
                    <div className="grid grid-cols-2 gap-2">
                      <NumericField
                        label="X"
                        value={draft.dx}
                        onChange={(dx) => patch({ dx })}
                      />
                      <NumericField
                        label="Y"
                        value={draft.dy}
                        onChange={(dy) => patch({ dy })}
                      />
                      <NumericField
                        label={text.width}
                        value={draft.width}
                        min={1}
                        onChange={(width) => patch({ width })}
                      />
                      <NumericField
                        label={text.height}
                        value={draft.height}
                        min={1}
                        onChange={(height) => patch({ height })}
                      />
                      <NumericField
                        label={text.rotation}
                        value={draft.rotation}
                        min={-180}
                        max={180}
                        onChange={(rotation) => patch({ rotation })}
                      />
                      <NumericField
                        label={text.zIndex}
                        value={draft.zIndex ?? 0}
                        onChange={(zIndex) => patch({ zIndex })}
                      />
                    </div>
                    <label className="flex items-center gap-2 text-xs">
                      <input
                        type="checkbox"
                        checked={!draft.hidden}
                        onChange={(event) =>
                          patch({ hidden: !event.target.checked })
                        }
                      />
                      {text.visible}
                    </label>
                  </div>
                </Card>
                <Card>
                  <CardTitle>{text.behavior}</CardTitle>
                  <div className="space-y-3">
                    <label className="grid gap-1 text-xs text-ui-fg-muted">
                      {text.behavior}
                      <select
                        className="h-8 rounded-md border border-ui-line-subtle bg-ui-inset px-2 text-ui-fg"
                        value={draft.activation}
                        onChange={(event) =>
                          patch({
                            activation: event.target
                              .value as ReactiveSpritePosition['activation'],
                          })
                        }
                      >
                        <option value="whileHeld">{text.whileHeld}</option>
                        <option value="onPress">{text.onPress}</option>
                      </select>
                    </label>
                    <div className="grid grid-cols-2 gap-2">
                      <NumericField
                        label={text.transition}
                        value={draft.transitionMs}
                        min={0}
                        max={1000}
                        onChange={(transitionMs) => patch({ transitionMs })}
                      />
                      {draft.activation === 'onPress' && (
                        <NumericField
                          label={text.duration}
                          value={draft.pressDurationMs}
                          min={1}
                          max={5000}
                          onChange={(pressDurationMs) =>
                            patch({ pressDurationMs })
                          }
                        />
                      )}
                    </div>
                    <label className="grid gap-1 text-xs text-ui-fg-muted">
                      {text.easing}
                      <Input
                        value={draft.transitionEasing}
                        onChange={(event) =>
                          patch({ transitionEasing: event.target.value })
                        }
                      />
                    </label>
                  </div>
                </Card>
              </>
            ) : (
              <Card>
                <CardTitle>{text.states}</CardTitle>
                <div className="space-y-3">
                  <label className="grid gap-1 text-xs text-ui-fg-muted">
                    {text.name}
                    <Input
                      value={selectedPose.name || ''}
                      onChange={(event) =>
                        patchPose({ name: event.target.value || null })
                      }
                    />
                  </label>
                  <p className="text-xs font-medium">{text.triggers}</p>
                  <p className="text-xs text-ui-fg-muted">
                    {text.triggersHint}
                  </p>
                  <div className="flex max-h-32 flex-wrap gap-2 overflow-y-auto">
                    {positions
                      .filter((position) => position.id)
                      .map((position) => {
                        const index = positions.indexOf(position);
                        const id = position.id!;
                        return (
                          <label
                            key={id}
                            className="flex items-center gap-1 rounded border border-ui-line-subtle px-2 py-1 text-xs"
                          >
                            <input
                              type="checkbox"
                              checked={selectedPose.triggers.includes(id)}
                              onChange={(event) =>
                                patchPose({
                                  triggers: event.target.checked
                                    ? [...selectedPose.triggers, id]
                                    : selectedPose.triggers.filter(
                                        (trigger) => trigger !== id,
                                      ),
                                })
                              }
                            />
                            {position.displayText ||
                              mappings[index] ||
                              `Key ${index + 1}`}
                          </label>
                        );
                      })}
                    {positions.length === 0 && (
                      <p className="text-xs text-ui-fg-muted">{text.noKeys}</p>
                    )}
                  </div>
                  <p className="text-xs font-medium">{text.poseImage}</p>
                  <p className="text-xs text-ui-fg-muted">
                    {text.poseImageHint}
                  </p>
                  <div className="flex gap-2">
                    <button
                      type="button"
                      className={buttonClass}
                      onClick={() => void chooseImage('pose')}
                    >
                      {text.choose}
                    </button>
                    <button
                      type="button"
                      className={buttonClass}
                      disabled={!selectedPose.imageOverride}
                      onClick={() =>
                        patchPose({
                          imageOverride: null,
                          imageOverrideMetrics: null,
                        })
                      }
                    >
                      {text.clear}
                    </button>
                  </div>
                  <button
                    type="button"
                    className={`${buttonClass} text-red-400`}
                    onClick={() => {
                      patch({
                        poses: draft.poses.filter(
                          (pose) => pose.poseId !== poseId,
                        ),
                      });
                      setPoseId(null);
                    }}
                  >
                    {text.remove}
                  </button>
                </div>
              </Card>
            )}
            <Card>
              <CardTitle>{text.transform}</CardTitle>
              <TransformFields
                value={selectedPose?.transform ?? draft.idleTransform}
                onChange={(transform) =>
                  selectedPose
                    ? patchPose({ transform })
                    : patch({ idleTransform: transform })
                }
                rotationLabel={text.rotation}
                scaleLabel={text.scale}
              />
            </Card>
            <Card>
              <CardTitle>{text.pivot}</CardTitle>
              {selectedPose && (
                <label className="mb-3 flex items-center gap-2 text-xs">
                  <input
                    type="checkbox"
                    checked={selectedPose.pivot == null}
                    onChange={(event) =>
                      patchPose({
                        pivot: event.target.checked ? null : { ...draft.pivot },
                      })
                    }
                  />
                  {text.followPivot}
                </label>
              )}
              {(!selectedPose || selectedPose.pivot) && (
                <div className="grid grid-cols-2 gap-2">
                  <NumericField
                    label="X"
                    value={previewPivot.x}
                    min={0}
                    max={1}
                    step={0.05}
                    onChange={(x) =>
                      selectedPose
                        ? patchPose({ pivot: { ...previewPivot, x } })
                        : patch({ pivot: { ...previewPivot, x } })
                    }
                  />
                  <NumericField
                    label="Y"
                    value={previewPivot.y}
                    min={0}
                    max={1}
                    step={0.05}
                    onChange={(y) =>
                      selectedPose
                        ? patchPose({ pivot: { ...previewPivot, y } })
                        : patch({ pivot: { ...previewPivot, y } })
                    }
                  />
                </div>
              )}
            </Card>
            {!selectedPose && (
              <Card>
                <label className="grid gap-1 text-xs text-ui-fg-muted">
                  {text.css}
                  <Input
                    value={draft.className || ''}
                    onChange={(event) =>
                      patch({ className: event.target.value || null })
                    }
                  />
                </label>
                <label className="mt-3 flex items-center gap-2 text-xs">
                  <input
                    type="checkbox"
                    checked={draft.useInlineStyles === true}
                    onChange={(event) =>
                      patch({ useInlineStyles: event.target.checked })
                    }
                  />
                  {text.inline}
                </label>
              </Card>
            )}
          </div>
        </fieldset>
        <footer className="space-y-3 border-t border-ui-line-subtle p-4">
          {error && (
            <p role="alert" className="text-xs text-red-400">
              {error}
            </p>
          )}
          {confirmDelete ? (
            <div className="flex items-center gap-2">
              <p className="mr-auto text-sm">{text.deleteConfirm}</p>
              <button
                type="button"
                disabled={busy}
                className={buttonClass}
                onClick={() => setConfirmDelete(false)}
              >
                {text.keep}
              </button>
              <button
                type="button"
                disabled={busy}
                className={`${buttonClass} text-red-400`}
                onClick={() => void remove()}
              >
                {text.confirmDelete}
              </button>
            </div>
          ) : (
            <div className="flex gap-2">
              {onDelete && (
                <button
                  type="button"
                  disabled={busy}
                  className={`${buttonClass} text-red-400`}
                  onClick={() => setConfirmDelete(true)}
                >
                  {text.delete}
                </button>
              )}
              <div className="flex-1" />
              <button
                type="button"
                disabled={busy}
                className={buttonClass}
                onClick={onClose}
              >
                {text.cancel}
              </button>
              <button
                type="submit"
                disabled={busy}
                className={`${buttonClass} bg-ui-accent-soft text-ui-accent-text`}
              >
                {busy ? text.saving : text.save}
              </button>
            </div>
          )}
        </footer>
      </form>
    </Modal>
  );
}
