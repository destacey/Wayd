'use client'

import { Button, Drawer, Flex, Segmented, Select, Tooltip, Typography } from 'antd'
import { CheckOutlined } from '@ant-design/icons'
import useTheme from '@/src/components/contexts/theme'
import { ThemeName } from '@/src/components/contexts/theme/types'
import { ThemeConstants } from '@/src/config/theme/theme-constants'

const { Text } = Typography

const PRESET_COLORS: { label: string; value: string }[] = [
  { label: 'Blue', value: ThemeConstants.COLOR_PRIMARY },
  { label: 'Geekblue', value: '#2f54eb' },
  { label: 'Purple', value: '#9254de' },
  { label: 'Magenta', value: '#eb2f96' },
  { label: 'Red', value: '#f5222d' },
  { label: 'Volcano', value: '#fa541c' },
  { label: 'Orange', value: '#fa8c16' },
  { label: 'Gold', value: '#faad14' },
  { label: 'Lime', value: '#a0d911' },
  { label: 'Green', value: '#52c41a' },
  { label: 'Cyan', value: '#13c2c2' },
]

const THEME_OPTIONS: { label: string; value: ThemeName }[] = [
  { label: 'Light', value: 'light' },
  { label: 'Shadcn', value: 'shadcn' },
  { label: 'Cartoon', value: 'cartoon' },
  { label: 'Slate', value: 'slate' },
  { label: 'Dark', value: 'dark' },
]

interface ThemeManagerDrawerProps {
  open: boolean
  onClose: () => void
}

const ThemeManagerDrawer = ({ open, onClose }: ThemeManagerDrawerProps) => {
  const {
    currentThemeName,
    setCurrentThemeName,
    userThemeConfig,
    setUserThemeConfig,
  } = useTheme()

  const colorPrimary =
    userThemeConfig?.colorPrimary ?? ThemeConstants.COLOR_PRIMARY
  const density: 'default' | 'compact' = userThemeConfig?.useCompactAlgorithm
    ? 'compact'
    : 'default'

  const handleReset = () => setUserThemeConfig(null)

  return (
    <Drawer
      title="Theme"
      placement="right"
      size={320}
      mask={false}
      open={open}
      onClose={onClose}
    >
      <Flex vertical gap="large">
        <Flex vertical gap="small">
          <Text strong>Mode</Text>
          <Select
            value={currentThemeName}
            options={THEME_OPTIONS}
            onChange={(v) => setCurrentThemeName(v as ThemeName)}
            popupMatchSelectWidth
          />
        </Flex>

        <Flex vertical gap="small">
          <Text strong>Primary Color</Text>
          <Flex wrap gap="small">
            {PRESET_COLORS.map(({ label, value }) => (
              <Tooltip key={value} title={label}>
                <button
                  aria-label={`${label}${colorPrimary === value ? ' (selected)' : ''}`}
                  aria-pressed={colorPrimary === value}
                  onClick={() =>
                    setUserThemeConfig({
                      colorPrimary: value,
                      useCompactAlgorithm:
                        userThemeConfig?.useCompactAlgorithm ?? false,
                    })
                  }
                  style={{
                    width: 28,
                    height: 28,
                    borderRadius: 6,
                    backgroundColor: value,
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    boxShadow:
                      colorPrimary === value
                        ? `0 0 0 2px #fff, 0 0 0 4px ${value}`
                        : undefined,
                    border: 'none',
                    padding: 0,
                  }}
                >
                  {colorPrimary === value && (
                    <CheckOutlined style={{ color: '#fff', fontSize: 12 }} />
                  )}
                </button>
              </Tooltip>
            ))}
          </Flex>
        </Flex>

        <Flex vertical gap="small">
          <Text strong>Density</Text>
          <Segmented<'default' | 'compact'>
            block
            value={density}
            options={[
              { label: 'Default', value: 'default' },
              { label: 'Compact', value: 'compact' },
            ]}
            onChange={(v) =>
              setUserThemeConfig({
                colorPrimary: userThemeConfig?.colorPrimary,
                useCompactAlgorithm: v === 'compact',
              })
            }
          />
        </Flex>

        <Flex vertical gap="small">
          <Button block onClick={handleReset}>
            Reset to Defaults
          </Button>
          <Text type="secondary" style={{ fontSize: 12 }}>
            Changes are saved automatically.
          </Text>
        </Flex>
      </Flex>
    </Drawer>
  )
}

export default ThemeManagerDrawer
