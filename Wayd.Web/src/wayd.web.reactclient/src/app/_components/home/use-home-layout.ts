'use client'

import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { useFeatureFlags, useLinkedEmployee } from '@/src/hooks'
import {
  useGetHomeLayoutQuery,
  useUpdateHomeLayoutMutation,
} from '@/src/store/features/user-management/profile-api'
import { useRef, useState } from 'react'
import {
  arrangeFromLayout,
  fromSavedWidgets,
  HomeLayout,
  layoutToSave,
  mutedTilesToSave,
  resolveLayout,
  SavedWidget,
  settingsChangeToSave,
  setWidgetSettings,
  toSavedWidgets,
} from './home-layout'
import { involvementPlacement } from './home-involvement'
import { HomeWidgets } from './home-widgets'
import { useHomeContext } from './use-home-context'
import {
  attentionTiles,
  isWidgetAvailable,
  WidgetAccess,
} from './widget-registry'
import { resolveSettings, WidgetSettingValues } from './widget-settings'

/** What the Customize drawer is editing, while it is open. */
interface LayoutDraft {
  layout: HomeLayout
  /** The saved layout the draft started from, whose unavailable widgets a save keeps. */
  basis: SavedWidget[]
  /** Whether the widgets were changed, so closing saves their arrangement. */
  changed: boolean
  /** Reset to the default with no widget changed since, so closing clears the saved arrangement. */
  reset: boolean
  /** The tiles muted in the draft, by id. */
  mutedTiles: string[]
  /**
   * Whether the muted tiles were changed. Kept apart from `changed`, so muting
   * a tile alone saves the widgets as they were saved and never pins a
   * default arrangement that would otherwise follow the user's involvement.
   */
  mutedChanged: boolean
}

/** A whole home layout as one save replaces it. */
interface SavedHomeLayout {
  widgets: SavedWidget[]
  mutedTiles: string[]
}

/**
 * The home page's layout for the signed-in account: the widgets it shows in
 * their columns, the tiles they contribute to the attention strip, and the
 * Customize drawer's editing. While the drawer is open the page draws its
 * draft, so each change shows at once; closing it saves.
 */
export const useHomeLayout = () => {
  const { hasPermissionClaim } = useAuth()
  const { hasLinkedEmployee } = useLinkedEmployee()
  const { isEnabled: isFeatureEnabled, isLoading: isFlagsLoading } =
    useFeatureFlags()
  const messageApi = useMessage()
  const { data, isLoading: isSavedLoading, isError } = useGetHomeLayoutQuery()
  const { context, isLoading: isContextLoading } = useHomeContext({
    refresh: true,
  })
  const isLoading = isSavedLoading || isContextLoading
  const [updateHomeLayout] = useUpdateHomeLayoutMutation()
  const [draft, setDraft] = useState<LayoutDraft | null>(null)
  // The layout last sent, while any save is in flight. Each save replaces the
  // whole layout, so the next one must build on this rather than on a cache an
  // earlier save's refetch may have put back to what the server held then.
  const [sending, setSending] = useState<SavedHomeLayout | null>(null)
  const savesInFlight = useRef(0)

  const access: WidgetAccess = {
    hasPermissionClaim,
    isFeatureEnabled,
    hasLinkedEmployee,
    context,
  }
  const saved = sending?.widgets ?? fromSavedWidgets(data?.widgets ?? [])
  const savedMuted = sending?.mutedTiles ?? data?.mutedTiles ?? []
  const mutedTiles = draft?.mutedTiles ?? savedMuted
  // The default, and where a widget the saved layout lacks joins it, follow
  // what the caller is involved in; a failed context falls back to the registry's.
  const place = involvementPlacement(HomeWidgets, context)
  const layout =
    draft?.layout ?? resolveLayout(HomeWidgets, access, saved, place)
  const columns = arrangeFromLayout(HomeWidgets, access, layout)
  // A draft taken before access changed may hold a widget the user can no
  // longer use; the drawer never offers one. A save still keeps it, from the basis.
  const available = new Set(
    HomeWidgets.filter((w) => isWidgetAvailable(w, access)).map((w) => w.id),
  )
  const offered: HomeLayout = {
    main: layout.main.filter((s) => available.has(s.id)),
    side: layout.side.filter((s) => available.has(s.id)),
  }
  // Customizing waits for the flags, which decide what is offered, and for the
  // saved layout: one built from the default after a failed load would overwrite it.
  const canCustomize = !isLoading && !isError && !isFlagsLoading

  const open = () =>
    setDraft({
      layout,
      basis: saved,
      changed: false,
      reset: false,
      mutedTiles: savedMuted,
      mutedChanged: false,
    })

  const change = (next: HomeLayout) =>
    setDraft((d) => d && { ...d, layout: next, changed: true, reset: false })

  const changeMutedTiles = (next: string[]) =>
    setDraft((d) => d && { ...d, mutedTiles: next, mutedChanged: true })

  const reset = () =>
    setDraft({
      layout: resolveLayout(HomeWidgets, access, [], place),
      basis: [],
      changed: true,
      reset: true,
      mutedTiles: [],
      mutedChanged: true,
    })

  const save = async (next: SavedHomeLayout, failure: string) => {
    savesInFlight.current += 1
    setSending(next)
    try {
      await updateHomeLayout({
        widgets: toSavedWidgets(next.widgets),
        mutedTiles: next.mutedTiles,
      }).unwrap()
    } catch {
      messageApi.error(failure)
    } finally {
      savesInFlight.current -= 1
      if (savesInFlight.current === 0) setSending(null)
    }
  }

  // From a widget's card the change saves at once; in the drawer it joins the
  // draft and saves with it.
  const changeSettings = (id: string, settings: WidgetSettingValues) => {
    const next = setWidgetSettings(layout, id, settings)
    if (draft) return change(next)
    return save(
      {
        widgets: settingsChangeToSave(next, saved, HomeWidgets, id),
        mutedTiles: mutedTilesToSave(savedMuted, HomeWidgets),
      },
      'Your change to this widget could not be saved. Try again.',
    )
  }

  const slots = [...layout.main, ...layout.side]
  const storedSettings = Object.fromEntries(
    slots.map((slot) => [slot.id, slot.settings ?? {}]),
  )
  const widgetSettings = Object.fromEntries(
    HomeWidgets.map((w) => [
      w.id,
      resolveSettings(w.settings, storedSettings[w.id]),
    ]),
  )

  const close = async () => {
    if (!draft) return
    setDraft(null)
    if (!draft.changed && !draft.mutedChanged) return
    const widgets = draft.reset
      ? []
      : draft.changed
        ? layoutToSave(draft.layout, draft.basis, HomeWidgets)
        : draft.basis
    await save(
      { widgets, mutedTiles: mutedTilesToSave(draft.mutedTiles, HomeWidgets) },
      'Your home page layout could not be saved. Open Customize to try again.',
    )
  }

  return {
    columns,
    tiles: attentionTiles(columns, new Set(mutedTiles)),
    hasLinkedEmployee,
    /** True until the saved layout and the involvement it is ordered by have loaded, so the page never reshuffles in front of the user. */
    isLayoutLoading: isLoading,
    /** Each widget's setting values in effect, by widget id. */
    widgetSettings,
    /** What the user changed about each shown widget's settings, by widget id. */
    storedSettings,
    changeSettings,
    customize: {
      canCustomize,
      isOpen: draft !== null,
      layout: offered,
      /** The tiles muted, by id, in the draft while the drawer is open. */
      mutedTiles,
      open,
      change,
      changeMutedTiles,
      reset,
      close,
    },
  }
}
