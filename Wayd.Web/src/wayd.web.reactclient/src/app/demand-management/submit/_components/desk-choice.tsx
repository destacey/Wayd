'use client'

import { IntakeDeskDto } from '@/src/services/wayd-api'
import { RightOutlined } from '@ant-design/icons'
import { Avatar, Flex, Typography } from 'antd'
import { FC } from 'react'
import { deskPhrase } from '../../_components/desk-status'
import styles from './desk-choice.module.css'

const { Title, Text, Paragraph } = Typography

export interface DeskChoiceProps {
  desks: IntakeDeskDto[]
  onPick: (deskId: string) => void
}

/**
 * The first question when more than one desk takes requests. Each desk is offered in its own
 * requester-facing words, and says who reviews it, so the requester knows exactly where the request is
 * going. There is no "not sure" choice in v1: every request is on a desk from the moment it exists.
 */
const DeskChoice: FC<DeskChoiceProps> = ({ desks, onPick }) => (
  <Flex vertical gap={12} className={styles.chooser}>
    <div>
      <Title level={4} className={styles.question}>
        What kind of request is this?
      </Title>
      <Paragraph type="secondary">
        This only decides who looks at it first. Everything you write next is
        the same whichever you pick, and a reviewer can pass it on if it belongs
        somewhere else.
      </Paragraph>
    </div>
    {desks.map((desk) => (
      <button
        key={desk.id}
        type="button"
        className={styles.card}
        onClick={() => onPick(desk.id)}
      >
        <Avatar shape="square" className={styles.letter}>
          {desk.name.trim().charAt(0).toUpperCase()}
        </Avatar>
        <Flex vertical gap={4} className={styles.text}>
          <Text strong>{desk.description}</Text>
          <Text type="secondary" className={styles.reviewedBy}>
            Reviewed by {deskPhrase(desk.name)}
            {desk.members.length > 0
              ? ` · ${desk.members.map((m) => m.employee.name).join(', ')}`
              : ''}
          </Text>
        </Flex>
        <RightOutlined className={styles.chevron} aria-hidden />
      </button>
    ))}
  </Flex>
)

export default DeskChoice
