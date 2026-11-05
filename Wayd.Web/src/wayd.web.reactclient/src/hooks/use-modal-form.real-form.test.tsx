import { act, render, screen } from '@testing-library/react'
import { Form, Input, Modal } from 'antd'
import useModalForm from './use-modal-form'

// use-modal-form.test.ts replaces antd with a stub, which cannot show how a real Form registers its fields
// after the modal opens. That ordering is what these tests are about.

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: () => true }),
}))

const TestForm = ({
  required,
  initialNote,
}: {
  required: boolean
  initialNote?: string
}) => {
  const { form, isOpen, isValid, handleOk, handleCancel } = useModalForm({
    onSubmit: async () => true,
    onComplete: () => {},
    onCancel: () => {},
  })

  return (
    <Modal
      open={isOpen}
      onOk={handleOk}
      onCancel={handleCancel}
      okText="Save"
      okButtonProps={{ disabled: !isValid }}
      destroyOnHidden
    >
      <Form form={form} name="test-form" initialValues={{ note: initialNote }}>
        <Form.Item
          name="note"
          label="Note"
          rules={required ? [{ required: true }] : []}
        >
          <Input />
        </Form.Item>
      </Form>
    </Modal>
  )
}

/** Lets the modal mount, its fields register, and every validation settle. */
const settle = () =>
  act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 50))
  })

const saveButton = () =>
  screen.getByRole('button', { name: 'Save' }) as HTMLButtonElement

describe('useModalForm with a real form', () => {
  it('enables saving a form whose fields are all optional and empty', async () => {
    // Arrange / Act
    render(<TestForm required={false} />)
    await settle()

    // Assert — an End dialog whose only field is "leave empty for today"
    expect(saveButton().disabled).toBe(false)
  })

  it('keeps saving disabled while a required field is empty', async () => {
    // Arrange / Act
    render(<TestForm required={true} />)
    await settle()

    // Assert
    expect(saveButton().disabled).toBe(true)
  })

  it('enables saving a form that opens with its required field filled', async () => {
    // Arrange / Act
    render(
      <TestForm
        required={true}
        initialNote="Recorded against the wrong product"
      />,
    )
    await settle()

    // Assert
    expect(saveButton().disabled).toBe(false)
  })
})
