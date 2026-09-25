import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import AttentionTiles from './attention-tiles'
import { AttentionCounts } from './dashboard-model'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: () => ({
    token: {
      colorPrimary: '#1677ff',
      colorError: '#ff4d4f',
      colorWarning: '#faad14',
    },
  }),
}))

const counts: AttentionCounts = {
  inScope: 10,
  unhealthy: 2,
  atRisk: 1,
  overdueTasks: 18,
  overdueProjects: 5,
  noHealthCheck: 3,
  endingSoon: 4,
}

describe('AttentionTiles', () => {
  it('renders one tile per signal with its count', () => {
    // Arrange / Act
    render(
      <AttentionTiles
        counts={counts}
        active="all"
        onChange={jest.fn()}
        isLoading={false}
      />,
    )

    // Assert
    expect(
      screen.getByRole('button', { name: 'In scope: 10' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Unhealthy: 2' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Overdue tasks: 18' }),
    ).toBeInTheDocument()
    expect(screen.getByText('across 5 projects')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Ending in 30 days: 4' }),
    ).toBeInTheDocument()
  })

  it('selects a tile on click and clears it on a second click', async () => {
    // Arrange
    const onChange = jest.fn()
    const { rerender } = render(
      <AttentionTiles
        counts={counts}
        active="all"
        onChange={onChange}
        isLoading={false}
      />,
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'At risk: 1' }))

    // Assert
    expect(onChange).toHaveBeenCalledWith('atRisk')

    // Act — the active tile says so, and clicking it again clears the filter
    rerender(
      <AttentionTiles
        counts={counts}
        active="atRisk"
        onChange={onChange}
        isLoading={false}
      />,
    )
    await userEvent.click(
      screen.getByRole('button', { name: 'At risk: 1 (filtering)' }),
    )
    expect(onChange).toHaveBeenLastCalledWith('all')
  })
})
