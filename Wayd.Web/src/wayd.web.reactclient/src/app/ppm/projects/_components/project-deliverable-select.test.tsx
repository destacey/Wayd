import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ProjectDeliverableDto, StatusCategory } from '@/src/services/wayd-api'
import ProjectDeliverableSelect from './project-deliverable-select'

const mockQuery = jest.fn()
jest.mock('@/src/store/features/ppm/project-deliverables-api', () => ({
  useGetProjectDeliverablesQuery: (projectId: string) => mockQuery(projectId),
}))

const deliverable = (
  id: string,
  category: StatusCategory,
  statusName: string = category,
): ProjectDeliverableDto =>
  ({
    id,
    name: id,
    status: { id: `s-${id}`, name: statusName, category },
  }) as unknown as ProjectDeliverableDto

describe('ProjectDeliverableSelect', () => {
  beforeEach(() => {
    mockQuery.mockReturnValue({
      data: [
        deliverable('Portal', StatusCategory.Active),
        deliverable('Training', StatusCategory.Removed, 'Descoped'),
      ],
      isLoading: false,
    })
  })

  it('offers a descoped deliverable only while it is already chosen', async () => {
    // Arrange
    const user = userEvent.setup()
    render(<ProjectDeliverableSelect projectId="p-1" value={[]} />)

    // Act
    await user.click(screen.getByRole('combobox'))

    // Assert
    expect(await screen.findByTitle('Portal')).toBeInTheDocument()
    expect(screen.queryByTitle('Training (Descoped)')).not.toBeInTheDocument()
  })

  it('keeps a descoped deliverable the task already serves, so it can be removed', () => {
    // Arrange & Act
    render(<ProjectDeliverableSelect projectId="p-1" value={['Training']} />)

    // Assert
    expect(screen.getByText('Training (Descoped)')).toBeInTheDocument()
  })
})
