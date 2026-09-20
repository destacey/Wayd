import { act, fireEvent, render, screen } from '@testing-library/react'
import {
  DependencyStrength,
  InteractionStyle,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import ChangeProductDependencyTermsForm from './change-product-dependency-terms-form'

jest.unmock('dayjs')

const changeTerms = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useChangeProductDependencyTermsMutation: () => [changeTerms],
}))

/** The submit handler from the live form, so a test need not click a jsdom-disabled button. */
let submitForm: (() => Promise<void>) | undefined
let formInstance:
  | {
      setFieldsValue: (values: Record<string, unknown>) => void
      getFieldValue: (name: string) => unknown
    }
  | undefined

// The form instance is real — antd's Form binds to it, and a stub breaks on render. See
// complete-deployment-form.test.tsx for why the OK button's enabled state is not asserted.
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

const dependency = {
  id: 'dependency-1',
  product: { id: 'web', key: 2, name: 'Storefront Web' },
  dependsOnProduct: { id: 'identity', key: 3, name: 'Identity Service' },
  strength: DependencyStrength.Soft,
  startsOn: new Date('2026-03-01T00:00:00Z'),
} as ProductDependencyDto

const submit = async () => {
  await act(async () => {
    await submitForm!()
  })
}

beforeEach(() => {
  jest.clearAllMocks()
  changeTerms.mockResolvedValue({ data: 'new-id' })
})

describe('ChangeProductDependencyTermsForm', () => {
  it('starts on the current terms, so editing one field does not change the other', () => {
    // Arrange — with two fields there is no single "other" value to pre-select, and flipping the strength
    // would put a change in front of somebody who came to record the styles
    const recorded = {
      ...dependency,
      interactionStyles: [InteractionStyle.Synchronous],
    }

    // Act
    render(
      <ChangeProductDependencyTermsForm
        dependency={recorded}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Assert
    expect(formInstance!.getFieldValue('strength')).toBe(
      DependencyStrength.Soft,
    )
    expect(formInstance!.getFieldValue('interactionStyles')).toEqual([
      InteractionStyle.Synchronous,
    ])
  })

  it('records styles in place on a dependency that had none, without starting a new one', async () => {
    // Arrange
    render(
      <ChangeProductDependencyTermsForm
        dependency={dependency}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Act — the strength is untouched, so the only change is writing down what was always true
    await act(async () => {
      fireEvent.click(screen.getByRole('checkbox', { name: /^Asynchronous/ }))
    })

    // Assert
    expect(
      screen.getByText('This records how the products already talk'),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('The current dependency ends and a new one starts'),
    ).not.toBeInTheDocument()
  })

  it('lets a dependency started today record its styles, since that changes nothing', async () => {
    // Arrange — a real change would have to end the dependency the day before it started, but recording
    // styles keeps one period, so the day-after rule does not apply
    const startedToday = { ...dependency, startsOn: new Date() }

    render(
      <ChangeProductDependencyTermsForm
        dependency={startedToday}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('checkbox', { name: /^Synchronous/ }))
    })

    // Assert
    expect(
      screen.queryByText('This dependency started today'),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Change' })).toBeEnabled()
  })

  it('changes the link on the product that has it, naming the product depended on', async () => {
    // Arrange
    render(
      <ChangeProductDependencyTermsForm
        dependency={dependency}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Act
    await submit()

    // Assert
    expect(changeTerms).toHaveBeenCalledWith(
      expect.objectContaining({
        productId: 'web',
        dependencyId: 'dependency-1',
        dependsOnProductId: 'identity',
      }),
    )
  })

  it('explains that a dependency started today cannot change terms until tomorrow', () => {
    // Arrange — the current dependency would have to end the day before it started
    const startedToday = { ...dependency, startsOn: new Date() }

    // Act
    render(
      <ChangeProductDependencyTermsForm
        dependency={startedToday}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Assert
    expect(
      screen.getByText('This dependency started today'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Change' })).toBeDisabled()
  })

  it('does not warn about a dependency that started before today', () => {
    // Arrange / Act
    render(
      <ChangeProductDependencyTermsForm
        dependency={dependency}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Assert
    expect(
      screen.queryByText('This dependency started today'),
    ).not.toBeInTheDocument()
  })
})
