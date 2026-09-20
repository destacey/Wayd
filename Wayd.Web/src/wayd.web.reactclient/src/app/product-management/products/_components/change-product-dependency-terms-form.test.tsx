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

    // The title and button have to agree with the alert — a reader acts on the title, and "Change"
    // sends them looking for an ended dependency that was never created.
    expect(screen.getByText('Record Interaction Styles')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Record' })).toBeInTheDocument()
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
    expect(screen.getByRole('button', { name: 'Record' })).toBeEnabled()
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

  it('explains that a dependency started today cannot change terms until tomorrow', async () => {
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

    // Act — a genuine change of terms, which is what the day-after rule applies to
    await act(async () => {
      fireEvent.click(screen.getByRole('radio', { name: /^Hard/ }))
    })

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

  it('claims no outcome before anything has been chosen', () => {
    // Arrange / Act — the dialog opens on the current terms, so nothing will happen yet. Asserting that
    // the dependency ends and a new one starts would describe an action nobody has taken.
    render(
      <ChangeProductDependencyTermsForm
        dependency={dependency}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Assert
    expect(screen.getByText('Nothing has changed yet')).toBeInTheDocument()
    expect(
      screen.queryByText('The current dependency ends and a new one starts'),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Change' })).toBeDisabled()

    // The day belongs to a dependency that is not being created, so offering it would invite a choice
    // that is silently discarded.
    expect(screen.getByLabelText('First Day')).toBeDisabled()
  })

  it('offers the first day only once a change that starts a new dependency is chosen', async () => {
    // Arrange
    render(
      <ChangeProductDependencyTermsForm
        dependency={dependency}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )
    expect(screen.getByLabelText('First Day')).toBeDisabled()

    // Act — a genuine change of terms
    await act(async () => {
      fireEvent.click(screen.getByRole('radio', { name: /^Hard/ }))
    })

    // Assert
    expect(screen.getByLabelText('First Day')).toBeEnabled()
  })

  it('does not offer the first day when the styles are merely being recorded', async () => {
    // Arrange
    render(
      <ChangeProductDependencyTermsForm
        dependency={dependency}
        onFormComplete={() => {}}
        onFormCancel={() => {}}
      />,
    )

    // Act — fills a blank, so one period is kept and no day applies
    await act(async () => {
      fireEvent.click(screen.getByRole('checkbox', { name: /^Synchronous/ }))
    })

    // Assert
    expect(screen.getByLabelText('First Day')).toBeDisabled()
  })
})
