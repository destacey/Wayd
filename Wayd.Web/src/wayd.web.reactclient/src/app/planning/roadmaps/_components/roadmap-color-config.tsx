'use client'

import { WaydColorPicker } from '@/src/components/common'
import {
  DndContext,
  DragEndEvent,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import {
  SortableContext,
  arrayMove,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import {
  DeleteOutlined,
  HolderOutlined,
  PlusOutlined,
  StarFilled,
  StarOutlined,
} from '@ant-design/icons'
import { Button, Input, Space, Tooltip, Typography, theme } from 'antd'
import { CSSProperties } from 'react'

const { Text } = Typography
const { useToken } = theme

/**
 * A single configurable color on the roadmap. `key` is a stable client-side identifier
 * used for list rendering and drag reordering only; it is not persisted (the color hex is
 * the natural key on the server).
 */
export interface RoadmapColorConfigEntry {
  key: string
  color?: string
  name: string
  isDefault: boolean
}

/** Per-row validation errors, keyed by entry key, plus an overall flag. */
export interface RoadmapColorConfigErrors {
  hasErrors: boolean
  byKey: Record<string, { color?: string; name?: string }>
}

/**
 * Validates the configured colors: every entry needs a color and a caption, and colors
 * must be unique (the color is the natural key). Returns per-row errors so the form can
 * render them inline and disable Save.
 */
export const validateColorEntries = (
  entries: RoadmapColorConfigEntry[],
): RoadmapColorConfigErrors => {
  const byKey: RoadmapColorConfigErrors['byKey'] = {}

  // Count colors to flag duplicates (case-insensitive, matching the domain).
  const colorCounts = new Map<string, number>()
  for (const e of entries) {
    if (e.color) {
      const normalized = e.color.toUpperCase()
      colorCounts.set(normalized, (colorCounts.get(normalized) ?? 0) + 1)
    }
  }

  for (const e of entries) {
    const rowErrors: { color?: string; name?: string } = {}
    if (!e.color) {
      rowErrors.color = 'Select a color.'
    } else if ((colorCounts.get(e.color.toUpperCase()) ?? 0) > 1) {
      rowErrors.color = 'Duplicate color.'
    }
    if (!e.name.trim()) {
      rowErrors.name = 'Enter a caption.'
    }
    if (rowErrors.color || rowErrors.name) {
      byKey[e.key] = rowErrors
    }
  }

  return { hasErrors: Object.keys(byKey).length > 0, byKey }
}

export interface RoadmapColorConfigProps {
  value: RoadmapColorConfigEntry[]
  onChange: (entries: RoadmapColorConfigEntry[]) => void
  errors?: RoadmapColorConfigErrors
}

interface ColorConfigRowProps {
  entry: RoadmapColorConfigEntry
  errors?: { color?: string; name?: string }
  onColorChange: (color: string | undefined) => void
  onNameChange: (name: string) => void
  onToggleDefault: () => void
  onRemove: () => void
}

const ColorConfigRow = ({
  entry,
  errors,
  onColorChange,
  onNameChange,
  onToggleDefault,
  onRemove,
}: ColorConfigRowProps) => {
  const { token } = useToken()
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } =
    useSortable({ id: entry.key })

  const style: CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.4 : 1,
    padding: '4px 0',
  }

  const rowError = errors?.color ?? errors?.name

  return (
    <div ref={setNodeRef} style={style}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <Button
          type="text"
          size="small"
          icon={<HolderOutlined />}
          style={{ cursor: 'grab', color: token.colorTextTertiary }}
          {...attributes}
          {...listeners}
          aria-label="Drag to reorder"
        />
        <WaydColorPicker value={entry.color} onChange={onColorChange} />
        <Input
          size="small"
          placeholder="Caption (e.g. At Risk)"
          value={entry.name}
          maxLength={32}
          showCount
          status={errors?.name ? 'error' : undefined}
          onChange={(e) => onNameChange(e.target.value)}
          style={{ flex: 1 }}
        />
      <Tooltip
        title={entry.isDefault ? 'Default color' : 'Set as default color'}
      >
        <Button
          type="text"
          size="small"
          icon={
            entry.isDefault ? (
              <StarFilled style={{ color: token.colorWarning }} />
            ) : (
              <StarOutlined />
            )
          }
          onClick={onToggleDefault}
          aria-label={entry.isDefault ? 'Default color' : 'Set as default color'}
        />
      </Tooltip>
        <Button
          type="text"
          size="small"
          danger
          icon={<DeleteOutlined />}
          onClick={onRemove}
          aria-label="Remove color"
        />
      </div>
      {rowError && (
        <Text
          type="danger"
          style={{ fontSize: token.fontSizeSM, paddingLeft: 40 }}
        >
          {rowError}
        </Text>
      )}
    </div>
  )
}

/**
 * An editable, reorderable list of a roadmap's configured colors. Each row pairs a color
 * with a caption; one row may be marked as the default. The list is managed as a whole and
 * saved together.
 */
const RoadmapColorConfig = ({
  value,
  onChange,
  errors,
}: RoadmapColorConfigProps) => {
  const { token } = useToken()
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
  )

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event
    if (!over || active.id === over.id) return

    const oldIndex = value.findIndex((e) => e.key === active.id)
    const newIndex = value.findIndex((e) => e.key === over.id)
    if (oldIndex === -1 || newIndex === -1) return

    onChange(arrayMove(value, oldIndex, newIndex))
  }

  const updateEntry = (
    key: string,
    changes: Partial<RoadmapColorConfigEntry>,
  ) => {
    onChange(value.map((e) => (e.key === key ? { ...e, ...changes } : e)))
  }

  const toggleDefault = (key: string) => {
    onChange(
      value.map((e) => ({
        ...e,
        // Only one entry can be the default; toggling one clears the others.
        isDefault: e.key === key ? !e.isDefault : false,
      })),
    )
  }

  const removeEntry = (key: string) => {
    onChange(value.filter((e) => e.key !== key))
  }

  const addEntry = () => {
    onChange([
      ...value,
      {
        key: crypto.randomUUID(),
        color: undefined,
        name: '',
        isDefault: false,
      },
    ])
  }

  return (
    <Space orientation="vertical" style={{ width: '100%' }} size={8}>
      <Text type="secondary" style={{ fontSize: token.fontSizeSM }}>
        {value.length === 0
          ? 'No colors configured. Activities use the standard color palette and the theme color when no color is set.'
          : 'Activities without a color use your theme color, unless one of these colors is marked as the default (★).'}
      </Text>

      <DndContext
        sensors={sensors}
        collisionDetection={closestCenter}
        onDragEnd={handleDragEnd}
      >
        <SortableContext
          items={value.map((e) => e.key)}
          strategy={verticalListSortingStrategy}
        >
          {value.map((entry) => (
            <ColorConfigRow
              key={entry.key}
              entry={entry}
              errors={errors?.byKey[entry.key]}
              // Ignore clears: a configured color must always have a value
              // (no "transparent" option). The picker emits undefined when the
              // active swatch is toggled off.
              onColorChange={(color) =>
                color && updateEntry(entry.key, { color })
              }
              onNameChange={(name) => updateEntry(entry.key, { name })}
              onToggleDefault={() => toggleDefault(entry.key)}
              onRemove={() => removeEntry(entry.key)}
            />
          ))}
        </SortableContext>
      </DndContext>

      <Button
        type="dashed"
        size="small"
        icon={<PlusOutlined />}
        onClick={addEntry}
        style={{ width: '100%', color: token.colorTextSecondary }}
      >
        Add color
      </Button>
    </Space>
  )
}

export default RoadmapColorConfig
