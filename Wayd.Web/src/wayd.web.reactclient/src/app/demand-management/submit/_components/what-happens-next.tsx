'use client'

import { Card, Flex, Timeline, Typography } from 'antd'
import { FC } from 'react'

const { Text, Paragraph } = Typography

const JOURNEY = [
  {
    name: 'Submitted',
    note: 'Today. The desk you picked sees it straight away.',
    color: 'gray',
  },
  {
    name: 'In Review',
    note: 'A reviewer owns it and may ask you questions.',
    color: 'blue',
  },
  {
    name: 'Accepted or declined',
    note: 'A decision with a reason either way.',
    color: 'green',
  },
  {
    name: 'Fulfilled',
    note: 'When everything it became is delivered.',
    color: 'green',
  },
]

/**
 * Beside the form: where a request goes after it is submitted, and why the form asks what makes the date.
 * The stages are the usual path, described rather than read from the desk's workflow, since the requester
 * needs the shape of it, not one desk's status names.
 */
const WhatHappensNext: FC = () => (
  <Flex vertical gap={16}>
    <Card size="small" title="What happens next">
      <Timeline
        items={JOURNEY.map((j) => ({
          color: j.color,
          content: (
            <Flex vertical>
              <Text strong>{j.name}</Text>
              <Text type="secondary">{j.note}</Text>
            </Flex>
          ),
        }))}
      />
    </Card>
    <Card size="small" title="Why we ask about the date">
      <Paragraph type="secondary" style={{ marginBottom: 0 }}>
        Two requests can both say 31 March. One is a contract term that will not
        move; the other is a date someone picked in a meeting. Reviewers need to
        tell them apart before planning a quarter around either.
      </Paragraph>
    </Card>
  </Flex>
)

export default WhatHappensNext
