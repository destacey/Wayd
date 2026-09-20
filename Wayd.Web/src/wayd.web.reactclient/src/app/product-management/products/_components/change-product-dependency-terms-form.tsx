'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ChangeProductDependencyTermsRequest,
  DependencyStrength,
  InteractionStyle,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import { useChangeProductDependencyTermsMutation } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Form, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { useState } from 'react'
import { DependencyStrengthRadio } from './dependency-strength'
import { InteractionStyleCheckboxes } from './interaction-style'

const { Item } = Form

export interface ChangeProductDependencyTermsFormProps {
  dependency: ProductDependencyDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeProductDependencyTermsFormValues {
  strength?: DependencyStrength
  interactionStyles?: InteractionStyle[]
  changedOn?: Dayjs
}

/**
 * Changes the terms a dependency holds on — its strength, how the products talk, or both.
 *
 * Opens on the current terms rather than pre-flipping the strength, which is what the strength-only form
 * used to do: with two fields there is no single "other" value to offer, and pre-selecting one would put a
 * change in front of somebody who came to edit the other.
 *
 * A change ends the dependency and records a new one from the chosen day, so time before it is still judged
 * by the terms that held then. Writing down styles that were never recorded is not a change — see
 * {@link isOnlyRecordingStyles} below.
 */
const ChangeProductDependencyTermsForm = ({
  dependency,
  onFormComplete,
  onFormCancel,
}: ChangeProductDependencyTermsFormProps) => {
  const messageApi = useMessage()

  const [changeTerms] = useChangeProductDependencyTermsMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeProductDependencyTermsFormValues>({
      onSubmit: async (
        values: ChangeProductDependencyTermsFormValues,
        form,
      ) => {
        try {
          const request = {
            strength: values.strength,
            interactionStyles: values.interactionStyles,
            changedOn: values.changedOn?.format('YYYY-MM-DD'),
          } as ChangeProductDependencyTermsRequest

          const response = await changeTerms({
            productId: dependency.product.id,
            dependencyId: dependency.id,
            dependsOnProductId: dependency.dependsOnProduct.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Dependency terms changed.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while changing the dependency terms. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while changing the dependency terms. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  // The alert and the date field depend on what is currently chosen, tracked here rather than with
  // Form.useWatch: the form instance comes from useModalForm, and useWatch binds only to a form created by
  // the same antd module instance, which nothing here guarantees.
  const [{ strength, interactionStyles: styles }, setTerms] = useState<{
    strength?: DependencyStrength
    interactionStyles?: InteractionStyle[]
  }>({
    strength: dependency.strength,
    interactionStyles: dependency.interactionStyles ?? [],
  })

  // The current dependency ends the day before the change, so the change can fall no earlier than the day
  // after it started.
  const earliest = dayjs(dependency.startsOn).add(1, 'day')
  const startedToday = earliest.isAfter(dayjs(), 'day')

  // Filling in styles nobody had recorded changed nothing about the dependency, so the API records it in
  // place and keeps one period. A date would be meaningless, and the day-after rule does not apply.
  const hadNoStyles = !dependency.interactionStyles?.length
  const isOnlyRecordingStyles =
    hadNoStyles && strength === dependency.strength && !!styles?.length

  const blocked = startedToday && !isOnlyRecordingStyles

  return (
    <Modal
      title="Change Dependency Terms"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || blocked }}
      okText="Change"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {isOnlyRecordingStyles ? (
        <Alert
          type="info"
          showIcon
          title="This records how the products already talk"
          description="Nothing about the dependency is changing, so it keeps one period and no new dependency starts."
          style={{ marginBottom: 16 }}
        />
      ) : blocked ? (
        <Alert
          type="warning"
          showIcon
          title="This dependency started today"
          description="Its terms can change from tomorrow. If it was recorded on the wrong terms, remove it and add it again."
          style={{ marginBottom: 16 }}
        />
      ) : (
        <Alert
          type="info"
          showIcon
          title="The current dependency ends and a new one starts"
          description="The current one ends the day before the change, so time before it is still judged by the terms that held then. It stays in the list under Show ended."
          style={{ marginBottom: 16 }}
        />
      )}
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-product-dependency-terms-form"
        onValuesChange={(_, values) => setTerms(values)}
        initialValues={{
          strength: dependency.strength,
          interactionStyles: dependency.interactionStyles ?? [],
        }}
      >
        <Item
          name="strength"
          label="Strength"
          rules={[{ required: true, message: 'Choose a strength' }]}
        >
          <DependencyStrengthRadio />
        </Item>
        <Item
          name="interactionStyles"
          label="Interaction"
          extra={
            hadNoStyles
              ? 'How these products talk. Nothing has been recorded yet, so choosing here records it without starting a new dependency.'
              : 'How these products talk. Changing this starts a new dependency, the same as changing the strength.'
          }
        >
          <InteractionStyleCheckboxes />
        </Item>
        <Item
          label="First Day"
          name="changedOn"
          extra="The first day the new terms hold. Leave empty for today."
        >
          <DatePicker
            style={{ width: '100%' }}
            disabled={blocked || isOnlyRecordingStyles}
            disabledDate={(current) =>
              current.isBefore(earliest, 'day') ||
              current.isAfter(dayjs(), 'day')
            }
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default ChangeProductDependencyTermsForm
