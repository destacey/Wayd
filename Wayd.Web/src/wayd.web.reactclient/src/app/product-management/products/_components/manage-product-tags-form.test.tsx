import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ProductDto, ProductTagCategoryDto } from '@/src/services/wayd-api'
import ManageProductTagsForm from './manage-product-tags-form'

const tagOption = (id: string, name: string, order: number) => ({
  id,
  name,
  order,
  isActive: true,
  productCount: 0,
})

const CATEGORIES: ProductTagCategoryDto[] = [
  {
    id: 'cat-platform',
    key: 1,
    name: 'Platform',
    allowsMany: true,
    order: 1,
    isActive: true,
    isSystem: false,
    tags: [
      tagOption('tag-ios', 'ios', 1),
      tagOption('tag-android', 'android', 2),
      tagOption('tag-web', 'web', 3),
    ],
  },
  {
    id: 'cat-tier',
    key: 2,
    name: 'Tier',
    allowsMany: false,
    order: 2,
    isActive: true,
    isSystem: false,
    tags: [
      tagOption('tag-gold', 'gold', 1),
      tagOption('tag-silver', 'silver', 2),
    ],
  },
]

const product = {
  id: 'product-1',
  key: 7,
  name: 'Storefront Web',
  type: { id: 'type-1', key: 1, name: 'Application' },
  status: { id: 'status-1', name: 'Concept', category: 1, alias: 0 },
  isReleasable: true,
  tags: [
    {
      tagId: 'tag-ios',
      tagName: 'ios',
      categoryId: 'cat-platform',
      categoryName: 'Platform',
    },
    { tagId: 'free-wave', tagName: 'migration wave' },
  ],
} as unknown as ProductDto

const FREE_TAGS = [
  {
    id: 'free-wave',
    name: 'migration wave',
    isFree: true,
    isActive: true,
    productCount: 3,
  },
]

const mockTagProduct = jest.fn()
const mockTagProductByName = jest.fn()
const mockUntagProduct = jest.fn()
let mockAllowsFreeTags = false

// Not spread from the real barrel: it re-exports store-bound hooks, and pulling those
// in initialises a store this form never touches. The form instance is real, though —
// antd's Form binds to it, and a stub object breaks on render. handleOk runs the real
// submit so the diffing under test is exercised end to end.
jest.mock('@/src/hooks', () => {
  const { Form: AntForm } = jest.requireActual('antd')
  return {
    useModalForm: ({
      onSubmit,
      onComplete,
    }: {
      onSubmit: (values: unknown, form: unknown) => Promise<boolean>
      onComplete: () => void
    }) => {
      const [form] = AntForm.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          if (await onSubmit(form.getFieldsValue(), form)) onComplete()
        },
        handleCancel: jest.fn(),
      }
    },
  }
})

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock(
  '@/src/store/features/product-management/product-tag-categories-api',
  () => ({
    useGetProductTagCategoriesQuery: () => ({
      data: CATEGORIES,
      isLoading: false,
    }),
  }),
)

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useTagProductMutation: () => [mockTagProduct],
  useTagProductByNameMutation: () => [mockTagProductByName],
  useUntagProductMutation: () => [mockUntagProduct],
}))

jest.mock('@/src/store/features/admin/system-settings-api', () => ({
  useGetProductTagSettingsQuery: () => ({
    data: { allowsFreeTags: mockAllowsFreeTags },
    isLoading: false,
  }),
}))

jest.mock('@/src/store/features/product-management/product-tags-api', () => ({
  useSearchProductTagsQuery: (_: unknown, options: { skip: boolean }) => ({
    data: options.skip ? undefined : FREE_TAGS,
    isLoading: false,
  }),
}))

const renderForm = async () =>
  await act(async () =>
    render(
      <ManageProductTagsForm
        product={product}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    ),
  )

const fieldFor = (categoryLabel: string) =>
  screen.getByText(categoryLabel).closest('.ant-form-item') as HTMLElement

/** Opens a category's dropdown and clicks an option by name. */
const selectOption = async (categoryLabel: string, optionName: string) => {
  const user = userEvent.setup()
  const field = fieldFor(categoryLabel)

  await act(async () => {
    await user.click(
      field.querySelector('input[role="combobox"]') as HTMLElement,
    )
  })

  // antd portals each dropdown out of the form, every opened field keeps its own
  // mounted, and a selected tag renders a titled element of its own — so neither a
  // global title lookup nor the first dropdown finds the right node. aria-controls
  // names this field's listbox.
  const listboxId = field
    .querySelector('input[role="combobox"]')!
    .getAttribute('aria-controls')!

  const dropdown = await waitFor(() => {
    const el = document
      .getElementById(listboxId)
      ?.closest('.ant-select-dropdown') as HTMLElement | null
    if (!el) throw new Error(`no dropdown for ${categoryLabel}`)
    return el
  })

  await act(async () => {
    await user.click(await within(dropdown).findByTitle(optionName))
  })
}

const save = async () => {
  const user = userEvent.setup()
  await act(async () => {
    await user.click(screen.getByRole('button', { name: 'Save' }))
  })
}

describe('ManageProductTagsForm', () => {
  beforeEach(() => {
    mockTagProduct.mockReset().mockResolvedValue({ data: undefined })
    mockTagProductByName.mockReset().mockResolvedValue({ data: 'free-new' })
    mockUntagProduct.mockReset().mockResolvedValue({ data: undefined })
    mockAllowsFreeTags = false
  })

  it('renders a select for every active category', async () => {
    // Arrange / Act
    await renderForm()

    // Assert
    expect(screen.getByText('Platform')).toBeInTheDocument()
    expect(screen.getByText('Tier')).toBeInTheDocument()
    expect(document.querySelectorAll('.ant-select')).toHaveLength(
      CATEGORIES.length,
    )
  })

  it('pre-populates the tags the product already carries', async () => {
    // Arrange / Act
    await renderForm()

    // Assert
    const selected = fieldFor('Platform').querySelectorAll(
      '.ant-select-selection-item',
    )
    expect(selected).toHaveLength(1)
    expect(selected[0]).toHaveTextContent('ios')
  })

  it('lets an allowsMany category hold several tags at once', async () => {
    // Arrange
    await renderForm()

    // Act
    await selectOption('Platform', 'android')

    // Assert
    expect(
      fieldFor('Platform').querySelectorAll('.ant-select-selection-item'),
    ).toHaveLength(2)
  })

  it('holds a single tag on a category that does not allow many', async () => {
    // allowsMany decides the field's mode, and the mode is what stops a second pick
    // from stacking beside the first. Asserting the mode rather than a second click
    // keeps this on the component's own decision.
    // Arrange
    await renderForm()

    // Act
    await selectOption('Tier', 'gold')

    // Assert
    // A single Select renders its value on the content element; only a multiple one
    // renders removable selection items.
    expect(fieldFor('Tier').querySelector('.ant-select-single')).not.toBeNull()
    expect(fieldFor('Tier').querySelector('.ant-select-multiple')).toBeNull()
    expect(
      fieldFor('Tier').querySelector('.ant-select-content-has-value'),
    ).toHaveAttribute('title', 'gold')
    expect(
      fieldFor('Platform').querySelector('.ant-select-multiple'),
    ).not.toBeNull()
  })

  it('sends only the changed tags on submit', async () => {
    // Arrange — the product already carries ios, which is left untouched.
    await renderForm()

    // Act
    await selectOption('Platform', 'android')
    await selectOption('Tier', 'gold')
    await save()

    // Assert
    expect(mockTagProduct).toHaveBeenCalledTimes(2)
    expect(mockTagProduct).toHaveBeenCalledWith({
      id: 'product-1',
      tagId: 'tag-android',
    })
    expect(mockTagProduct).toHaveBeenCalledWith({
      id: 'product-1',
      tagId: 'tag-gold',
    })
    expect(mockUntagProduct).not.toHaveBeenCalled()
  })

  it('untags a tag the user cleared', async () => {
    // Arrange
    await renderForm()

    // Act — deselecting the carried tag from the multiple-mode field
    await selectOption('Platform', 'ios')
    await save()

    // Assert
    expect(mockUntagProduct).toHaveBeenCalledTimes(1)
    expect(mockUntagProduct).toHaveBeenCalledWith({
      id: 'product-1',
      tagId: 'tag-ios',
    })
    expect(mockTagProduct).not.toHaveBeenCalled()
  })

  it('sends nothing when no tag changed', async () => {
    // Arrange / Act
    await renderForm()
    await save()

    // Assert
    expect(mockTagProduct).not.toHaveBeenCalled()
    expect(mockUntagProduct).not.toHaveBeenCalled()
  })

  it('offers no free tag field while free tags are off, and keeps the ones carried', async () => {
    // Arrange / Act
    await renderForm()
    await save()

    // Assert
    expect(screen.queryByText('Free tags')).not.toBeInTheDocument()
    expect(mockUntagProduct).not.toHaveBeenCalled()
  })

  it('applies a typed free tag by name, after the other changes', async () => {
    // Arrange
    mockAllowsFreeTags = true
    await renderForm()
    const input = fieldFor('Free tags').querySelector(
      'input[role="combobox"]',
    ) as HTMLInputElement

    // Act — the field's comma separator commits each entry, which jsdom drives reliably
    await selectOption('Tier', 'gold')
    await act(async () => {
      await userEvent.type(input, 'follow up,')
    })
    await save()

    // Assert
    expect(mockTagProduct).toHaveBeenCalledWith({
      id: 'product-1',
      tagId: 'tag-gold',
    })
    expect(mockTagProductByName).toHaveBeenCalledWith({
      id: 'product-1',
      name: 'follow up',
    })
    expect(mockTagProduct.mock.invocationCallOrder[0]).toBeLessThan(
      mockTagProductByName.mock.invocationCallOrder[0],
    )
    expect(mockUntagProduct).not.toHaveBeenCalled()
  })

  it('untags a free tag the user cleared', async () => {
    // Arrange
    mockAllowsFreeTags = true
    await renderForm()
    const removeButton = fieldFor('Free tags').querySelector(
      '.ant-select-selection-item-remove',
    ) as HTMLElement

    // Act
    await act(async () => {
      await userEvent.click(removeButton)
    })
    await save()

    // Assert
    expect(mockUntagProduct).toHaveBeenCalledWith({
      id: 'product-1',
      tagId: 'free-wave',
    })
    expect(mockTagProductByName).not.toHaveBeenCalled()
  })
})
