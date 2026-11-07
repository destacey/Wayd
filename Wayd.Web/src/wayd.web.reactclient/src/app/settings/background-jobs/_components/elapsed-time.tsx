'use client'

import { FC, useEffect, useState } from 'react'
import { formatDuration } from '@/src/utils/duration'
import styles from './job-health-summary.module.css'

export interface ElapsedTimeProps {
  start: Date | string
}

/**
 * Time since `start`, ticking each second. Each cell owns its timer, so the
 * tick re-renders only this text rather than the grid around it.
 */
const ElapsedTime: FC<ElapsedTimeProps> = ({ start }) => {
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 1000)
    return () => clearInterval(timer)
  }, [])

  return <span className={styles.elapsed}>{formatDuration(start, now)}</span>
}

export default ElapsedTime
