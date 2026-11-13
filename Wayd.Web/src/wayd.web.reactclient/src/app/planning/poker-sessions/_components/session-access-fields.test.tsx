import { render, screen } from '@testing-library/react'
import { Form } from 'antd'
import { Visibility } from '@/src/services/wayd-api'
import SessionAccessFields, {
  SessionAccessValues,
} from './session-access-fields'

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
  values: Partial<SessionAccessValues>,
  selfEmployeeId?: string,
  alwaysPrivate?: boolean,
) =>
  render(
    <Form initialValues={values}>
      <SessionAccessFields
        selfEmployeeId={selfEmployeeId}
        alwaysPrivate={alwaysPrivate}
      />
    </Form>,
  )

describe('SessionAccessFields', () => {
  it('offers only Private for a session that belongs to another area', () => {
    renderFields(
      { visibility: Visibility.Private, facilitatorIds: ['me'] },
      undefined,
      true,
    )

    expect(screen.getByRole('radio', { name: 'Public' })).toBeDisabled()
    expect(screen.getByRole('radio', { name: 'Private' })).toBeEnabled()
    expect(screen.getByText(/always private/)).toBeInTheDocument()
  })

  it('offers Public for a standalone session', () => {
    renderFields({ visibility: Visibility.Private, facilitatorIds: ['me'] })

    expect(screen.getByRole('radio', { name: 'Public' })).toBeEnabled()
  })

  it('warns a facilitator who takes themselves off the list', () => {
    renderFields(
      { visibility: Visibility.Public, facilitatorIds: ['someone-else'] },
      'me',
    )

    expect(
      screen.getByText(/you will no longer be able to run this session/),
    ).toBeInTheDocument()
  })

  it('does not warn while the facilitator stays on the list', () => {
    renderFields(
      { visibility: Visibility.Public, facilitatorIds: ['me', 'someone-else'] },
      'me',
    )

    expect(
      screen.queryByText(/you will no longer be able to run this session/),
    ).not.toBeInTheDocument()
  })

  it('shows the invites only for a private session', () => {
    const { unmount } = renderFields({
      visibility: Visibility.Public,
      facilitatorIds: ['me'],
    })
    expect(screen.queryByText('Invited Teams')).not.toBeInTheDocument()
    unmount()

    renderFields({ visibility: Visibility.Private, facilitatorIds: ['me'] })
    expect(screen.getByText('Invited People')).toBeInTheDocument()
    expect(screen.getByText('Invited Teams')).toBeInTheDocument()
  })
})
