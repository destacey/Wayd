import { render, screen } from '@testing-library/react'
import IncidentStatusStepper from './incident-status-stepper'

vi.mock('@/src/store/features/product-management/incidents-api', () => ({
  useGetIncidentStatusWorkflowQuery: () => ({
    data: {
      statuses: [
        { id: 'open', name: 'Open', order: 1, isRetired: false },
        { id: 'responding', name: 'Responding', order: 2, isRetired: false },
        { id: 'resolved', name: 'Resolved', order: 3, isRetired: false },
        { id: 'closed', name: 'Closed', order: 4, isRetired: false },
        { id: 'canceled', name: 'Canceled', order: 5, isRetired: false },
      ],
    },
  }),
}))

describe('IncidentStatusStepper', () => {
  it('keeps a status name on one line so the record column cannot break it', () => {
    // Arrange
    const currentStatusId = 'responding'

    // Act
    render(
      <IncidentStatusStepper incidentId="incident-1" currentStatusId={currentStatusId} />,
    )

    // Assert
    const title = screen.getByText('Responding')
    expect(title).toHaveStyle({ whiteSpace: 'nowrap', flexShrink: '0' })
    expect(title.closest('.ant-steps-item')).toHaveStyle({ flexShrink: '0' })
    expect(title.closest('.ant-steps')).toHaveStyle({ overflowX: 'auto', minWidth: '0px' })
  })
})
