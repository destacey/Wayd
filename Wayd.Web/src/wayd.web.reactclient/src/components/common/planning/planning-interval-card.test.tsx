import React from 'react'
import { render, screen } from '@testing-library/react'
import { IterationState } from '../../types'
import PlanningIntervalCard from './planning-interval-card'
import { useGetPlanningIntervalIterationsQuery } from '@/src/store/features/planning/planning-interval-api'

jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalIterationsQuery: jest.fn(),
}))

jest.mock('next/link', () => {
  const MockedLink = ({
    children,
    href,
  }: {
    children: React.ReactNode
    href: string
  }) => <a href={href}>{children}</a>
  MockedLink.displayName = 'Link'
  return MockedLink
})

const mockIterationsQuery =
  useGetPlanningIntervalIterationsQuery as unknown as jest.Mock

const createPlanningInterval = (overrides: Record<string, unknown> = {}) =>
  ({
    id: 'pi-1',
    key: 42,
    name: '23.4',
    start: new Date('2026-01-01'),
    end: new Date('2026-03-31'),
    state: { id: IterationState.Active, name: 'Active' },
    ...overrides,
  }) as any

describe('PlanningIntervalCard', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockIterationsQuery.mockReturnValue({ data: [] })
  })

  // Every state lands on Overview now. The card used to send a Future PI to a
  // separate Details page instead, duplicating a rule the record page owned.
  it.each([
    ['Future', IterationState.Future],
    ['Active', IterationState.Active],
    ['Completed', IterationState.Completed],
  ])('links %s planning intervals to the record', (name, id) => {
    // Arrange / Act
    render(
      <PlanningIntervalCard
        planningInterval={createPlanningInterval({ state: { id, name } })}
      />,
    )

    // Assert
    expect(screen.getByRole('link', { name: 'Overview' })).toHaveAttribute(
      'href',
      '/planning/planning-intervals/42',
    )
    expect(
      screen.queryByRole('link', { name: 'Details' }),
    ).not.toBeInTheDocument()
  })

  it('links Plan Review to the section rather than a page', () => {
    // Arrange / Act
    render(
      <PlanningIntervalCard
        planningInterval={createPlanningInterval({
          state: { id: IterationState.Active, name: 'Active' },
        })}
      />,
    )

    // Assert
    expect(screen.getByRole('link', { name: 'Plan Review' })).toHaveAttribute(
      'href',
      '/planning/planning-intervals/42?section=plan-review',
    )
  })
})

