'use client'

import { createContext, useContext } from 'react'
import { WidgetSettingValues } from './widget-settings'

/**
 * Every widget's setting values in effect, by widget id, set by the page. For
 * the few requests two widgets share, whose arguments come from both widgets'
 * settings; a widget reads its own settings from its props. Empty away from
 * the page, where every setting reads as its default.
 */
export const HomeSettingsContext = createContext<
  Record<string, WidgetSettingValues>
>({})

/** The setting values in effect for one widget, or undefined away from the page. */
export const useWidgetSettingsOf = (
  widgetId: string,
): WidgetSettingValues | undefined => useContext(HomeSettingsContext)[widgetId]
