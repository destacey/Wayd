import { act, fireEvent, render, screen } from '@testing-library/react'
import { ProductDto } from '@/src/services/wayd-api'
import ManageProductTeamsForm from './manage-product-teams-form'

const setTeams = jest.fn()
const messageError = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: messageError, success: jest.fn() }),
}))

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useSetProductTeamsMutation: () => [setTeams],
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
      getFieldsValue: (all: true) => Record<string, unknown>
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

// Payments is linked and active; Legacy Ops is linked but has since been deactivated.
const product = {
  id: 'web',
  key: 2,
  name: 'Storefront Web',
  teams: [
    { id: 'payments', key: 7, name: 'Payments', code: 'PAY', isActive: true },
    { id: 'legacy', key: 9, name: 'Legacy Ops', code: 'LEG', isActive: false },
  ],
} as unknown as ProductDto

const renderForm = () =>
  render(
    <ManageProductTeamsForm
      product={product}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

const submit = async () => {
  await act(async () => {
    await submitForm!()
  })
}

beforeEach(() => {
  jest.clearAllMocks()
  setTeams.mockResolvedValue({ data: undefined })
})

describe('ManageProductTeamsForm', () => {
  it("starts with the product's current teams selected, an inactive one by name, and nothing to save", () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(formInstance!.getFieldsValue(true).teamIds).toEqual([
      'payments',
      'legacy',
    ])
    expect(screen.getByText('Legacy Ops (inactive)')).toBeInTheDocument()
    expect(screen.getByText('No changes')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('saves the whole set, adding and removing in one change', async () => {
    // Arrange
    renderForm()
    act(() => formInstance!.setFieldsValue({ teamIds: ['payments', 'atlas'] }))

    // Act
    await submit()

    // Assert
    expect(setTeams).toHaveBeenCalledWith({
      productId: 'web',
      teamIds: ['payments', 'atlas'],
    })
  })

  it('says what will change once a team is removed, and allows saving', async () => {
    // Arrange
    renderForm()
    const legacy = screen
      .getByText('Legacy Ops (inactive)')
      .closest('.ant-select-selection-item')!

    // Act
    await act(async () => {
      fireEvent.click(
        legacy.querySelector('.ant-select-selection-item-remove')!,
      )
    })

    // Assert
    expect(
      await screen.findByText('0 to link, 1 to unlink'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled()
  })

  it('shows the refusal the API gives', async () => {
    // Arrange
    setTeams.mockResolvedValue({
      error: {
        status: 400,
        detail: 'Atlas: An inactive team cannot be linked to a product.',
      },
    })
    renderForm()
    act(() => formInstance!.setFieldsValue({ teamIds: ['atlas'] }))

    // Act
    await submit()

    // Assert
    expect(messageError).toHaveBeenCalledWith(
      'Atlas: An inactive team cannot be linked to a product.',
    )
  })
})
