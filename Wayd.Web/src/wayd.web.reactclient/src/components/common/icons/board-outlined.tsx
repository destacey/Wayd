import Icon from '@ant-design/icons'
import type { GetProps } from 'antd'

const BoardSvg = () => (
  <svg viewBox="0 0 1024 1024" width="1em" height="1em" fill="currentColor">
    <path d="M128 128h224a32 32 0 0 1 32 32v704a32 32 0 0 1-32 32H128a32 32 0 0 1-32-32V160a32 32 0 0 1 32-32zm40 72v592h144V200H168zM400 128h224a32 32 0 0 1 32 32v448a32 32 0 0 1-32 32H400a32 32 0 0 1-32-32V160a32 32 0 0 1 32-32zm40 72v368h144V200H440zM672 128h224a32 32 0 0 1 32 32v576a32 32 0 0 1-32 32H672a32 32 0 0 1-32-32V160a32 32 0 0 1 32-32zm40 72v496h144V200H712z" />
  </svg>
)

/**
 * A board: columns of cards. Drawn in the outlined style of Ant Design's icons, which has no board of its
 * own, and kept apart from the project icon the navigation already uses for PPM.
 */
const BoardOutlined = (props: Partial<GetProps<typeof Icon>>) => (
  <Icon component={BoardSvg} {...props} />
)

export default BoardOutlined
