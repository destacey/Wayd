import { FC } from 'react'
import {
  DataItemEnhanced,
  DataGroup,
  TimelineItem,
} from 'vis-timeline/standalone'
import { TimelineOptionsTemplateFunction } from '@/src/lib/vis-timeline'

export type WaydDataItem<T = unknown, G = unknown> = DataItemEnhanced<G> & {
  itemColor?: string
  objectData?: T
}

export type WaydDataGroup<T = unknown> = DataGroup & {
  treeLevel?: number // undocumented property, that is used for styling
  objectData?: T
}

export type WaydTimelineOptions<T> = {
  maxHeight?: number
  minHeight?: number
  showCurrentTime?: boolean
  groupColumnWidth?: number
  start: Date
  end: Date
  min: Date
  max: Date
  groupOrder?: string
  template?: TimelineOptionsTemplateFunction<T>
}

export type WaydTimelineProps<
  TItem extends WaydDataItem,
  TGroup extends WaydDataGroup,
> = {
  data: TItem[]
  groups?: TGroup[]
  isLoading: boolean
  options: WaydTimelineOptions<TItem>
  rangeItemTemplate?: TimelineTemplate<TItem>
  groupTemplate?: TimelineTemplate<TGroup>
  emptyMessage?: string
  allowFullScreen?: boolean
  allowSaveAsImage?: boolean
  onMove?: (item: TimelineItem) => void
}

export type GroupTemplateProps<T extends WaydDataGroup> = {
  item: T
  fontColor: string
  foregroundColor?: string
  parentElement?: HTMLElement
}

export type ItemTemplateProps<T extends WaydDataItem> = {
  item: T
  fontColor: string
  foregroundColor?: string
}

export type TimelineTemplate<T extends WaydDataItem | WaydDataGroup> =
  T extends WaydDataItem
    ? FC<ItemTemplateProps<T>>
    : T extends WaydDataGroup
      ? FC<GroupTemplateProps<T>>
      : never
