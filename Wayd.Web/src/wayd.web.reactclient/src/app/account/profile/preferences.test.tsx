jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    success: jest.fn(),
    error: jest.fn(),
  }),
}))

import { render, screen, fireEvent } from '@testing-library/react'

import Preferences from './preferences'
import { GRID_PERSISTENCE_ENABLED_KEY } from '@/src/components/common/wayd-grid'

describe('Preferences', () => {
  beforeEach(() => {
    // jest.setup's localStorage stub has no backing store — replace it with a
    // real in-memory implementation
    const localStorageMock = (() => {
      let store: Record<string, string> = {}
      return {
        getItem: (key: string) => store[key] ?? null,
        setItem: (key: string, value: string) => {
          store[key] = value
        },
        removeItem: (key: string) => {
          delete store[key]
        },
        clear: () => {
          store = {}
        },
        get length() {
          return Object.keys(store).length
        },
        key: (index: number) => {
          const keys = Object.keys(store)
          return keys[index] ?? null
        },
      }
    })()
    Object.defineProperty(window, 'localStorage', {
      value: localStorageMock,
      writable: true,
    })
  })

  it('defaults the remember-column-layouts switch to on', () => {
    // Arrange / Act
    render(<Preferences />)

    // Assert
    expect(
      screen.getByRole('switch', { name: 'Remember column layouts' }),
    ).toBeChecked()
  })

  it('writes the disabled flag when the switch is turned off', () => {
    // Arrange
    render(<Preferences />)

    // Act
    fireEvent.click(
      screen.getByRole('switch', { name: 'Remember column layouts' }),
    )

    // Assert — the grid hook reads this exact key at effect time
    expect(window.localStorage.getItem(GRID_PERSISTENCE_ENABLED_KEY)).toBe(
      'false',
    )
  })

  it('reflects a previously disabled flag', () => {
    // Arrange
    window.localStorage.setItem(GRID_PERSISTENCE_ENABLED_KEY, 'false')

    // Act
    render(<Preferences />)

    // Assert
    expect(
      screen.getByRole('switch', { name: 'Remember column layouts' }),
    ).not.toBeChecked()
  })

  it('reset-all clears every saved grid layout after confirmation', () => {
    // Arrange
    window.localStorage.setItem('wayd-grid:ppm-projects:v1', '{}')
    window.localStorage.setItem('wayd-grid:team-sprints:v1', '{}')
    window.localStorage.setItem('appTheme', '"dark"')
    render(<Preferences />)

    // Act — open the Popconfirm, then confirm
    fireEvent.click(screen.getByRole('button', { name: 'Reset All' }))
    fireEvent.click(screen.getByRole('button', { name: 'Reset' }))

    // Assert — grid layouts gone, unrelated keys untouched
    expect(
      window.localStorage.getItem('wayd-grid:ppm-projects:v1'),
    ).toBeNull()
    expect(
      window.localStorage.getItem('wayd-grid:team-sprints:v1'),
    ).toBeNull()
    expect(window.localStorage.getItem('appTheme')).toBe('"dark"')
  })
})
