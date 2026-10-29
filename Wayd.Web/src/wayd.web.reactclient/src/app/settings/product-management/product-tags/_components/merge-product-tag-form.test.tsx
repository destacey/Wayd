import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ProductTagListItemDto } from '@/src/services/wayd-api'
import MergeProductTagForm from './merge-product-tag-form'

const mockMerge = jest.fn()

const source: ProductTagListItemDto = {
  id: 'free-iphone',
  name: 'iphone',
  isFree: true,
  isActive: true,
  productCount: 2,
}

const TAGS: ProductTagListItemDto[] = [
  source,
  {
    id: 'tag-ios',
    name: 'ios',
    categoryId: 'cat-platform',
    categoryName: 'Platform',
    isFree: false,
    isActive: true,
    productCount: 5,
  },
  {
    id: 'free-apple',
    name: 'apple',
    isFree: true,
    isActive: true,
    productCount: 1,
  },
]

// The form instance is real so antd's Form binds to it; handleOk runs the real
// submit, as the other tag form tests do.
jest.mock('@/src/hooks', () => {
  const { Form: AntForm } = jest.requireActual('antd')
  return {
    useModalForm: ({
      onSubmit,
    }: {
      onSubmit: (values: unknown, form: unknown) => Promise<boolean>
    }) => {
      const [form] = AntForm.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => await onSubmit(form.getFieldsValue(), form),
        handleCancel: jest.fn(),
      }
    },
  }
})

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/store/features/product-management/product-tags-api', () => ({
  useMergeProductTagMutation: () => [mockMerge],
  useSearchProductTagsQuery: () => ({ data: TAGS, isLoading: false }),
}))

const renderForm = () =>
  render(
    <MergeProductTagForm
      tag={source}
      onFormComplete={jest.fn()}
      onFormCancel={jest.fn()}
    />,
  )

const openTargets = async () => {
  await act(async () => {
    await userEvent.click(screen.getByRole('combobox'))
  })
  return await waitFor(() => {
    const dropdown = document.querySelector('.ant-select-dropdown')
    if (!dropdown) throw new Error('no dropdown')
    return dropdown as HTMLElement
  })
}

describe('MergeProductTagForm', () => {
  beforeEach(() => {
    mockMerge.mockReset().mockResolvedValue({ data: undefined })
  })

  it('offers every other tag, a tag on a category named with it', async () => {
    // Arrange
    renderForm()

    // Act
    const dropdown = await openTargets()

    // Assert
    const options = [...dropdown.querySelectorAll('.ant-select-item-option')]
    expect(options.map((option) => option.getAttribute('title'))).toEqual([
      'apple',
      'Platform | ios',
    ])
  })

  it('says how many products move', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(
      screen.getByText(/The 2 product\(s\) carrying "iphone"/),
    ).toBeInTheDocument()
  })

  it('merges into the chosen tag', async () => {
    // Arrange
    renderForm()
    const dropdown = await openTargets()

    // Act
    await act(async () => {
      await userEvent.click(
        dropdown.querySelector('[title="Platform | ios"]') as HTMLElement,
      )
    })
    await act(async () => {
      await userEvent.click(screen.getByRole('button', { name: 'Merge' }))
    })

    // Assert
    expect(mockMerge).toHaveBeenCalledWith({
      id: 'free-iphone',
      targetTagId: 'tag-ios',
    })
  })
})
