'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { UpdatePortfolioRequest } from '@/src/services/wayd-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import {
  useGetPortfolioQuery,
  useUpdatePortfolioMutation,
} from '@/src/store/features/ppm/portfolios-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { BaseOptionType } from 'antd/es/select'
import { useEffect, useState } from 'react'

const { Item } = Form

export interface EditPortfolioFormProps {
  portfolioKey: number
  onFormComplete: () => void
  onFormCancel: () => void
}

interface UpdatePortfolioFormValues {
  name: string
  description: string
  sponsorIds: string[]
  ownerIds: string[]
  managerIds: string[]
}

const mapToRequestValues = (
  values: UpdatePortfolioFormValues,
  portfolioId: string,
): UpdatePortfolioRequest => {
  return {
    id: portfolioId,
    name: values.name,
    description: values.description,
    sponsorIds: values.sponsorIds,
    ownerIds: values.ownerIds,
    managerIds: values.managerIds,
  } as UpdatePortfolioRequest
}

const EditPortfolioForm = ({
  portfolioKey,
  onFormComplete,
  onFormCancel,
}: EditPortfolioFormProps) => {
  const [employees, setEmployees] = useState<BaseOptionType[]>([])

  const messageApi = useMessage()

  const {
    data: portfolioData,
    isLoading,
    error,
  } = useGetPortfolioQuery(portfolioKey)

  const [updatePortfolio] = useUpdatePortfolioMutation()

  const { data: employeeData } = useGetEmployeeOptionsQuery(false)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<UpdatePortfolioFormValues>({
      onSubmit: async (values: UpdatePortfolioFormValues, form) => {
        try {
          const request = mapToRequestValues(values, portfolioData!.id)
          const response = await updatePortfolio({
            request,
            cacheKey: portfolioData!.key,
          })
          if (response.error) throw response.error

          messageApi.success('Portfolio updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              (isApiError(apiError) ? apiError.detail : undefined) ??
                'An error occurred while updating the Portfolio. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the Portfolio. Please try again.',
      permission: 'Permissions.ProjectPortfolios.Update',
    })

  // Initialize form values when data is loaded
  useEffect(() => {
    if (!portfolioData) return

    form.setFieldsValue({
      name: portfolioData.name,
      description: portfolioData.description,
      sponsorIds: portfolioData.portfolioSponsors.map((p) => p.id),
      ownerIds: portfolioData.portfolioOwners.map((p) => p.id),
      managerIds: portfolioData.portfolioManagers.map((p) => p.id),
    })
  }, [portfolioData, form])

  // Build employee options including inactive assigned employees
  useEffect(() => {
    if (!employeeData) return
    const employeeOptions = [...employeeData]

    if (portfolioData) {
      const assignedEmployees = [
        ...portfolioData.portfolioSponsors,
        ...portfolioData.portfolioOwners,
        ...portfolioData.portfolioManagers,
      ]

      if (employeeOptions && employeeOptions.length > 0) {
        const missingEmployees = assignedEmployees.filter(
          (e) => !employeeOptions.find((emp) => emp.value === e.id),
        )
        if (missingEmployees.length > 0) {
          employeeOptions.push(
            ...missingEmployees.map((e) => ({
              value: e.id,
              label: `${e.name} (Inactive)`,
            })),
          )
        }
      }
    }

    setEmployees(employeeOptions.sort((a, b) => a.label.localeCompare(b.label)))
  }, [employeeData, portfolioData])

  // Query error display
  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading the Portfolio. Please try again.',
      )
    }
  }, [error, messageApi])

  return (
    <Modal
      title="Edit Portfolio"
      open={isOpen}
      width={'60vw'}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-portfolio-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <Input maxLength={128} showCount />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[
            { required: true, message: 'Description is required' },
            { max: 1024 },
          ]}
        >
          <MarkdownEditor
            value={form.getFieldValue('description')}
            onChange={(value) => form.setFieldValue('description', value || '')}
            maxLength={1024}
          />
        </Item>
        <Item name="sponsorIds" label="Sponsors">
          <EmployeeSelect
            employees={employees}
            allowMultiple={true}
            placeholder="Select Sponsors"
          />
        </Item>
        <Item name="ownerIds" label="Owners">
          <EmployeeSelect
            employees={employees}
            allowMultiple={true}
            placeholder="Select Owners"
          />
        </Item>
        <Item name="managerIds" label="Portfolio Managers">
          <EmployeeSelect
            employees={employees}
            allowMultiple={true}
            placeholder="Select Portfolio Managers"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditPortfolioForm
