'use client'

import { createContext, useContext, useEffect } from 'react'
import { EmptyDisplay } from './widget-settings'

/**
 * What a widget has to show. A failed load is its own state, never empty: a
 * widget whose data could not be loaded always shows its error.
 */
export type WidgetState = 'loading' | 'empty' | 'failed' | 'rows'

/** How a widget's card is drawn: in full, as one line beside its title, or not at all. */
export type WidgetForm = 'full' | 'line' | 'hidden'

/** What the page tells a widget about how to draw itself, and where it reports its state. */
export interface WidgetDisplay {
  widgetId: string
  /** The widget's empty setting in effect. */
  emptyDisplay: EmptyDisplay
  /** The user chose to see the widgets hidden as empty, until they leave the page. */
  revealed: boolean
  /**
   * Takes a widget's state; undefined when it unmounts, so a widget switched
   * off and on again is not drawn from what it last said. Must keep its
   * identity across renders: the report re-runs whenever it changes.
   */
  onReport: (report: WidgetStateReport) => void
}

/** A widget's state as reported to the page. */
export interface WidgetStateReport {
  widgetId: string
  state: WidgetState | undefined
}

/** The page's record of each widget's last reported state, by widget id. */
export type WidgetStates = Record<string, WidgetState | undefined>

/** Records a report; one identical to the last leaves the record as it was. */
export const recordWidgetState = (
  states: WidgetStates,
  { widgetId, state }: WidgetStateReport,
): WidgetStates =>
  states[widgetId] === state ? states : { ...states, [widgetId]: state }

/** Set by the page around each widget; null away from it, where a widget always draws in full. */
export const WidgetDisplayContext = createContext<WidgetDisplay | null>(null)

/**
 * How a widget in `state` is drawn under its empty setting. Only an empty
 * widget changes form: a loading one keeps its skeleton, so a hidden widget
 * leaves only once its data says it has nothing.
 */
export const widgetForm = (
  state: WidgetState | undefined,
  emptyDisplay: EmptyDisplay,
  revealed: boolean,
): WidgetForm => {
  if (state !== 'empty') return 'full'
  if (emptyDisplay === 'line') return 'line'
  if (emptyDisplay === 'hide' && !revealed) return 'hidden'
  return 'full'
}

/**
 * Reports a widget's state to the page and returns the form to draw it in.
 * Away from the page the widget draws in full.
 */
export const useWidgetState = (state: WidgetState): WidgetForm => {
  const display = useContext(WidgetDisplayContext)
  const onReport = display?.onReport
  const widgetId = display?.widgetId

  useEffect(() => {
    if (!onReport || widgetId === undefined) return
    onReport({ widgetId, state })
    return () => onReport({ widgetId, state: undefined })
  }, [onReport, widgetId, state])

  return display
    ? widgetForm(state, display.emptyDisplay, display.revealed)
    : 'full'
}
