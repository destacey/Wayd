'use client'

import { Empty } from 'antd'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useDocumentTitle } from '@/src/hooks'
import useSettingsMenuItems, {
  firstSettingsRoute,
} from './_components/use-settings-menu-items'

/**
 * `/settings` has no content of its own — the rail is the area's index.
 *
 * It sends you to the first page you can actually open rather than a fixed
 * route, because the menu is permission-filtered and feature-flagged: an
 * administrator lands on Users, someone who can only see work configuration
 * lands on Work Types, and neither is sent somewhere they would be bounced
 * from.
 *
 * Deliberately not wrapped in `authorizePage`: there is no single permission
 * that means "can use settings", and gating on one would lock out a viewer
 * who holds a different one. The destination pages authorize themselves.
 */
const SettingsPage = () => {
  useDocumentTitle('Settings')
  const router = useRouter()
  const { menuItems } = useSettingsMenuItems()

  const destination = firstSettingsRoute(menuItems)

  useEffect(() => {
    if (destination) {
      router.replace(destination)
    }
  }, [destination, router])

  // Only reachable by a viewer whose permissions leave the whole rail empty —
  // rare, but a blank page would read as a failure rather than an answer.
  if (!destination) {
    return <Empty description="You do not have access to any settings." />
  }

  return null
}

export default SettingsPage
