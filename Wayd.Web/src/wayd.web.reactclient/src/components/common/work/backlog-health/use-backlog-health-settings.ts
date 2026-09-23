'use client'

import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useEffect } from 'react'
import { useLocalStorageState } from '@/src/hooks'
import {
  BacklogHealthSettings,
  EMPTY_BACKLOG_HEALTH_SETTINGS,
  hasOverrides,
  settingsFromSearchParams,
  writeSettingsToSearchParams,
} from './backlog-health-settings'

/**
 * The report's settings: a link's parameters first, then the viewer's last
 * settings for this report, then the API's defaults. A change is saved for the
 * viewer and written to the URL, so the link always shows what the page shows.
 */
export const useBacklogHealthSettings = (
  storageKey: string,
): [BacklogHealthSettings, (settings: BacklogHealthSettings) => void] => {
  const searchParams = useSearchParams()
  const router = useRouter()
  const pathname = usePathname()

  const [stored, setStored] = useLocalStorageState<BacklogHealthSettings>(
    storageKey,
    EMPTY_BACKLOG_HEALTH_SETTINGS,
    { version: 1 },
  )

  const fromUrl = settingsFromSearchParams(searchParams)
  const settings = fromUrl ?? stored

  const urlFor = (next: BacklogHealthSettings) => {
    const query = writeSettingsToSearchParams(
      new URLSearchParams(searchParams.toString()),
      next,
    ).toString()
    return query ? `${pathname}?${query}` : pathname
  }

  // Opened from a plain link with saved settings: put them in the URL, or a
  // link copied now would show the recipient the defaults.
  const syncUrl =
    fromUrl === null && hasOverrides(stored) ? urlFor(stored) : null
  useEffect(() => {
    if (syncUrl) router.replace(syncUrl, { scroll: false })
  }, [syncUrl, router])

  const setSettings = (next: BacklogHealthSettings) => {
    setStored(next)
    router.replace(urlFor(next), { scroll: false })
  }

  return [settings, setSettings]
}
