import { act, render, screen } from '@testing-library/react'
import { ProductDto } from '@/src/services/wayd-api'
import LinkProductTeamForm from './link-product-team-form'

const linkTeam = jest.fn()
const messageError = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: messageError, success: jest.fn() }),
}))

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useLinkProductTeamMutation: () => [linkTeam],
}))

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamOptionsQuery: () => ({
    data: [
      { label: 'Atlas', value: 'atlas' },
      { label: 'Payments', value: 'payments' },
    ],
    isLoading: false,
  }),
}))

/** The submit handler from the live form, so a test need not click a jsdom-disabled button. */
let submitForm: (() => Promise<void>) | undefined
let formInstance:
  | {
      setFieldsValue: (values: Record<string, unknown>) => void
      getFieldError: (name: string) => string[]
    }
  | undefined

// The form instance is real — antd's Form binds to it, and a stub breaks on render.
jest.mock('@/src/hooks', () => {
  const { Form } = jest.requireActual('antd')
  return {
    useModalForm: ({
      onSubmit,
    }: {
      onSubmit: (
        values: Record<string, unknown>,
        form: unknown,
      ) => Promise<boolean>
    }) => {
      const [form] = Form.useForm()
      formInstance = form
      const handleOk = async () => {
        await onSubmit(form.getFieldsValue(true), form)
      }
      submitForm = handleOk
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk,
        handleCancel: jest.fn(),
      }
    },
  }
})

const product = {
  id: 'web',
  key: 2,
  name: 'Storefront Web',
  teams: [{ id: 'atlas', key: 3, name: 'Atlas', code: 'ATL' }],
} as ProductDto

const submit = async () => {
  await act(async () => {
    await submitForm!()
  })
}

beforeEach(() => {
  jest.clearAllMocks()
  linkTeam.mockResolvedValue({ data: undefined })
})

describe('LinkProductTeamForm', () => {
  it('links the chosen team to the product', async () => {
    // Arrange
    render(
      <LinkProductTeamForm
        product={product}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )
    act(() => formInstance!.setFieldsValue({ teamId: 'payments' }))

    // Act
    await submit()

    // Assert
    expect(linkTeam).toHaveBeenCalledWith({
      productId: 'web',
      teamId: 'payments',
    })
  })

  it('leaves out the teams already linked to the product', async () => {
    // Arrange
    render(
      <LinkProductTeamForm
        product={product}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Act
    await act(async () => {
      screen.getByRole('combobox').focus()
    })
    const { fireEvent } = await import('@testing-library/react')
    fireEvent.mouseDown(screen.getByRole('combobox'))

    // Assert
    expect(await screen.findByTitle('Payments')).toBeInTheDocument()
    expect(screen.queryByTitle('Atlas')).not.toBeInTheDocument()
  })

  it('marks the team field a validation error names', async () => {
    // Arrange
    linkTeam.mockResolvedValue({
      error: { status: 422, errors: { TeamId: ['Team Id must not be empty.'] } },
    })
    render(
      <LinkProductTeamForm
        product={product}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )
    act(() => formInstance!.setFieldsValue({ teamId: 'payments' }))

    // Act
    await submit()

    // Assert
    expect(formInstance!.getFieldError('teamId')).toEqual([
      'Team Id must not be empty.',
    ])
    expect(messageError).toHaveBeenCalledWith(
      'Correct the validation error(s) to continue.',
    )
  })

  it('shows the refusal the API gives', async () => {
    // Arrange
    linkTeam.mockResolvedValue({
      error: {
        status: 400,
        detail: 'An inactive team cannot be linked to a product.',
      },
    })
    render(
      <LinkProductTeamForm
        product={product}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )
    act(() => formInstance!.setFieldsValue({ teamId: 'payments' }))

    // Act
    await submit()

    // Assert
    expect(messageError).toHaveBeenCalledWith(
      'An inactive team cannot be linked to a product.',
    )
  })
})
