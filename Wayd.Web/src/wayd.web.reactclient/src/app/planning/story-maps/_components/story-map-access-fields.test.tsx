import { render, screen } from '@testing-library/react'
import { Form } from 'antd'
import { StoryMapEditAccess, Visibility } from '@/src/services/wayd-api'
import StoryMapAccessFields, {
  StoryMapAccessValues,
} from './story-map-access-fields'

vi.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeOptionsQuery: () => ({ data: [] }),
}))

vi.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamOptionsQuery: () => ({ data: [] }),
}))

vi.mock('@/src/components/common/organizations', () => ({
  EmployeeSelect: () => <div />,
}))

const renderFields = (
  values: Partial<StoryMapAccessValues>,
  selfEmployeeId?: string,
) =>
  render(
    <Form
      initialValues={{
        editAccess: StoryMapEditAccess.AnyoneWhoCanView,
        ...values,
      }}
    >
      <StoryMapAccessFields selfEmployeeId={selfEmployeeId} />
    </Form>,
  )

describe('StoryMapAccessFields', () => {
  it('warns an owner who takes themselves off the list', () => {
    renderFields(
      { visibility: Visibility.Public, ownerIds: ['someone-else'] },
      'me',
    )

    expect(
      screen.getByText(/you will no longer be able to manage this map/),
    ).toBeInTheDocument()
  })

  it('does not warn while the owner stays on the list', () => {
    renderFields(
      { visibility: Visibility.Public, ownerIds: ['me', 'someone-else'] },
      'me',
    )

    expect(
      screen.queryByText(/you will no longer be able to manage this map/),
    ).not.toBeInTheDocument()
  })

  it('asks who can edit only for a public map', () => {
    const { unmount } = renderFields({
      visibility: Visibility.Public,
      ownerIds: ['me'],
    })
    expect(screen.getByText('Who Can Edit')).toBeInTheDocument()
    expect(screen.getByText('Owners and editors only')).toBeInTheDocument()
    unmount()

    renderFields({ visibility: Visibility.Private, ownerIds: ['me'] })
    expect(screen.queryByText('Who Can Edit')).not.toBeInTheDocument()
    expect(
      screen.getByText('Only owners and editors can see and edit this map.'),
    ).toBeInTheDocument()
  })

  it('shows the editors whatever the visibility', () => {
    renderFields({ visibility: Visibility.Public, ownerIds: ['me'] })

    expect(screen.getByText('Editors')).toBeInTheDocument()
    expect(screen.getByText('Editor Teams')).toBeInTheDocument()
  })
})
