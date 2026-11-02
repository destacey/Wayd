import { render, screen } from '@testing-library/react'
import { Form } from 'antd'
import { Visibility } from '@/src/services/wayd-api'
import SessionAccessFields, {
  SessionAccessValues,
} from './session-access-fields'

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeOptionsQuery: () => ({ data: [] }),
}))

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamOptionsQuery: () => ({ data: [] }),
}))

jest.mock('@/src/components/common/organizations', () => ({
  EmployeeSelect: () => <div />,
}))

const renderFields = (
  values: Partial<SessionAccessValues>,
  selfEmployeeId?: string,
) =>
  render(
    <Form initialValues={values}>
      <SessionAccessFields selfEmployeeId={selfEmployeeId} />
    </Form>,
  )

describe('SessionAccessFields', () => {
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
