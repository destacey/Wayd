'use client'

import { Flex, Typography } from 'antd'
import { ReactNode } from 'react'

const { Paragraph, Title } = Typography

export interface FreeTagsHeadingProps {
  /** One or two sentences on what free tags are in this area. */
  description: ReactNode
  /** Controls set beside the heading, such as a link to the setting's history. */
  extra?: ReactNode
}

/**
 * The heading of a tag settings page's free tags section, so every area's
 * section below its categories grid reads the same.
 */
const FreeTagsHeading = ({ description, extra }: FreeTagsHeadingProps) => (
  <>
    <Flex
      justify="space-between"
      align="center"
      gap="small"
      style={{ marginTop: 'var(--ant-margin-lg)' }}
    >
      <Title level={4} style={{ marginBlock: 0 }}>
        Free Tags
      </Title>
      {extra}
    </Flex>
    <Paragraph type="secondary" style={{ marginTop: 'var(--ant-margin-xs)' }}>
      {description}
    </Paragraph>
  </>
)

export default FreeTagsHeading
