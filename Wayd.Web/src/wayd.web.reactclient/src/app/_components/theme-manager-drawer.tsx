'use client'

import { useMemo } from 'react'
import { Button, Drawer, Flex, Segmented, Select, Tooltip, Typography } from 'antd'
import { CheckOutlined } from '@ant-design/icons'
import useTheme from '@/src/components/contexts/theme'
import { ThemeId, ThemeMode } from '@/src/components/contexts/theme/types'
import { ThemeConstants } from '@/src/config/theme/theme-constants'
import { THEME_METADATA } from '@/src/config/theme/theme-registry'

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

const THEME_OPTIONS: { label: string; value: ThemeId }[] = (
  Object.entries(THEME_METADATA) as [ThemeId, { label: string }][]
)
  .map(([value, { label }]) => ({ label, value }))
  .sort((a, b) => a.label.localeCompare(b.label))

const MODE_LABELS: Record<ThemeMode, string> = {
  light: 'Light',
  dark: 'Dark',
  slate: 'Slate',
}

interface ThemeManagerDrawerProps {
  open: boolean
  onClose: () => void
}

const ThemeManagerDrawer = ({ open, onClose }: ThemeManagerDrawerProps) => {
  const {
    currentTheme,
    currentMode,
    availableModes,
    setCurrentTheme,
    setCurrentMode,
    userThemeConfig,
    setUserThemeConfig,
    token,
    defaultPrimaryColor,
    allowsPrimaryOverride,
  } = useTheme()

  const selectedPrimaryColor = (
    userThemeConfig?.colorPrimary ?? defaultPrimaryColor ?? token.colorPrimary
  ).toLowerCase()
  const colorOptions = useMemo(() => {
    if (!defaultPrimaryColor) return PRESET_COLORS

    const hasDefault = PRESET_COLORS.some(
      (option) => option.value.toLowerCase() === defaultPrimaryColor.toLowerCase(),
    )
    if (hasDefault) return PRESET_COLORS

    return [{ label: 'Theme Default', value: defaultPrimaryColor }, ...PRESET_COLORS]
  }, [defaultPrimaryColor])
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
          <Text strong>Theme</Text>
          <Select
            value={currentTheme}
            options={THEME_OPTIONS}
            onChange={(v) => {
              setCurrentTheme(v as ThemeId)
              setUserThemeConfig(
                userThemeConfig?.useCompactAlgorithm
                  ? { useCompactAlgorithm: true }
                  : null,
              )
            }}
            popupMatchSelectWidth
          />
        </Flex>

        {availableModes.length > 1 && (
          <Flex vertical gap="small">
            <Text strong>Mode</Text>
            <Segmented<ThemeMode>
              block
              value={currentMode}
              options={availableModes.map((mode) => ({
                label: MODE_LABELS[mode],
                value: mode,
              }))}
              onChange={(mode) => setCurrentMode(mode)}
            />
          </Flex>
        )}

        {allowsPrimaryOverride && (
          <Flex vertical gap="small">
            <Text strong>Primary Color</Text>
            <Flex wrap gap="small">
              {colorOptions.map(({ label, value }) => (
                <Tooltip key={value} title={label}>
                  <button
                    aria-label={`${label}${selectedPrimaryColor === value.toLowerCase() ? ' (selected)' : ''}`}
                    aria-pressed={selectedPrimaryColor === value.toLowerCase()}
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
                      selectedPrimaryColor === value.toLowerCase()
                        ? `0 0 0 2px #fff, 0 0 0 4px ${value}`
                        : undefined,
                      border: 'none',
                      padding: 0,
                    }}
                  >
                    {selectedPrimaryColor === value.toLowerCase() && (
                      <CheckOutlined style={{ color: '#fff', fontSize: 12 }} />
                    )}
                  </button>
                </Tooltip>
              ))}
            </Flex>
          </Flex>
        )}

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
                colorPrimary: allowsPrimaryOverride
                  ? userThemeConfig?.colorPrimary
                  : undefined,
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
