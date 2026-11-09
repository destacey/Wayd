import { HomeContextResponse } from '@/src/services/wayd-api'
import {
  defaultPlacement,
  HomeWidget,
  PlaceWidget,
  WidgetPlacement,
} from './widget-registry'

/**
 * How much a widget's area involves the caller, from their home context.
 * Higher leads; a widget with no rule is shared context, ranked between
 * {@link Involvement.Involved} and {@link Involvement.None}.
 */
export const Involvement = {
  /** The caller has no records in the area: the widget would show its empty state. */
  None: 0,
  /** Shared context everyone sees, such as notifications: the rank of a widget with no rule. */
  Shared: 1,
  /** The caller has records in the area. */
  Involved: 2,
  /** The area is the caller's own day-to-day work, or work they lead. Leads the main column. */
  Leads: 3,
} as const

/** Reads the caller's involvement in a widget's area. */
export type InvolvementRule = (context: HomeContextResponse) => number

const rank = (widget: HomeWidget, context: HomeContextResponse) =>
  widget.involvement?.(context) ?? Involvement.Shared

/**
 * Places widgets by the caller's involvement. A widget the caller leads moves
 * to the main column; then each column runs from most involved to least, ties
 * in registry order, so someone with several areas gets them interleaved by
 * rank. Without a context — still loading, failed, or an unlinked account —
 * every widget sits where its registry entry says.
 */
export const involvementPlacement = (
  widgets: HomeWidget[],
  context: HomeContextResponse | undefined,
): PlaceWidget => {
  if (!context?.hasLinkedEmployee) return defaultPlacement

  const ranked = widgets
    .map((widget) => ({ widget, rank: rank(widget, context) }))
    .sort(
      (a, b) =>
        b.rank - a.rank || a.widget.defaultOrder - b.widget.defaultOrder,
    )
  const placement = new Map<string, WidgetPlacement>(
    ranked.map(({ widget, rank }, order) => [
      widget.id,
      {
        column: rank >= Involvement.Leads ? 'main' : widget.defaultColumn,
        order,
      },
    ]),
  )

  return (widget) => placement.get(widget.id) ?? defaultPlacement(widget)
}
