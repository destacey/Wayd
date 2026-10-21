'use client'

import { useEffect, useState } from 'react'

/** How long Approve stays disabled after the page shows, and after it regains focus or visibility. */
export const APPROVE_DELAY_MS = 500

/**
 * Whether Approve may be clicked yet. Framing headers stop an overlay, but not
 * a site that opens this page and then uncovers it under the user's cursor —
 * closing its own popup on the first click of a double-click, say — so the
 * second click lands on Approve. Approve becomes clickable only once the page
 * has been visible and focused for a moment, and is disabled again whenever it
 * loses either.
 */
export const useApproveArmed = (): boolean => {
  const [armed, setArmed] = useState(false)

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined

    const arm = () => {
      timer = setTimeout(() => setArmed(true), APPROVE_DELAY_MS)
    }
    const disarm = () => {
      clearTimeout(timer)
      setArmed(false)
    }
    const rearm = () => {
      disarm()
      if (document.visibilityState === 'visible') arm()
    }

    arm()
    window.addEventListener('focus', rearm)
    window.addEventListener('blur', disarm)
    document.addEventListener('visibilitychange', rearm)
    return () => {
      clearTimeout(timer)
      window.removeEventListener('focus', rearm)
      window.removeEventListener('blur', disarm)
      document.removeEventListener('visibilitychange', rearm)
    }
  }, [])

  return armed
}
