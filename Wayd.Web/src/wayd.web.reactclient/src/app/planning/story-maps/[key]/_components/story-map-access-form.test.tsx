import type { Mock } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  StoryMapDetailsDto,
  StoryMapEditAccess,
  Visibility,
} from '@/src/services/wayd-api'
import StoryMapAccessForm from './story-map-access-form'
import useAuth from '@/src/components/contexts/auth'
import {
  useSetStoryMapEditAccessMutation,
  useSetStoryMapEditorsMutation,
  useSetStoryMapOwnersMutation,
  useSetStoryMapVisibilityMutation,
} from '@/src/store/features/planning/story-maps-api'

vi.mock('@/src/components/contexts/auth', () => ({
  default: vi.fn(),
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

vi.mock('@/src/store/features/planning/story-maps-api', () => ({
  useSetStoryMapOwnersMutation: vi.fn(),
  useSetStoryMapEditorsMutation: vi.fn(),
  useSetStoryMapVisibilityMutation: vi.fn(),
  useSetStoryMapEditAccessMutation: vi.fn(),
}))

vi.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeOptionsQuery: () => ({ data: [] }),
}))

vi.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamOptionsQuery: () => ({ data: [] }),
}))

// A button that adds one employee, so a test can change a list without driving a real select.
vi.mock('@/src/components/common/organizations', () => ({
  EmployeeSelect: ({
    value,
    onChange,
    placeholder,
  }: {
    value?: string[]
    onChange?: (ids: string[]) => void
    placeholder: string
  }) => (
    <button type="button" onClick={() => onChange?.([...(value ?? []), 'new'])}>
      {placeholder}
    </button>
  ),
}))

const map = {
  id: 'map-1',
  key: 7,
  visibility: Visibility.Public,
  editAccess: StoryMapEditAccess.AnyoneWhoCanView,
  owners: [{ id: 'me', key: 1, name: 'Mia Me' }],
  editors: [],
  editorTeams: [],
} as unknown as StoryMapDetailsDto

const calls: string[] = []
const mutation = (name: string) => {
  const trigger = vi.fn(async () => {
    calls.push(name)
    return { data: null }
  })
  return [trigger]
}

const renderForm = () =>
  render(
    <StoryMapAccessForm
      map={map}
      onFormUpdate={vi.fn()}
      onFormCancel={vi.fn()}
    />,
  )

const save = async () => {
  const button = screen.getByRole('button', { name: 'Save' })
  await waitFor(() => expect(button).toBeEnabled())
  await userEvent.click(button)
}

describe('StoryMapAccessForm', () => {
  beforeEach(() => {
    calls.length = 0
    ;(useAuth as Mock).mockReturnValue({
      user: { employeeId: 'me' },
      hasPermissionClaim: () => true,
    })
    ;(useSetStoryMapOwnersMutation as Mock).mockReturnValue(mutation('owners'))
    ;(useSetStoryMapEditorsMutation as Mock).mockReturnValue(
      mutation('editors'),
    )
    ;(useSetStoryMapVisibilityMutation as Mock).mockReturnValue(
      mutation('visibility'),
    )
    ;(useSetStoryMapEditAccessMutation as Mock).mockReturnValue(
      mutation('editAccess'),
    )
  })

  it('saves editors before visibility, and owners last', async () => {
    renderForm()

    await userEvent.click(screen.getByText('Select people who can edit'))
    await userEvent.click(screen.getByText('Private'))
    await userEvent.click(screen.getByText('Select one or more owners'))
    await save()

    // Editors first, so the people added are let in when the map turns private; owners last,
    // since an owner who removes themselves can change nothing after it.
    await waitFor(() =>
      expect(calls).toEqual(['editors', 'visibility', 'owners']),
    )
  })

  it('sends only what changed', async () => {
    renderForm()

    await userEvent.click(screen.getByText('Owners and editors only'))
    await save()

    await waitFor(() => expect(calls).toEqual(['editAccess']))
  })
})
