'use client'

import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { useFeatureFlags, useLinkedEmployee } from '@/src/hooks'
import {
  useGetHomeLayoutQuery,
  useUpdateHomeLayoutMutation,
} from '@/src/store/features/user-management/profile-api'
import { useState } from 'react'
import {
  arrangeFromLayout,
  fromSavedWidgets,
  HomeLayout,
  layoutToSave,
  resolveLayout,
  SavedWidget,
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

/** What the Customize drawer is editing, while it is open. */
interface LayoutDraft {
  layout: HomeLayout
  /** The saved layout the draft started from, whose unavailable widgets a save keeps. */
  basis: SavedWidget[]
  changed: boolean
  /** Reset to the default with nothing changed since, so closing clears the saved layout. */
  reset: boolean
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

  const access: WidgetAccess = {
    hasPermissionClaim,
    isFeatureEnabled,
    hasLinkedEmployee,
  }
  const saved = fromSavedWidgets(data?.widgets ?? [])
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
    setDraft({ layout, basis: saved, changed: false, reset: false })

  const change = (next: HomeLayout) =>
    setDraft((d) => d && { ...d, layout: next, changed: true, reset: false })

  const reset = () =>
    setDraft({
      layout: resolveLayout(HomeWidgets, access, [], place),
      basis: [],
      changed: true,
      reset: true,
    })

  const close = async () => {
    if (!draft) return
    setDraft(null)
    if (!draft.changed) return
    const widgets = draft.reset
      ? []
      : layoutToSave(draft.layout, draft.basis, HomeWidgets)
    try {
      await updateHomeLayout({ widgets: toSavedWidgets(widgets) }).unwrap()
    } catch {
      messageApi.error(
        'Your home page layout could not be saved. Open Customize to try again.',
      )
    }
  }

  return {
    columns,
    tiles: attentionTiles(columns),
    hasLinkedEmployee,
    /** True until the saved layout and the involvement it is ordered by have loaded, so the page never reshuffles in front of the user. */
    isLayoutLoading: isLoading,
    customize: {
      canCustomize,
      isOpen: draft !== null,
      layout: offered,
      open,
      change,
      reset,
      close,
    },
  }
}
