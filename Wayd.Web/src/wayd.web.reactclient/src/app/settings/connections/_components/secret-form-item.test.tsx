import { render, screen } from '@testing-library/react'
import { Form } from 'antd'
import { SecretFormItem, SecretFormItemProps } from './secret-form-item'

const renderItem = (props: Partial<SecretFormItemProps>) =>
  render(
    <Form>
      <SecretFormItem
        label="Personal Access Token"
        name="personalAccessToken"
        maxLength={128}
        mode="edit"
        {...props}
      />
    </Form>,
  )

describe('SecretFormItem', () => {
  it('names what needs the secret again when editing', () => {
    renderItem({ requiredAgainWhen: 'the organization' })

    expect(
      screen.getByText(
        'Leave blank to keep the current value, unless you change the organization.',
      ),
    ).toBeInTheDocument()
  })

  it('offers only the keep note when nothing needs the secret again', () => {
    renderItem({})

    expect(
      screen.getByText('Leave blank to keep the current value.'),
    ).toBeInTheDocument()
  })

  it('shows no keep note when creating', () => {
    renderItem({ mode: 'create', requiredAgainWhen: 'the organization' })

    expect(screen.queryByText(/Leave blank/)).not.toBeInTheDocument()
  })
})
