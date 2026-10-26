'use client'

import { IntakeDeskDto } from '@/src/services/wayd-api'
import { Flex, Radio, Typography } from 'antd'
import { FC } from 'react'
import styles from './desk-choice.module.css'

const { Text } = Typography

export interface DeskChoiceProps {
  desks: IntakeDeskDto[]
  value?: string
  onChange?: (deskId: string) => void
}

const reviewedBy = (desk: IntakeDeskDto) =>
  desk.members.length > 0
    ? `Reviewed by ${desk.members.map((m) => m.employee.name).join(', ')}`
    : undefined

/**
 * The first question on the form when more than one desk takes requests. Each option is worded as the kind
 * of ask the desk takes, in the desk's own requester-facing description, with who reviews it; the group's
 * internal name is secondary.
 */
const DeskChoice: FC<DeskChoiceProps> = ({ desks, value, onChange }) => (
  <Radio.Group
    value={value}
    onChange={(e) => onChange?.(e.target.value)}
    className={styles.group}
  >
    <Flex vertical gap={8}>
      {desks.map((desk) => (
        <Radio key={desk.id} value={desk.id} className={styles.option}>
          <Flex vertical gap={2}>
            <Text>{desk.description}</Text>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {[desk.name, reviewedBy(desk)].filter(Boolean).join(' · ')}
            </Text>
          </Flex>
        </Radio>
      ))}
    </Flex>
  </Radio.Group>
)

export default DeskChoice
