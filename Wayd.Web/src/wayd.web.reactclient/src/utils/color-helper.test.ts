import {
  getLuminance,
  getLuminanceTheme,
  getLifecycleCategoryColor,
  getLifecycleCategoryColorFromStatus,
  getLifecycleCategoryTagColor,
  getSemanticChartColor,
  softenChartColor,
} from './color-helper'
import { LifecycleCategory } from '../components/types'
import { LifecycleNavigationDto } from '../services/wayd-api'

describe('getLuminance', () => {
  test('should return 1 for a light color', () => {
    expect(getLuminance('#FFFFFF')).toBeCloseTo(1, 5)
  })

  test('should return 0 for a dark color', () => {
    expect(getLuminance('#000000')).toBeCloseTo(0, 5)
  })

  test('should return approximately 0.5 for a mid-tone color', () => {
    expect(getLuminance('#808080')).toBeCloseTo(0.5)
  })

  test('should handle invalid color format gracefully', () => {
    expect(() => getLuminance('invalid')).toThrow('Invalid hex color format')
  })
})

describe('getLuminanceTheme', () => {
  test('should return "light" for a light color', () => {
    expect(getLuminanceTheme('#FFFFFF')).toBe('light')
  })

  test('should return "dark" for a dark color', () => {
    expect(getLuminanceTheme('#000000')).toBe('dark')
  })

  test('should return "light" for a mid-tone color', () => {
    expect(getLuminanceTheme('#808080')).toBe('light')
  })

  test('should handle invalid color format gracefully', () => {
    expect(() => getLuminanceTheme('invalid')).toThrow(
      'Invalid hex color format',
    )
  })
})

describe('getLifecycleCategoryColor', () => {
  const mockToken = {
    colorPrimary: '#1677ff',
    colorSuccess: '#52c41a',
    colorTextTertiary: '#00000073',
  }

  test('should return colorPrimary for Active category', () => {
    expect(getLifecycleCategoryColor(LifecycleCategory.Active, mockToken as any)).toBe(
      mockToken.colorPrimary,
    )
  })

  test('should return colorSuccess for Completed category', () => {
    expect(getLifecycleCategoryColor(LifecycleCategory.Completed, mockToken as any)).toBe(
      mockToken.colorSuccess,
    )
  })

  test('should return undefined for NotStarted category', () => {
    expect(getLifecycleCategoryColor(LifecycleCategory.NotStarted, mockToken as any)).toBe(
      undefined,
    )
  })

  test('should return undefined for invalid category', () => {
    expect(getLifecycleCategoryColor(999 as LifecycleCategory, mockToken as any)).toBe(
      undefined,
    )
  })
})

describe('getLifecycleCategoryColorFromStatus', () => {
  const mockToken = {
    colorPrimary: '#1677ff',
    colorSuccess: '#52c41a',
    colorTextTertiary: '#00000073',
  }

  test('should return colorPrimary for Active lifecycleCategory', () => {
    const status: LifecycleNavigationDto = {
      id: 1,
      name: 'In Progress',
      lifecycleCategory: 'Active',
    }
    expect(getLifecycleCategoryColorFromStatus(status, mockToken)).toBe(
      mockToken.colorPrimary,
    )
  })

  test('should return colorSuccess for Completed lifecycleCategory', () => {
    const status: LifecycleNavigationDto = {
      id: 2,
      name: 'Completed',
      lifecycleCategory: 'Completed',
    }
    expect(getLifecycleCategoryColorFromStatus(status, mockToken)).toBe(
      mockToken.colorSuccess,
    )
  })

  test('should return undefined for NotStarted lifecycleCategory', () => {
    const status: LifecycleNavigationDto = {
      id: 3,
      name: 'Proposed',
      lifecycleCategory: 'NotStarted',
    }
    expect(getLifecycleCategoryColorFromStatus(status, mockToken)).toBe(undefined)
  })

  test('should handle invalid lifecycleCategory string', () => {
    const status: LifecycleNavigationDto = {
      id: 4,
      name: 'Unknown',
      lifecycleCategory: 'InvalidCategory' as any,
    }
    expect(getLifecycleCategoryColorFromStatus(status, mockToken)).toBe(undefined)
  })
})

describe('getLifecycleCategoryTagColor', () => {
  test('should return "processing" for Active category', () => {
    expect(getLifecycleCategoryTagColor(LifecycleCategory.Active)).toBe('processing')
  })

  test('should return "success" for Completed category', () => {
    expect(getLifecycleCategoryTagColor(LifecycleCategory.Completed)).toBe('success')
  })

  test('should return "default" for NotStarted category', () => {
    expect(getLifecycleCategoryTagColor(LifecycleCategory.NotStarted)).toBe('default')
  })

  test('should return "default" for invalid category', () => {
    expect(getLifecycleCategoryTagColor(999 as LifecycleCategory)).toBe('default')
  })
})

describe('softenChartColor', () => {
  test('should blend two hex colors with the default soften amount', () => {
    expect(softenChartColor('#000000', '#ffffff')).toBe('rgb(115, 115, 115)')
  })

  test('should support rgb() input colors', () => {
    expect(softenChartColor('rgb(255, 0, 0)', 'rgb(255, 255, 255)', 0.5)).toBe(
      'rgb(255, 128, 128)',
    )
  })

  test('should clamp softenBy below 0 to the base color', () => {
    expect(softenChartColor('#112233', '#ffffff', -1)).toBe('rgb(17, 34, 51)')
  })

  test('should clamp softenBy above 1 to the background color', () => {
    expect(softenChartColor('#112233', '#abcdef', 2)).toBe('rgb(171, 205, 239)')
  })

  test('should return baseColor when parsing fails', () => {
    expect(softenChartColor('not-a-color', '#ffffff', 0.5)).toBe('not-a-color')
  })

  test('should support rgba input by compositing over background first', () => {
    expect(softenChartColor('rgba(0, 0, 0, 0.25)', '#ffffff', 0.35)).toBe(
      'rgb(213, 213, 213)',
    )
  })

  test('should support 8-digit hex with alpha', () => {
    expect(softenChartColor('#00000040', '#ffffff', 0.35)).toBe(
      'rgb(213, 213, 213)',
    )
  })
})

describe('getSemanticChartColor', () => {
  const token = {
    colorInfo: '#1677ff',
    colorSuccess: '#52c41a',
    colorError: '#ff4d4f',
    colorWarning: '#faad14',
    colorTextSecondary: '#000000a6',
  }

  test('should map processing to colorInfo', () => {
    expect(getSemanticChartColor('processing', token as any)).toBe(
      token.colorInfo,
    )
  })

  test('should map success to colorSuccess', () => {
    expect(getSemanticChartColor('success', token as any)).toBe(
      token.colorSuccess,
    )
  })

  test('should map error to colorError', () => {
    expect(getSemanticChartColor('error', token as any)).toBe(token.colorError)
  })

  test('should map warning to colorWarning', () => {
    expect(getSemanticChartColor('warning', token as any)).toBe(
      token.colorWarning,
    )
  })

  test('should map default and unknown to colorTextSecondary', () => {
    expect(getSemanticChartColor('default', token as any)).toBe(
      token.colorTextSecondary,
    )
    expect(getSemanticChartColor('anything-else', token as any)).toBe(
      token.colorTextSecondary,
    )
  })
})
