import { render, screen } from '@testing-library/react'
import {
  DeliverySignal,
  IntakeRequestDetailsDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import IntakeRequestFacts from './intake-request-facts'

// Linking a person checks the reader's permissions, which these tests are not about.
vi.mock('../../../_components/employee-name', () => ({
  default: ({ employee }: { employee: { name: string } }) => employee.name,
}))

const request = (
  category: StatusCategory,
  deliveryState?: DeliverySignal,
): IntakeRequestDetailsDto =>
  ({
    summary: {
      id: 'r-42',
      key: 42,
      status: { id: 's-1', name: 'Accepted', category },
      desk: { id: 'd-1', key: 1, name: 'Finance' },
      requester: { id: 'e-1', key: 1, name: 'Riley Requester' },
    },
    substance: undefined,
    estimateValues: [],
    deliveryState,
  }) as unknown as IntakeRequestDetailsDto

describe('IntakeRequestFacts', () => {
  it('shows the request’s delivery state', () => {
    // Arrange / Act
    render(
      <IntakeRequestFacts
        request={request(StatusCategory.Active, DeliverySignal.InProgress)}
      />,
    )

    // Assert
    expect(screen.getByText('Delivery')).toBeInTheDocument()
    expect(screen.getByText('In Progress')).toBeInTheDocument()
  })

  it('leaves delivery out when no outcome reports one', () => {
    // Arrange / Act
    render(<IntakeRequestFacts request={request(StatusCategory.Active)} />)

    // Assert
    expect(screen.queryByText('Delivery')).not.toBeInTheDocument()
  })

  it('leaves delivery out of a declined, withdrawn or duplicate request', () => {
    // Arrange / Act
    render(
      <IntakeRequestFacts
        request={request(StatusCategory.Removed, DeliverySignal.InProgress)}
      />,
    )

    // Assert
    expect(screen.queryByText('Delivery')).not.toBeInTheDocument()
    expect(screen.queryByText('In Progress')).not.toBeInTheDocument()
  })

  it('shows delivery on a finished request', () => {
    // Arrange / Act
    render(
      <IntakeRequestFacts
        request={request(StatusCategory.Done, DeliverySignal.Done)}
      />,
    )

    // Assert
    expect(screen.getByText('Delivery')).toBeInTheDocument()
    expect(screen.getByText('Done')).toBeInTheDocument()
  })
})
