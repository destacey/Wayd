import {
  HomeLayoutColumn,
  HomeLayoutWidgetRequest,
  HomeLayoutWidgetResponse,
} from '@/src/services/wayd-api'
import {
  arrangeWidgets,
  defaultPlacement,
  HomeWidget,
  isWidgetAvailable,
  PlaceWidget,
  WidgetAccess,
  WidgetColumn,
  WidgetPlacement,
} from './widget-registry'

/** The page's columns, in reading order: main first, as a phone stacks them. */
export const WIDGET_COLUMNS: WidgetColumn[] = ['main', 'side']

/** One widget's place in the user's saved layout, as the profile stores it. */
export interface SavedWidget {
  widgetId: string
  column: WidgetColumn
  visible: boolean
}

/** A widget in an arranged column. */
export interface LayoutSlot {
  id: string
  visible: boolean
}

/**
 * The available widgets in each column, in order, shown or not: what the page
 * draws and what the Customize drawer edits.
 */
export type HomeLayout = Record<WidgetColumn, LayoutSlot[]>

const toColumn = (column: HomeLayoutColumn): WidgetColumn =>
  column === HomeLayoutColumn.Side ? 'side' : 'main'

const fromColumn = (column: WidgetColumn): HomeLayoutColumn =>
  column === 'side' ? HomeLayoutColumn.Side : HomeLayoutColumn.Main

/** The saved layout as the API returned it. */
export const fromSavedWidgets = (
  widgets: HomeLayoutWidgetResponse[],
): SavedWidget[] =>
  widgets.map((w) => ({
    widgetId: w.widgetId,
    column: toColumn(w.column),
    visible: w.visible,
  }))

/** A layout to save, as the API takes it. */
export const toSavedWidgets = (
  widgets: SavedWidget[],
): HomeLayoutWidgetRequest[] =>
  widgets.map((w) => ({
    widgetId: w.widgetId,
    column: fromColumn(w.column),
    visible: w.visible,
  }))

/**
 * Merges the saved layout with the registry for this account. Saved widgets
 * keep their column, order and visibility; a widget the account can use but
 * the layout does not mention (new, or newly available) joins the column
 * `place` gives it, ahead of the first widget `place` orders after it. Saved
 * ids the account cannot use, or the registry no longer has, are left out.
 * With nothing saved, this is the default layout `place` describes.
 */
export const resolveLayout = (
  widgets: HomeWidget[],
  access: WidgetAccess,
  saved: SavedWidget[],
  place: PlaceWidget = defaultPlacement,
): HomeLayout => {
  const available = widgets.filter((w) => isWidgetAvailable(w, access))
  const placement = new Map(available.map((w) => [w.id, place(w)]))
  const layout: HomeLayout = { main: [], side: [] }
  const placed = new Set<string>()

  for (const entry of saved) {
    if (!placement.has(entry.widgetId) || placed.has(entry.widgetId)) continue
    layout[entry.column].push({ id: entry.widgetId, visible: entry.visible })
    placed.add(entry.widgetId)
  }

  const orderOf = (id: string) => placement.get(id)!.order
  const missing = available
    .filter((w) => !placed.has(w.id))
    .sort((a, b) => orderOf(a.id) - orderOf(b.id))
  for (const widget of missing) {
    const { column, order } = placement.get(widget.id)!
    const slots = layout[column]
    const before = slots.findIndex((s) => orderOf(s.id) > order)
    slots.splice(before === -1 ? slots.length : before, 0, {
      id: widget.id,
      visible: true,
    })
  }

  return layout
}

/** The widgets a layout shows, in their columns, through the registry's own arranging. */
export const arrangeFromLayout = (
  widgets: HomeWidget[],
  access: WidgetAccess,
  layout: HomeLayout,
): Record<WidgetColumn, HomeWidget[]> => {
  const placement = new Map<string, WidgetPlacement>()
  for (const column of WIDGET_COLUMNS) {
    layout[column].forEach((slot, order) => {
      if (slot.visible) placement.set(slot.id, { column, order })
    })
  }
  return arrangeWidgets(
    widgets.filter((w) => placement.has(w.id)),
    access,
    (w) => placement.get(w.id)!,
  )
}

/**
 * The layout to save: every widget the user arranged, main column first, plus
 * the saved widgets they cannot use right now, so those return where they were
 * when access does. Each kept widget follows the widget it followed before.
 * Ids the registry no longer has are dropped.
 */
export const layoutToSave = (
  layout: HomeLayout,
  saved: SavedWidget[],
  widgets: HomeWidget[],
): SavedWidget[] => {
  const known = new Set(widgets.map((w) => w.id))
  const arranged = WIDGET_COLUMNS.flatMap((column) =>
    layout[column].map((slot) => ({
      widgetId: slot.id,
      column,
      visible: slot.visible,
    })),
  )
  const arrangedIds = new Set(arranged.map((w) => w.widgetId))

  // Kept widgets grouped by the arranged widget they came after; '' is the start.
  const keptAfter = new Map<string, SavedWidget[]>()
  const kept = new Set<string>()
  let previous = ''
  for (const entry of saved) {
    if (arrangedIds.has(entry.widgetId)) {
      previous = entry.widgetId
    } else if (known.has(entry.widgetId) && !kept.has(entry.widgetId)) {
      kept.add(entry.widgetId)
      keptAfter.set(previous, [...(keptAfter.get(previous) ?? []), entry])
    }
  }

  return [
    ...(keptAfter.get('') ?? []),
    ...arranged.flatMap((entry) => [
      entry,
      ...(keptAfter.get(entry.widgetId) ?? []),
    ]),
  ]
}

/** Where a widget sits in a layout, if it is there. */
export const findSlot = (
  layout: HomeLayout,
  id: string,
): { column: WidgetColumn; index: number } | undefined => {
  for (const column of WIDGET_COLUMNS) {
    const index = layout[column].findIndex((s) => s.id === id)
    if (index !== -1) return { column, index }
  }
  return undefined
}

/** Moves a widget to a position in a column, the same column or the other. */
export const moveWidget = (
  layout: HomeLayout,
  id: string,
  column: WidgetColumn,
  index: number,
): HomeLayout => {
  const from = findSlot(layout, id)
  if (!from) return layout
  const slot = layout[from.column][from.index]
  const next: HomeLayout = {
    main: layout.main.filter((s) => s.id !== id),
    side: layout.side.filter((s) => s.id !== id),
  }
  const at = Math.max(0, Math.min(index, next[column].length))
  next[column] = [...next[column].slice(0, at), slot, ...next[column].slice(at)]
  return next
}

/** Shows or hides a widget, leaving it where it is. */
export const setWidgetVisible = (
  layout: HomeLayout,
  id: string,
  visible: boolean,
): HomeLayout => ({
  main: layout.main.map((s) => (s.id === id ? { ...s, visible } : s)),
  side: layout.side.map((s) => (s.id === id ? { ...s, visible } : s)),
})
