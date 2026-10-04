import { render } from '@testing-library/react'
import { SizingMethod } from '@/src/services/wayd-api'

// A stand-in that records the columns the grid is given.
const mockWaydGrid = jest.fn(
  (_props: { columns: { id?: string; header?: unknown }[] }) => null,
)

jest.mock('@/src/components/common/wayd-grid', () => ({
  WaydGrid: (props: { columns: { id?: string; header?: unknown }[] }) =>
    mockWaydGrid(props),
  createCsvColumn: jest.fn(() => ({ id: 'csv' })),
  renderAssignedToLink: jest.fn(),
  renderProjectLink: jest.fn(),
  renderSprintLink: jest.fn(),
  renderWorkItemLink: jest.fn(),
  renderWorkStatusTag: jest.fn(),
  workItemKeySort: jest.fn(),
  workStatusCategorySort: jest.fn(),
}))

import BacklogHealthGrid from './backlog-health-grid'

const columnsFor = (sizingMethod: SizingMethod) => {
  mockWaydGrid.mockClear()
  render(
    <BacklogHealthGrid
      workItems={[]}
      sizingMethod={sizingMethod}
      isLoading={false}
      refetch={jest.fn()}
    />,
  )
  return mockWaydGrid.mock.calls[0][0].columns
}

describe('BacklogHealthGrid', () => {
  it("heads the estimate column with the team's sizing method", () => {
    // Act
    const columns = columnsFor(SizingMethod.Effort)

    // Assert
    expect(columns.find((c) => c.id === 'estimate')?.header).toBe('Effort')
  })

  it('has no estimate column for a team that sizes by count', () => {
    // Act
    const columns = columnsFor(SizingMethod.Count)

    // Assert
    expect(columns.some((c) => c.id === 'estimate')).toBe(false)
  })

  it('gives the grid the same columns on every render', () => {
    // Act
    const first = columnsFor(SizingMethod.StoryPoints)
    const second = columnsFor(SizingMethod.StoryPoints)

    // Assert
    expect(second).toBe(first)
  })
})
