'use client'

import useAuth from '@/src/components/contexts/auth'
import { formatCalendarDate, todayCalendarDate } from '@/src/utils'
import { Typography } from 'antd'
import { FC } from 'react'
import styles from './home.module.css'

const { Title, Text } = Typography

/** The greeting for an hour of the day, 0–23, in the viewer's time. */
export const greetingFor = (hour: number): string =>
  hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening'

/** The first word of a display name, or nothing for a blank one. */
export const firstName = (name: string | undefined): string =>
  name?.trim().split(/\s+/)[0] ?? ''

/** "Good morning, Alex" with today's date beneath it. */
const HomeGreeting: FC = () => {
  const { user } = useAuth()
  const greeting = greetingFor(new Date().getHours())
  const name = firstName(user?.name)

  return (
    <div className={styles.greeting}>
      <Title level={3} className={styles.greetingTitle}>
        {name ? `${greeting}, ${name}` : greeting}
      </Title>
      <Text type="secondary">
        {formatCalendarDate(todayCalendarDate(), 'dddd, MMMM D, YYYY')}
      </Text>
    </div>
  )
}

export default HomeGreeting
