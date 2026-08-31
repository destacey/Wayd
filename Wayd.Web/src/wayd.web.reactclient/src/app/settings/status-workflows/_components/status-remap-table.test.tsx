import { render, screen, within } from '@testing-library/react'
import {
  StatusRemapEntryDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import StatusRemapTable from './status-remap-table'

const status = (id: string, name: string, order: number): WorkflowStatusDto =>
  ({
    id,
    name,
    category: { id: 2, name: 'Active' },
    alias: 0,
    order,
  }) as unknown as WorkflowStatusDto

const entry = (
  from: WorkflowStatusDto,
  matchedBy: string,
  to?: WorkflowStatusDto,
  recordCount = 0,
): StatusRemapEntryDto =>
  ({ from, to, matchedBy, recordCount }) as unknown as StatusRemapEntryDto

const TARGETS = [
  status('t1', 'Planned', 1),
  status('t2', 'Shipped', 2),
  status('t3', 'Pulled', 3),
]

describe('StatusRemapTable', () => {
  it('shows a row per source status', () => {
    // Arrange / Act
    render(
      <StatusRemapTable
        entries={[
          entry(status('s1', 'Draft', 1), 'Name', TARGETS[0]),
          entry(status('s2', 'Released', 2), 'Alias', TARGETS[1]),
        ]}
        targetStatuses={TARGETS}
        decisions={{ s1: 't1', s2: 't2' }}
        onChange={() => {}}
      />,
    )

    // Assert
    expect(screen.getByText('Draft')).toBeInTheDocument()
    expect(screen.getByText('Released')).toBeInTheDocument()
  })

  it('sorts unresolved rows to the top', () => {
    // They are the rows that need a person; buried under correct ones is how a
    // mapping gets confirmed unread.
    // Arrange / Act
    const { container } = render(
      <StatusRemapTable
        entries={[
          entry(status('s1', 'Draft', 1), 'Name', TARGETS[0]),
          entry(status('s2', 'On Hold', 2), 'Unresolved'),
        ]}
        targetStatuses={TARGETS}
        decisions={{ s1: 't1' }}
        onChange={() => {}}
      />,
    )

    // Assert
    const rows = container.querySelectorAll('tbody tr')
    expect(within(rows[0] as HTMLElement).getByText('On Hold')).toBeInTheDocument()
  })

  it('warns how many statuses still need a target', () => {
    // Arrange / Act
    render(
      <StatusRemapTable
        entries={[
          entry(status('s1', 'Draft', 1), 'Unresolved'),
          entry(status('s2', 'On Hold', 2), 'Unresolved'),
        ]}
        targetStatuses={TARGETS}
        decisions={{}}
        onChange={() => {}}
      />,
    )

    // Assert
    expect(screen.getByText(/2 statuses still need a target/)).toBeInTheDocument()
  })

  it('says nothing when every status is mapped', () => {
    // Arrange / Act
    render(
      <StatusRemapTable
        entries={[entry(status('s1', 'Draft', 1), 'Name', TARGETS[0])]}
        targetStatuses={TARGETS}
        decisions={{ s1: 't1' }}
        onChange={() => {}}
      />,
    )

    // Assert
    expect(screen.queryByText(/still need a target/)).not.toBeInTheDocument()
  })

  it('labels how each row was matched, so an operator knows what to check', () => {
    // An alias match is unambiguous; a category match is a lone-candidate guess.
    // Arrange / Act
    render(
      <StatusRemapTable
        entries={[
          entry(status('s1', 'Released', 1), 'Alias', TARGETS[1]),
          entry(status('s2', 'Draft', 2), 'Category', TARGETS[0]),
        ]}
        targetStatuses={TARGETS}
        decisions={{ s1: 't2', s2: 't1' }}
        onChange={() => {}}
      />,
    )

    // Assert
    expect(screen.getByText('Alias')).toBeInTheDocument()
    expect(screen.getByText('Category')).toBeInTheDocument()
  })

  it('marks a row the operator overrode as chosen, not as the machine decided it', () => {
    // Once a person picks a target, how it would have been matched is no longer
    // the truth about the row.
    // Arrange / Act
    render(
      <StatusRemapTable
        entries={[entry(status('s1', 'Released', 1), 'Alias', TARGETS[1])]}
        targetStatuses={TARGETS}
        decisions={{ s1: 't3' }}
        onChange={() => {}}
      />,
    )

    // Assert
    expect(screen.getByText('Chosen')).toBeInTheDocument()
    expect(screen.queryByText('Alias')).not.toBeInTheDocument()
  })

  it('shows how many records sit behind each status', () => {
    // The blast radius, per row.
    // Arrange / Act
    render(
      <StatusRemapTable
        entries={[entry(status('s1', 'Draft', 1), 'Name', TARGETS[0], 412)]}
        targetStatuses={TARGETS}
        decisions={{ s1: 't1' }}
        onChange={() => {}}
      />,
    )

    // Assert
    expect(screen.getByText('412')).toBeInTheDocument()
  })
})
