'use client'

import {
  DownOutlined,
  HolderOutlined,
  MoreOutlined,
  UpOutlined,
} from '@ant-design/icons'
import {
  Announcements,
  closestCorners,
  DndContext,
  DragEndEvent,
  DragOverEvent,
  DragOverlay,
  DragStartEvent,
  KeyboardSensor,
  PointerSensor,
  UniqueIdentifier,
  useDroppable,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import {
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { Button, Drawer, Dropdown, Grid, Switch, Tag, Typography } from 'antd'
import type { MenuProps } from 'antd'
import { FC, useState } from 'react'
import {
  findSlot,
  HomeLayout,
  LayoutSlot,
  moveWidget,
  setTileMuted,
  setWidgetSettings,
  setWidgetVisible,
  tileChoices,
  WIDGET_COLUMNS,
} from './home-layout'
import styles from './home.module.css'
import { HomeWidget, WidgetColumn } from './widget-registry'
import { hasCustomSettings, WidgetSettingValues } from './widget-settings'
import WidgetSettingsForm from './widget-settings-form'

const { Text } = Typography

const COLUMN_LABELS: Record<WidgetColumn, { title: string; hint: string }> = {
  main: { title: 'Main', hint: 'The wide column.' },
  side: {
    title: 'Side',
    hint: 'Beside main on a wide screen, below it on a phone.',
  },
}

const otherColumn = (column: WidgetColumn): WidgetColumn =>
  column === 'main' ? 'side' : 'main'

/** The drop target id of a column, kept apart from widget ids. */
const columnDropId = (column: WidgetColumn) => `column:${column}`

type MoveAction = 'up' | 'down' | 'other-column'

interface WidgetRowProps {
  widget: HomeWidget
  slot: LayoutSlot
  column: WidgetColumn
  isFirst: boolean
  isLast: boolean
  onToggle: (visible: boolean) => void
  onMove: (action: MoveAction) => void
  onSettingsChange: (settings: WidgetSettingValues) => void
}

const WidgetRow: FC<WidgetRowProps> = ({
  widget,
  slot,
  column,
  isFirst,
  isLast,
  onToggle,
  onMove,
  onSettingsChange,
}) => {
  const [settingsOpen, setSettingsOpen] = useState(false)
  const hasSettings = (widget.settings?.length ?? 0) > 0
  const settingsId = `customize-home-settings-${widget.id}`
  const {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: widget.id })

  const moveItems: MenuProps['items'] = [
    { key: 'up', label: 'Move up', disabled: isFirst },
    { key: 'down', label: 'Move down', disabled: isLast },
    {
      key: 'other-column',
      label: `Move to ${COLUMN_LABELS[otherColumn(column)].title.toLowerCase()} column`,
    },
  ]

  return (
    <li
      ref={setNodeRef}
      // dnd-kit positions the row while it moves; nothing else is inline.
      style={{ transform: CSS.Translate.toString(transform), transition }}
      // While dragged, the overlay carries the row and this one marks where it will land.
      className={`${styles.customizeItem} ${isDragging ? styles.customizeRowPlaceholder : ''}`}
    >
      <div className={styles.customizeRow}>
        <Button
          ref={setActivatorNodeRef}
          type="text"
          size="small"
          icon={<HolderOutlined />}
          className={styles.customizeHandle}
          {...attributes}
          {...listeners}
          aria-label={`Drag ${widget.title}`}
        />
        <div className={styles.customizeRowText}>
          <span className={styles.customizeRowTitle}>
            <Text type={slot.visible ? undefined : 'secondary'}>
              {widget.title}
            </Text>
            {hasCustomSettings(widget.settings, slot.settings) && (
              <Tag color="processing">Customized</Tag>
            )}
          </span>
          <Text type="secondary" className={styles.customizeRowDescription}>
            {widget.description}
          </Text>
        </div>
        {hasSettings && (
          <Button
            type="text"
            size="small"
            icon={settingsOpen ? <UpOutlined /> : <DownOutlined />}
            onClick={() => setSettingsOpen(!settingsOpen)}
            aria-expanded={settingsOpen}
            aria-controls={settingsId}
            aria-label={`${widget.title} settings`}
          />
        )}
        <Dropdown
          trigger={['click']}
          menu={{
            items: moveItems,
            onClick: ({ key }) => onMove(key as MoveAction),
          }}
        >
          <Button
            type="text"
            size="small"
            icon={<MoreOutlined />}
            aria-label={`Move ${widget.title}`}
          />
        </Dropdown>
        <Switch
          size="small"
          checked={slot.visible}
          onChange={onToggle}
          aria-label={`Show ${widget.title}`}
        />
      </div>
      {hasSettings && settingsOpen && (
        <div id={settingsId} className={styles.customizeRowSettings}>
          <WidgetSettingsForm
            widget={widget}
            stored={slot.settings ?? {}}
            onChange={onSettingsChange}
          />
        </div>
      )}
    </li>
  )
}

/** The row that follows the pointer while a widget is dragged. */
const WidgetRowPreview: FC<{ widget: HomeWidget; slot: LayoutSlot }> = ({
  widget,
  slot,
}) => (
  <div className={`${styles.customizeRow} ${styles.customizeRowDragging}`}>
    <HolderOutlined className={styles.customizeHandle} />
    <div className={styles.customizeRowText}>
      <Text type={slot.visible ? undefined : 'secondary'}>{widget.title}</Text>
      <Text type="secondary" className={styles.customizeRowDescription}>
        {widget.description}
      </Text>
    </div>
  </div>
)

interface ColumnListProps {
  column: WidgetColumn
  slots: LayoutSlot[]
  widgetsById: Map<string, HomeWidget>
  onToggle: (id: string, visible: boolean) => void
  onMove: (id: string, action: MoveAction) => void
  onSettingsChange: (id: string, settings: WidgetSettingValues) => void
}

const ColumnList: FC<ColumnListProps> = ({
  column,
  slots,
  widgetsById,
  onToggle,
  onMove,
  onSettingsChange,
}) => {
  // The list is a drop target too, so a widget can be dropped into an empty column.
  const { setNodeRef } = useDroppable({ id: columnDropId(column) })
  const headingId = `customize-home-${column}`
  const { title, hint } = COLUMN_LABELS[column]

  return (
    <section className={styles.customizeColumn} aria-labelledby={headingId}>
      <div className={styles.customizeColumnHeading}>
        <Text strong id={headingId}>
          {title}
        </Text>
        <Text type="secondary" className={styles.customizeRowDescription}>
          {hint}
        </Text>
      </div>
      <SortableContext
        items={slots.map((s) => s.id)}
        strategy={verticalListSortingStrategy}
      >
        <ol ref={setNodeRef} className={styles.customizeList}>
          {slots.length === 0 && (
            <li className={styles.customizeEmpty}>
              <Text type="secondary">
                Drag a widget here, or move one from its ⋯ menu.
              </Text>
            </li>
          )}
          {slots.map((slot, index) => {
            const widget = widgetsById.get(slot.id)
            if (!widget) return null
            return (
              <WidgetRow
                key={slot.id}
                widget={widget}
                slot={slot}
                column={column}
                isFirst={index === 0}
                isLast={index === slots.length - 1}
                onToggle={(visible) => onToggle(slot.id, visible)}
                onMove={(action) => onMove(slot.id, action)}
                onSettingsChange={(settings) =>
                  onSettingsChange(slot.id, settings)
                }
              />
            )
          })}
        </ol>
      </SortableContext>
    </section>
  )
}

interface AttentionTileListProps {
  layout: HomeLayout
  widgets: HomeWidget[]
  mutedTiles: string[]
  onChange: (mutedTiles: string[]) => void
}

/**
 * The tiles the user's widgets offer the attention strip, in the order they
 * take slots, each with a switch. A tile whose widget is switched off cannot
 * show, so its switch is disabled, but it keeps whether it was muted.
 */
const AttentionTileList: FC<AttentionTileListProps> = ({
  layout,
  widgets,
  mutedTiles,
  onChange,
}) => {
  const choices = tileChoices(layout, widgets)
  if (choices.length === 0) return null
  const muted = new Set(mutedTiles)
  const headingId = 'customize-home-attention'

  return (
    <section className={styles.customizeColumn} aria-labelledby={headingId}>
      <div className={styles.customizeColumnHeading}>
        <Text strong id={headingId}>
          Choose counts to show
        </Text>
        <Text type="secondary" className={styles.customizeRowDescription}>
          The strip shows the four most urgent counts above zero. Switch off a
          tile you never act on and its slot goes to the next.
        </Text>
      </div>
      <ul className={styles.customizeList}>
        {choices.map(({ tile, widgetTitle, widgetVisible }) => (
          <li key={tile.id} className={styles.customizeItem}>
            <div className={styles.customizeRow}>
              <div className={styles.customizeRowText}>
                <Text type={widgetVisible ? undefined : 'secondary'}>
                  {tile.title}
                </Text>
                <Text
                  type="secondary"
                  id={`customize-home-tile-${tile.id}`}
                  className={styles.customizeRowDescription}
                >
                  {widgetVisible
                    ? `From ${widgetTitle}.`
                    : `Unavailable while ${widgetTitle} is switched off.`}
                </Text>
              </div>
              <Switch
                size="small"
                checked={!muted.has(tile.id)}
                disabled={!widgetVisible}
                onChange={(on) =>
                  onChange(setTileMuted(mutedTiles, tile.id, !on))
                }
                aria-label={`Show the ${tile.title} tile`}
                aria-describedby={`customize-home-tile-${tile.id}`}
              />
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}

export interface CustomizeHomeDrawerProps {
  open: boolean
  /** The layout being edited: the available widgets only, so nothing the user cannot use is offered. */
  layout: HomeLayout
  /** The widget registry, to name each widget in the layout. */
  widgets: HomeWidget[]
  /** The attention tiles muted, by id. */
  mutedTiles: string[]
  onChange: (layout: HomeLayout) => void
  onMutedTilesChange: (mutedTiles: string[]) => void
  onReset: () => void
  /** Closing saves, so Done and the close button both end here. */
  onClose: () => void
}

/**
 * Where a user arranges their home page: each widget listed under its column,
 * dragged within and between columns (or moved from its menu, by keyboard or
 * on a phone), switched on or off, and opened to change what it shows. The
 * page behind it follows each change.
 */
const CustomizeHomeDrawer: FC<CustomizeHomeDrawerProps> = ({
  open,
  layout,
  widgets,
  mutedTiles,
  onChange,
  onMutedTilesChange,
  onReset,
  onClose,
}) => {
  const screens = Grid.useBreakpoint()
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  )
  const widgetsById = new Map(widgets.map((w) => [w.id, w]))

  const columnOf = (id: UniqueIdentifier) =>
    WIDGET_COLUMNS.find((c) => columnDropId(c) === id)

  /** Where a dragged widget lands over a target: a widget's place, or the end of a column. */
  const dropTarget = (overId: UniqueIdentifier) => {
    const column = columnOf(overId)
    if (column) return { column, index: layout[column].length }
    return findSlot(layout, String(overId))
  }

  const describe = (id: UniqueIdentifier) => {
    const column = columnOf(id)
    if (column) return `the ${COLUMN_LABELS[column].title} column`
    return widgetsById.get(String(id))?.title ?? String(id)
  }

  const announcements: Announcements = {
    onDragStart: ({ active }) => `Picked up ${describe(active.id)}.`,
    onDragOver: ({ active, over }) =>
      over
        ? `${describe(active.id)} is over ${describe(over.id)}.`
        : `${describe(active.id)} is no longer over a column.`,
    onDragEnd: ({ active, over }) =>
      over
        ? `${describe(active.id)} was dropped on ${describe(over.id)}.`
        : `${describe(active.id)} was dropped.`,
    onDragCancel: ({ active }) =>
      `Moving ${describe(active.id)} was cancelled.`,
  }

  // The layout as the drag found it, to put back if the drag is cancelled.
  const [dragStart, setDragStart] = useState<{
    id: string
    layout: HomeLayout
  } | null>(null)
  const dragged = dragStart ? findSlot(layout, dragStart.id) : undefined
  const draggedWidget = dragStart ? widgetsById.get(dragStart.id) : undefined

  const handleDragStart = ({ active }: DragStartEvent) =>
    setDragStart({ id: String(active.id), layout })

  // Crossing into the other column moves the widget at once, so the list it
  // lands in makes room for it while the drag goes on.
  const handleDragOver = ({ active, over }: DragOverEvent) => {
    if (!over) return
    const from = findSlot(layout, String(active.id))
    const to = dropTarget(over.id)
    if (!from || !to || from.column === to.column) return
    onChange(moveWidget(layout, String(active.id), to.column, to.index))
  }

  const handleDragEnd = ({ active, over }: DragEndEvent) => {
    setDragStart(null)
    if (!over || active.id === over.id) return
    const from = findSlot(layout, String(active.id))
    const to = dropTarget(over.id)
    if (!from || !to) return
    // Over its own column's gaps rather than a row: the drop stays where the
    // drag left it instead of jumping to the end.
    if (from.column === to.column && columnOf(over.id)) return
    onChange(moveWidget(layout, String(active.id), to.column, to.index))
  }

  // A cross-column move was applied while dragging, so cancelling undoes it.
  const handleDragCancel = () => {
    if (dragStart) onChange(dragStart.layout)
    setDragStart(null)
  }

  const handleMove = (id: string, action: MoveAction) => {
    const from = findSlot(layout, id)
    if (!from) return
    if (action === 'other-column') {
      const to = otherColumn(from.column)
      onChange(moveWidget(layout, id, to, layout[to].length))
    } else {
      const index = action === 'up' ? from.index - 1 : from.index + 1
      onChange(moveWidget(layout, id, from.column, index))
    }
  }

  return (
    <Drawer
      title="Customize home"
      open={open}
      onClose={onClose}
      size={screens.sm ? 440 : '100%'}
      destroyOnHidden
      // Escape cancels a keyboard drag; it must not also close (and save) the drawer.
      keyboard={dragStart === null}
      footer={
        <div className={styles.customizeFooter}>
          <Button onClick={onReset}>Reset to default</Button>
          <Button type="primary" onClick={onClose}>
            Done
          </Button>
        </div>
      }
    >
      <Text type="secondary" className={styles.customizeIntro}>
        Drag widgets to reorder them or move them between columns, switch off
        the ones you do not need, and open a widget to change what it shows. The
        attention strip stays at the top and shows the tiles of the widgets you
        keep, less any you switch off under Choose counts to show. Your layout
        saves when you close this panel. Reset to default also clears each
        widget&apos;s settings and turns every tile back on.
      </Text>
      <DndContext
        sensors={sensors}
        collisionDetection={closestCorners}
        onDragStart={handleDragStart}
        onDragOver={handleDragOver}
        onDragEnd={handleDragEnd}
        onDragCancel={handleDragCancel}
        accessibility={{ announcements }}
      >
        {WIDGET_COLUMNS.map((column) => (
          <ColumnList
            key={column}
            column={column}
            slots={layout[column]}
            widgetsById={widgetsById}
            onToggle={(id, visible) =>
              onChange(setWidgetVisible(layout, id, visible))
            }
            onMove={handleMove}
            onSettingsChange={(id, settings) =>
              onChange(setWidgetSettings(layout, id, settings))
            }
          />
        ))}
        {/* Carries the row across columns, where the list item itself remounts. */}
        <DragOverlay>
          {draggedWidget && dragged ? (
            <WidgetRowPreview
              widget={draggedWidget}
              slot={layout[dragged.column][dragged.index]}
            />
          ) : null}
        </DragOverlay>
      </DndContext>
      <AttentionTileList
        layout={layout}
        widgets={widgets}
        mutedTiles={mutedTiles}
        onChange={onMutedTilesChange}
      />
    </Drawer>
  )
}

export default CustomizeHomeDrawer
