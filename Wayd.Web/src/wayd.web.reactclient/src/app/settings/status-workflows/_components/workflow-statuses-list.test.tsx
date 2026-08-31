jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

import { render as rtlRender, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { App } from 'antd'
import {
  StatusWorkflowDetailsDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import WorkflowStatusesList from './workflow-statuses-list'

/** The list reaches for `App.useApp().modal` to confirm a delete. */
const render = (ui: React.ReactElement) =>
  rtlRender(<App>{ui}</App>)

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

jest.mock('@/src/store/features/common/status-workflows-api', () => ({
  useGetWorkflowOwnerTypesQuery: () => ({ data: [] }),
  useRemoveWorkflowStatusMutation: () => [jest.fn()],
  useReorderWorkflowStatusesMutation: () => [jest.fn()],
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasClaim: () => true,
    hasPermissionClaim: () => true,
  }),
}))

const status = (
  id: string,
  name: string,
  order: number,
): WorkflowStatusDto => ({
  id,
  name,
  category: { id: 2, name: 'Active' },
  alias: 0,
  order,
})

const NEW = status('11111111-0000-4000-a000-000000000001', 'New', 1)
const IN_FLIGHT = status('22222222-0000-4000-a000-000000000002', 'In Flight', 2)
const SHIPPED = status('33333333-0000-4000-a000-000000000003', 'Shipped', 3)

const workflow = (
  overrides: Partial<StatusWorkflowDetailsDto> = {},
): StatusWorkflowDetailsDto => ({
  id: '44444444-0000-4000-a000-000000000004',
  key: 7,
  name: 'Release Lifecycle',
  owner: { key: 'Release', name: 'Release' },
  state: 'Draft',
  isSystem: false,
  isAssigned: false,
  statuses: [NEW, IN_FLIGHT, SHIPPED],
  missingRequiredAliases: [],
  canEdit: true,
  canPublish: false,
  canArchive: false,
  ...overrides,
})

/** Opens the row menu for a status and returns the visible item labels. */
const openRowMenu = async (statusName: string) => {
  const user = userEvent.setup()
  const row = screen.getByText(statusName).closest('tr')!
  const trigger = row.querySelector<HTMLElement>(
    '[aria-label="Status actions"]',
  )!
  await user.click(trigger)
  return Array.from(document.querySelectorAll('.ant-dropdown-menu-item')).map(
    (el) => el.textContent?.trim(),
  )
}

describe('WorkflowStatusesList', () => {
  it('hides the actions column entirely when the workflow cannot be edited', () => {
    // Arrange — a published or system workflow is frozen, so offering row
    // actions that would all fail is worse than offering none.
    render(<WorkflowStatusesList statusWorkflow={workflow({ canEdit: false })} />)

    // Assert
    expect(
      screen.queryByLabelText('Status actions'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Add Status' }),
    ).not.toBeInTheDocument()
  })

  it('shows the actions column when the workflow can be edited', () => {
    // Arrange / Act
    render(<WorkflowStatusesList statusWorkflow={workflow()} />)

    // Assert
    expect(screen.getAllByLabelText('Status actions')).toHaveLength(3)
  })

  it('omits Move Up on the first status', async () => {
    // Arrange
    render(<WorkflowStatusesList statusWorkflow={workflow()} />)

    // Act
    const items = await openRowMenu('New')

    // Assert — nothing to swap with above the first row
    expect(items).not.toContain('Move Up')
    expect(items).toContain('Move Down')
  })

  it('omits Move Down on the last status', async () => {
    // Arrange
    render(<WorkflowStatusesList statusWorkflow={workflow()} />)

    // Act
    const items = await openRowMenu('Shipped')

    // Assert
    expect(items).toContain('Move Up')
    expect(items).not.toContain('Move Down')
  })

  it('offers both directions on a middle status', async () => {
    // Arrange
    render(<WorkflowStatusesList statusWorkflow={workflow()} />)

    // Act
    const items = await openRowMenu('In Flight')

    // Assert
    expect(items).toEqual(
      expect.arrayContaining(['Edit', 'Move Up', 'Move Down', 'Delete']),
    )
  })
})
