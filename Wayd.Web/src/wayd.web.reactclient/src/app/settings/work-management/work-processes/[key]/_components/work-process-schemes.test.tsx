import { render, screen } from '@testing-library/react'
import { WorkProcessSchemeDto } from '@/src/services/wayd-api'
import WorkProcessSchemes from './work-process-schemes'

const mockSchemesQuery = jest.fn()

jest.mock('@/src/store/features/work-management/work-process-api', () => ({
  useGetWorkProcessSchemesQuery: (...args: unknown[]) =>
    mockSchemesQuery(...args),
}))

const scheme = (
  overrides: Partial<WorkProcessSchemeDto> = {},
): WorkProcessSchemeDto =>
  ({
    id: 'b7e2d100-0000-0000-0000-000000000001',
    workType: { id: 1, name: 'Story', description: 'A unit of user value' },
    workflow: { id: 4, name: 'Agile Workflow' },
    isActive: true,
    ...overrides,
  }) as WorkProcessSchemeDto

const PROCESS_ID = 'a4f1c2e0-0000-0000-0000-000000000001'

describe('WorkProcessSchemes', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockSchemesQuery.mockReturnValue({ data: [scheme()], isLoading: false })
  })

  it('asks for the schemes of the process it was given', () => {
    // Arrange / Act
    render(<WorkProcessSchemes workProcessId={PROCESS_ID} />)

    // Assert
    expect(mockSchemesQuery).toHaveBeenCalledWith(PROCESS_ID)
  })

  it('renders a row per work type and its workflow', () => {
    // Arrange
    mockSchemesQuery.mockReturnValue({
      data: [
        scheme(),
        scheme({
          id: 'b7e2d100-0000-0000-0000-000000000002',
          workType: { id: 2, name: 'Bug', description: 'A defect' } as any,
          workflow: { id: 5, name: 'Bug Workflow' } as any,
        }),
      ],
      isLoading: false,
    })

    // Act
    render(<WorkProcessSchemes workProcessId={PROCESS_ID} />)

    // Assert — the mapping is the point of the section
    expect(screen.getByText('Story')).toBeInTheDocument()
    expect(screen.getByText('Agile Workflow')).toBeInTheDocument()
    expect(screen.getByText('Bug')).toBeInTheDocument()
    expect(screen.getByText('Bug Workflow')).toBeInTheDocument()
  })

  it('says the process has no work types when there are none', () => {
    // Arrange
    mockSchemesQuery.mockReturnValue({ data: [], isLoading: false })

    // Act
    render(<WorkProcessSchemes workProcessId={PROCESS_ID} />)

    // Assert
    expect(
      screen.getByText('This work process has no work types.'),
    ).toBeInTheDocument()
  })

  it('tolerates the query still loading', () => {
    // Arrange — data is undefined until the fetch resolves
    mockSchemesQuery.mockReturnValue({ data: undefined, isLoading: true })

    // Act / Assert — renders without throwing
    expect(() =>
      render(<WorkProcessSchemes workProcessId={PROCESS_ID} />),
    ).not.toThrow()
  })
})
