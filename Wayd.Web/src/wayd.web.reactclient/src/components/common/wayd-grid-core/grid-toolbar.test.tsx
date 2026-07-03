import { render, screen } from '@testing-library/react'
import GridToolbar from './grid-toolbar'

describe('GridToolbar', () => {
  const defaultProps = {
    displayedRowCount: 5,
    totalRowCount: 10,
    searchValue: '',
    onSearchChange: jest.fn(),
    onClearFilters: jest.fn(),
    hasActiveFilters: false,
    isLoading: false,
  }

  it('renders row count', () => {
    // Arrange / Act
    render(<GridToolbar {...defaultProps} />)

    // Assert
    expect(screen.getByText('5 of 10')).toBeInTheDocument()
  })

  it('renders search input with value', () => {
    // Arrange / Act
    render(<GridToolbar {...defaultProps} searchValue="test query" />)

    // Assert
    const input = screen.getByPlaceholderText('Search')
    expect(input).toHaveValue('test query')
  })

  it('hides the search input when includeGlobalSearch is false', () => {
    // Arrange / Act
    render(<GridToolbar {...defaultProps} includeGlobalSearch={false} />)

    // Assert
    expect(screen.queryByPlaceholderText('Search')).not.toBeInTheDocument()
  })

  it('renders leftSlot content', () => {
    // Arrange / Act
    render(
      <GridToolbar {...defaultProps} leftSlot={<button>Create Task</button>} />,
    )

    // Assert
    expect(screen.getByText('Create Task')).toBeInTheDocument()
  })

  it('renders rightSlot content after the export button', () => {
    // Arrange / Act
    render(
      <GridToolbar
        {...defaultProps}
        onExportCsv={jest.fn()}
        rightSlot={<button>View</button>}
      />,
    )

    // Assert — rightSlot renders at the far right (after export)
    const download = screen.getByLabelText('download').closest('button')!
    const viewButton = screen.getByText('View').closest('button')!
    expect(
      download.compareDocumentPosition(viewButton) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
  })

  it('renders help popover button when helpContent provided', () => {
    // Arrange / Act
    render(
      <GridToolbar
        {...defaultProps}
        helpContent={<div>Keyboard shortcuts here</div>}
      />,
    )

    // Assert — QuestionCircleOutlined icon has aria-label="question-circle"
    expect(screen.getByLabelText('question-circle')).toBeInTheDocument()
  })

  it('does not render help button when helpContent is not provided', () => {
    // Arrange / Act
    render(<GridToolbar {...defaultProps} />)

    // Assert
    expect(screen.queryByLabelText('question-circle')).not.toBeInTheDocument()
  })

  it('disables clear filters button when hasActiveFilters is false', () => {
    // Arrange / Act
    render(<GridToolbar {...defaultProps} hasActiveFilters={false} />)

    // Assert — ClearOutlined icon has aria-label="clear"
    const clearIcon = screen.getByLabelText('clear')
    expect(clearIcon.closest('button')).toBeDisabled()
  })

  it('enables clear filters button when hasActiveFilters is true', () => {
    // Arrange / Act
    render(<GridToolbar {...defaultProps} hasActiveFilters={true} />)

    // Assert
    const clearIcon = screen.getByLabelText('clear')
    expect(clearIcon.closest('button')).not.toBeDisabled()
  })

  it('renders refresh button when onRefresh is provided', () => {
    // Arrange / Act
    render(<GridToolbar {...defaultProps} onRefresh={jest.fn()} />)

    // Assert
    expect(screen.getByLabelText('reload')).toBeInTheDocument()
  })

  it('does not render refresh button when onRefresh is not provided', () => {
    // Arrange / Act
    render(<GridToolbar {...defaultProps} />)

    // Assert
    expect(screen.queryByLabelText('reload')).not.toBeInTheDocument()
  })

  it('renders export button when onExportCsv is provided', () => {
    // Arrange / Act
    render(<GridToolbar {...defaultProps} onExportCsv={jest.fn()} />)

    // Assert
    expect(screen.getByLabelText('download')).toBeInTheDocument()
  })

  it('disables export button when loading', () => {
    // Arrange / Act
    render(
      <GridToolbar {...defaultProps} onExportCsv={jest.fn()} isLoading={true} />,
    )

    // Assert
    const downloadIcon = screen.getByLabelText('download')
    expect(downloadIcon.closest('button')).toBeDisabled()
  })

  it('disables export button when displayedRowCount is 0', () => {
    // Arrange / Act
    render(
      <GridToolbar
        {...defaultProps}
        onExportCsv={jest.fn()}
        displayedRowCount={0}
      />,
    )

    // Assert
    const downloadIcon = screen.getByLabelText('download')
    expect(downloadIcon.closest('button')).toBeDisabled()
  })
})
