'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'

/**
 * The My Projects dashboard became the Me scope of the Projects Dashboard.
 * Old links and installed PWAs still land here, so the route stays and
 * forwards.
 */
const MyProjectsRedirectPage = () => {
  const router = useRouter()

  useEffect(() => {
    router.replace('/ppm/dashboards/projects')
  }, [router])

  return null
}

export default MyProjectsRedirectPage
