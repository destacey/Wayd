import {
  JsonPatchOperation,
  JsonPatchOperationType,
} from '@/src/services/wayd-api'

export type ProjectTaskUpdate = {
  name?: string
  description?: string
  statusId?: number
  priorityId?: number
  assigneeIds?: string[]
  progress?: number
  plannedStart?: string | null
  plannedEnd?: string | null
  plannedDate?: string | null
  estimatedEffortHours?: number | null
}

// Note: ASP.NET Core JsonPatch expects PascalCase paths matching the C# property names
export type ProjectStageUpdate = {
  description?: string
  statusId?: number
  progress?: number
  plannedStart?: string | null
  plannedEnd?: string | null
  assigneeIds?: string[]
}

// Note: ASP.NET Core JsonPatch expects PascalCase paths matching the C# property names
export const buildProjectStagePatchOperations = (
  updates: ProjectStageUpdate,
): JsonPatchOperation[] => {
  const patchOperations: JsonPatchOperation[] = []

  if (updates.description !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/Description',
      value: updates.description,
    })
  }
  if (updates.statusId !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/Status',
      value: updates.statusId,
    })
  }
  if (updates.progress !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/Progress',
      value: updates.progress,
    })
  }
  if (updates.plannedStart !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/PlannedStart',
      value: updates.plannedStart,
    })
  }
  if (updates.plannedEnd !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/PlannedEnd',
      value: updates.plannedEnd,
    })
  }
  if (updates.assigneeIds !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/AssigneeIds',
      value: updates.assigneeIds,
    })
  }

  return patchOperations
}

export const buildProjectTaskPatchOperations = (
  updates: ProjectTaskUpdate,
): JsonPatchOperation[] => {
  const patchOperations: JsonPatchOperation[] = []

  if (updates.name !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/Name',
      value: updates.name,
    })
  }
  if (updates.description !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/Description',
      value: updates.description,
    })
  }
  if (updates.statusId !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/StatusId',
      value: updates.statusId,
    })
  }
  if (updates.priorityId !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/PriorityId',
      value: updates.priorityId,
    })
  }
  if (updates.assigneeIds !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/AssigneeIds',
      value: updates.assigneeIds,
    })
  }
  if (updates.progress !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/Progress',
      value: updates.progress,
    })
  }
  if (updates.plannedStart !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/PlannedStart',
      value: updates.plannedStart,
    })
  }
  if (updates.plannedEnd !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/PlannedEnd',
      value: updates.plannedEnd,
    })
  }
  if (updates.plannedDate !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/PlannedDate',
      value: updates.plannedDate,
    })
  }
  if (updates.estimatedEffortHours !== undefined) {
    patchOperations.push({
      op: JsonPatchOperationType.Replace,
      path: '/EstimatedEffortHours',
      value: updates.estimatedEffortHours,
    })
  }

  return patchOperations
}
