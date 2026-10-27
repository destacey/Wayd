import type { ThemeConfig } from 'antd'

/**
 * The theme Demand Management's controls take below `md`, where they are tapped rather than clicked: every
 * control is at least a 44px touch target. A segmented control's track pads its segments by 2px each side,
 * so its own height is 48.
 */
export const TOUCH_THEME: ThemeConfig = {
  token: { controlHeight: 44 },
  components: { Segmented: { controlHeight: 48 } },
}
