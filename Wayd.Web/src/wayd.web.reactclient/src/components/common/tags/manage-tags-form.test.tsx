import { render, screen } from '@testing-library/react'
import ManageTagsForm from './manage-tags-form'
import { TagAssignment, TagCategory } from './types'

// Not spread from the real barrel: it re-exports store-bound hooks, and pulling
// those in initialises a store this component never touches. The form instance is
// real, though — antd's Form binds to it, and a stub breaks on render.
jest.mock('@/src/hooks', () => {
  const { Form } = jest.requireActual('antd')
  return {
    useModalForm: ({
      onSubmit,
    }: {
      onSubmit: (values: Record<string, unknown>) => Promise<boolean>
    }) => {
      const [form] = Form.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => await onSubmit(form.getFieldsValue()),
        handleCancel: jest.fn(),
      }
    },
  }
})

const platform: TagCategory = {
  id: 'cat-platform',
  name: 'Platform',
  allowsMany: true,
  tags: [
    { id: 'tag-ios', name: 'ios', isActive: true },
    { id: 'tag-android', name: 'android', isActive: true },
    { id: 'tag-blackberry', name: 'blackberry', isActive: false },
  ],
}

const tier: TagCategory = {
  id: 'cat-tier',
  name: 'Tier',
  allowsMany: false,
  tags: [
    { id: 'tag-gold', name: 'gold', isActive: true },
    { id: 'tag-silver', name: 'silver', isActive: true },
  ],
}

const carried = (
  tagId: string,
  tagName: string,
  categoryId: string,
  categoryName: string,
): TagAssignment => ({ tagId, tagName, categoryId, categoryName })

const renderForm = (
  overrides: Partial<React.ComponentProps<typeof ManageTagsForm>> = {},
) => {
  const onSave = jest.fn().mockResolvedValue(true)
  const view = render(
    <ManageTagsForm
      categories={[platform, tier]}
      tags={[]}
      onSave={onSave}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
      permission="Permissions.Test.Update"
      {...overrides}
    />,
  )
  return { onSave, ...view }
}

describe('ManageTagsForm', () => {
  it('offers a field per category it is given', () => {
    // Arrange / Act
    renderForm()

    // Assert
    expect(screen.getByText('Platform')).toBeInTheDocument()
    expect(screen.getByText('Tier')).toBeInTheDocument()
  })

  it('lets a many-valued axis hold several and a single-valued one hold one', () => {
    // Arrange / Act
    renderForm()

    // Assert
    // Queried off document: the modal renders in a portal, outside container.
    expect(document.querySelectorAll('.ant-select-multiple')).toHaveLength(1)
    expect(document.querySelectorAll('.ant-select-single')).toHaveLength(1)
  })

  it('keeps an inactive tag the record already carries', () => {
    // Offering only active tags would hide it while it was still attached, and
    // the next save would then remove it unasked.
    // Arrange / Act
    renderForm({
      tags: [
        carried('tag-blackberry', 'blackberry', 'cat-platform', 'Platform'),
      ],
    })

    // Assert
    expect(screen.getByText('blackberry (inactive)')).toBeInTheDocument()
  })

  it('holds no knowledge of what is being tagged', () => {
    // The whole point of the seam: an area with entirely different records uses
    // the same component by passing its own axes.
    // Arrange / Act
    renderForm({
      categories: [
        {
          id: 'cat-risk',
          name: 'Risk Level',
          allowsMany: false,
          tags: [{ id: 'tag-high', name: 'high', isActive: true }],
        },
      ],
      tags: [],
    })

    // Assert
    expect(screen.getByText('Risk Level')).toBeInTheDocument()
  })
})
