import { Flex, Typography } from 'antd'

const { Text } = Typography

export interface OverviewBlockProps {
  title: string
  /** Shown at the end of the title row, such as a link to the full section. */
  extra?: React.ReactNode
  children: React.ReactNode
}

/**
 * A titled block on the project overview.
 *
 * 14px `Text strong` — the third step of the type scale, below the section
 * heading the layout renders above it.
 */
const OverviewBlock = ({ title, extra, children }: OverviewBlockProps) => (
  <Flex vertical gap="small">
    <Flex justify="space-between" align="baseline" gap="small" wrap>
      <Text strong style={{ fontSize: 14 }}>
        {title}
      </Text>
      {extra}
    </Flex>
    {children}
  </Flex>
)

export default OverviewBlock
