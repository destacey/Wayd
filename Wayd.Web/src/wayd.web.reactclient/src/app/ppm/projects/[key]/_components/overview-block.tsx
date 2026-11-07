import { Flex, Typography } from 'antd'

const { Text } = Typography

export interface OverviewBlockProps {
  title: string
  children: React.ReactNode
}

/**
 * A titled block on the project overview.
 *
 * 14px `Text strong` — the third step of the type scale, below the section
 * heading the layout renders above it.
 */
const OverviewBlock = ({ title, children }: OverviewBlockProps) => (
  <Flex vertical gap="small">
    <Text strong style={{ fontSize: 14 }}>
      {title}
    </Text>
    {children}
  </Flex>
)

export default OverviewBlock
