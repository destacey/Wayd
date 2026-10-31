import { render, screen } from '@testing-library/react'
import DiscussionSettingsPanel from './discussion-settings-panel'

const mockUseGetDiscussionSettingsQuery = jest.fn()
jest.mock('@/src/store/features/common/discussions-api', () => ({
  useGetDiscussionSettingsQuery: (args: unknown) =>
    mockUseGetDiscussionSettingsQuery(args),
}))

jest.mock('./change-discussion-settings-form', () => ({
  __esModule: true,
  default: () => <div>settings form</div>,
}))

const settings = {
  channelType: 'demand.intake-request',
  scopeId: 'd-1',
  internalNotesEnabled: false,
  mentionsGrantAccess: true,
  isInherited: false,
  supportsInternalNotes: true,
  supportsMentionAccess: true,
  canManage: false,
}

const renderPanel = () =>
  render(
    <DiscussionSettingsPanel
      channelType="demand.intake-request"
      scopeId="d-1"
      scopeName="IT Help Desk"
    />,
  )

describe('DiscussionSettingsPanel', () => {
  it('asks for the scope it is given', () => {
    mockUseGetDiscussionSettingsQuery.mockReturnValue({
      data: settings,
      isLoading: false,
    })

    renderPanel()

    expect(mockUseGetDiscussionSettingsQuery).toHaveBeenCalledWith({
      channelType: 'demand.intake-request',
      scopeId: 'd-1',
    })
  })

  it('says what each setting does', () => {
    mockUseGetDiscussionSettingsQuery.mockReturnValue({
      data: settings,
      isLoading: false,
    })

    renderPanel()

    expect(
      screen.getByText('Internal notes are off: nobody can post a new one.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Mentioning someone can give them access.'),
    ).toBeInTheDocument()
  })

  it('offers no change to someone who cannot manage them', () => {
    mockUseGetDiscussionSettingsQuery.mockReturnValue({
      data: settings,
      isLoading: false,
    })

    renderPanel()

    expect(
      screen.queryByRole('button', { name: 'Change Conversation Settings' }),
    ).toBeNull()
  })

  it('offers a change to someone who can, and says when they are inherited', () => {
    mockUseGetDiscussionSettingsQuery.mockReturnValue({
      data: { ...settings, canManage: true, isInherited: true },
      isLoading: false,
    })

    renderPanel()

    expect(
      screen.getByRole('button', { name: 'Change Conversation Settings' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Following the default.')).toBeInTheDocument()
  })

  it('says so when the settings cannot be loaded', () => {
    mockUseGetDiscussionSettingsQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    renderPanel()

    expect(
      screen.getByText('Could not load the conversation settings.'),
    ).toBeInTheDocument()
  })

  it('says when mentions cannot give access', () => {
    mockUseGetDiscussionSettingsQuery.mockReturnValue({
      data: { ...settings, mentionsGrantAccess: false },
      isLoading: false,
    })

    renderPanel()

    expect(
      screen.getByText(
        'Only people who already have the record can be mentioned.',
      ),
    ).toBeInTheDocument()
  })
})
