import { HomeContextResponse } from '@/src/services/wayd-api'
import { ComponentType, FC } from 'react'

/** The home page column a widget sits in. */
export type WidgetColumn = 'main' | 'side'

/** What an account needs before a widget applies to it. Every listed requirement must hold. */
export interface WidgetRequirements {
  /** Permission claims the account must hold, such as `Permissions.Projects.View`. */
  permissions?: string[]
  /** A feature flag, by its kebab-case name, that must be on. */
  featureFlag?: string
  /** Whether the account must be linked to an employee — true for anything about "my" work. */
  linkedEmployee?: boolean
}

/** A count in the attention strip, contributed by the widget whose data it comes from. */
export interface AttentionTileDefinition {
  id: string
  /**
   * How urgent the count is when above zero; the strip shows the highest
   * first, ties in page order, so where a user placed a widget never decides
   * which tiles make the cut.
   */
  priority: number
  /** Renders the tile through `AttentionTile`, loading its own count. */
  Tile: FC
}

/** What a tile last reported to the strip about its count. */
export interface AttentionTileReport {
  /** The count, or undefined when it could not be loaded. */
  value: number | undefined
  loading: boolean
}

/** What the page hands every widget it renders. */
export interface HomeWidgetProps {
  /** The registry's title, so the card and the Customize drawer never disagree. */
  title: string
}

/**
 * One section of the home page. The registry is the list the page lays out,
 * and the list a user's saved layout and the Customize drawer work from, so
 * an id never changes once shipped.
 */
export interface HomeWidget {
  id: string
  title: string
  /** One sentence on what the widget shows, for the Customize drawer. */
  description: string
  defaultColumn: WidgetColumn
  /** Position within its column; lower comes first. */
  defaultOrder: number
  requires: WidgetRequirements
  /** The widget itself; may be `lazy`, for one whose code is heavy enough to load after the page. */
  Component: ComponentType<HomeWidgetProps>
  /** Tiles the widget adds to the attention strip, in the order they show. */
  tiles?: AttentionTileDefinition[]
  /**
   * How much the widget's area involves the caller, on the `Involvement` scale
   * in `home-involvement.ts`; it orders the default layout. Left out for shared
   * context that involves everyone alike.
   */
  involvement?: (context: HomeContextResponse) => number
}

/** Where a widget sits: its column and its position within it, lower first. */
export interface WidgetPlacement {
  column: WidgetColumn
  order: number
}

/**
 * Decides where a widget sits. The default reads the widget's own defaults; a
 * saved layout or involvement-based ordering supplies its own.
 */
export type PlaceWidget = (widget: HomeWidget) => WidgetPlacement

/** What the gating checks a widget's requirements against. */
export interface WidgetAccess {
  hasPermissionClaim: (claim: string) => boolean
  isFeatureEnabled: (featureName: string) => boolean
  hasLinkedEmployee: boolean
}

/** The most tiles the attention strip shows; more would stop it being a glance. */
export const MAX_ATTENTION_TILES = 4

/** The element id a widget's section carries, so a tile can point at the widget it summarises. */
export const widgetAnchorId = (widgetId: string) => `home-widget-${widgetId}`

/** Places each widget where its registry entry says. */
export const defaultPlacement: PlaceWidget = (widget) => ({
  column: widget.defaultColumn,
  order: widget.defaultOrder,
})

/** Whether every requirement a widget declares holds for this account. */
export const isWidgetAvailable = (
  widget: HomeWidget,
  access: WidgetAccess,
): boolean => {
  const { permissions = [], featureFlag, linkedEmployee } = widget.requires
  if (linkedEmployee && !access.hasLinkedEmployee) return false
  // Unknown counts as off: while the flags load, a flagged widget is not even
  // hinted at, since an organization may not want the feature seen at all.
  if (featureFlag && !access.isFeatureEnabled(featureFlag)) return false
  return permissions.every((claim) => access.hasPermissionClaim(claim))
}

/** The widgets that apply to this account, split into their columns in order. */
export const arrangeWidgets = (
  widgets: HomeWidget[],
  access: WidgetAccess,
  place: PlaceWidget = defaultPlacement,
): Record<WidgetColumn, HomeWidget[]> => {
  const placed = widgets
    .filter((w) => isWidgetAvailable(w, access))
    .map((widget) => ({ widget, ...place(widget) }))
    .sort((a, b) => a.order - b.order)
  const inColumn = (column: WidgetColumn) =>
    placed.filter((p) => p.column === column).map((p) => p.widget)
  return { main: inColumn('main'), side: inColumn('side') }
}

/**
 * Every tile the shown widgets offer, highest priority first, ties in page
 * order (main column first). All of them load their counts; which ones take a
 * slot is `visibleAttentionTiles`'s decision once the counts are known.
 */
export const attentionTiles = (
  columns: Record<WidgetColumn, HomeWidget[]>,
): AttentionTileDefinition[] =>
  [...columns.main, ...columns.side]
    .flatMap((w) => w.tiles ?? [])
    .map((tile, pageOrder) => ({ tile, pageOrder }))
    .sort(
      (a, b) => b.tile.priority - a.tile.priority || a.pageOrder - b.pageOrder,
    )
    .map(({ tile }) => tile)

/**
 * The ids of the tiles that take a slot: in the given order, leaving out each
 * one known to read zero, up to the maximum. A tile still loading, or one that
 * has not reported yet, holds its place until its count says otherwise; one
 * whose count failed keeps it, since a dash is not news that all is well.
 */
export const visibleAttentionTiles = (
  tiles: AttentionTileDefinition[],
  reports: Record<string, AttentionTileReport | undefined>,
): Set<string> => {
  const isZero = (id: string) => {
    const report = reports[id]
    return !!report && !report.loading && report.value === 0
  }
  return new Set(
    tiles
      .filter((t) => !isZero(t.id))
      .slice(0, MAX_ATTENTION_TILES)
      .map((t) => t.id),
  )
}
