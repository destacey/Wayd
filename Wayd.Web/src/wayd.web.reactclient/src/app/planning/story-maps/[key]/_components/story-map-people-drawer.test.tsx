import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  StoryMapDetailsDto,
  StoryMapEditAccess,
  Visibility,
} from '@/src/services/wayd-api'
import StoryMapPeopleDrawer, { accessSummary } from './story-map-people-drawer'
import { useGetStoryMapEditorTeamMembersQuery } from '@/src/store/features/planning/story-maps-api'

jest.mock('@/src/store/features/planning/story-maps-api', () => ({
  useGetStoryMapEditorTeamMembersQuery: jest.fn(),
}))

const employee = (id: string, name: string) => ({ id, key: 1, name })

const map = {
  id: 'map-1',
  key: 7,
  visibility: Visibility.Public,
  editAccess: StoryMapEditAccess.OwnersAndEditors,
  owners: [employee('o-1', 'Olive Owner')],
  editors: [employee('e-1', 'Eddie Editor')],
  editorTeams: [
    { id: 'team-1', key: 1, name: 'Platform', code: 'PLT', type: 'Team' },
  ],
} as unknown as StoryMapDetailsDto

describe('StoryMapPeopleDrawer', () => {
  beforeEach(() => {
    ;(useGetStoryMapEditorTeamMembersQuery as jest.Mock).mockReturnValue({
      data: [employee('t-1', 'Tam Teammate')],
      isFetching: false,
    })
  })

  it('lists owners, editors and who can see and edit the map', () => {
    render(<StoryMapPeopleDrawer map={map} open onClose={jest.fn()} />)

    expect(screen.getByText('Olive Owner')).toBeInTheDocument()
    expect(screen.getByText('Eddie Editor')).toBeInTheDocument()
    expect(
      screen.getByText(/only owners and editors can edit it/),
    ).toBeInTheDocument()
  })

  it('expands an editor team into its members', async () => {
    render(<StoryMapPeopleDrawer map={map} open onClose={jest.fn()} />)

    expect(useGetStoryMapEditorTeamMembersQuery).toHaveBeenLastCalledWith(
      { storyMapId: 'map-1', teamId: 'team-1' },
      { skip: true },
    )

    await userEvent.click(screen.getByRole('button', { name: /Platform/ }))

    expect(useGetStoryMapEditorTeamMembersQuery).toHaveBeenLastCalledWith(
      { storyMapId: 'map-1', teamId: 'team-1' },
      { skip: false },
    )
    expect(screen.getByText('Tam Teammate')).toBeInTheDocument()
  })

  it('says when a map has no owners', () => {
    render(
      <StoryMapPeopleDrawer
        map={{ ...map, owners: [] }}
        open
        onClose={jest.fn()}
      />,
    )

    expect(
      screen.getByText(/A Story Maps administrator can manage this map/),
    ).toBeInTheDocument()
  })
})

describe('accessSummary', () => {
  it.each([
    [
      Visibility.Public,
      StoryMapEditAccess.AnyoneWhoCanView,
      'Public: anyone who can view Story Maps can see and edit this map.',
    ],
    [
      Visibility.Public,
      StoryMapEditAccess.OwnersAndEditors,
      'Public: anyone who can view Story Maps can see this map; only owners and editors can edit it.',
    ],
    [
      Visibility.Private,
      StoryMapEditAccess.AnyoneWhoCanView,
      'Private: only owners and editors can see and edit this map.',
    ],
  ])('describes %s with %s', (visibility, editAccess, expected) => {
    expect(accessSummary({ ...map, visibility, editAccess })).toBe(expected)
  })
})
