import { getHolidayCalendarsClient } from '@/src/services/clients'
import {
  CreateHolidayCalendarRequest,
  HolidayCalendarDetailsDto,
  HolidayCalendarListDto,
  HolidayRequest,
  ObjectIdAndKey,
  PagedResponseOfActivityLogDto,
  UpdateHolidayCalendarRequest,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

const run = async <T>(call: () => Promise<T>) => {
  try {
    return { data: await call() }
  } catch (error) {
    console.error('API Error:', error)
    return { error }
  }
}

export interface HolidayMutationArg {
  calendarId: string
  request: HolidayRequest
}

export const holidayCalendarsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getHolidayCalendars: builder.query<HolidayCalendarListDto[], void>({
      queryFn: () => run(() => getHolidayCalendarsClient().getList()),
      providesTags: [{ type: QueryTags.HolidayCalendar, id: 'LIST' }],
    }),
    getHolidayCalendar: builder.query<HolidayCalendarDetailsDto, string>({
      queryFn: (idOrKey) => run(() => getHolidayCalendarsClient().get(idOrKey)),
      providesTags: (result) => [
        { type: QueryTags.HolidayCalendar, id: result?.id ?? 'DETAIL' },
      ],
    }),
    getHolidayCalendarActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: ({ idOrKey, page, pageSize }) =>
        run(() =>
          getHolidayCalendarsClient().getActivities(
            String(idOrKey),
            page,
            pageSize,
          ),
        ),
      providesTags: (result, error, { idOrKey }) => [
        { type: QueryTags.ActivityLog, id: String(idOrKey) },
      ],
    }),
    createHolidayCalendar: builder.mutation<
      ObjectIdAndKey,
      CreateHolidayCalendarRequest
    >({
      queryFn: (request) =>
        run(() => getHolidayCalendarsClient().create(request)),
      invalidatesTags: [{ type: QueryTags.HolidayCalendar, id: 'LIST' }],
    }),
    updateHolidayCalendar: builder.mutation<void, UpdateHolidayCalendarRequest>(
      {
        queryFn: (request) =>
          run(() => getHolidayCalendarsClient().update(request.id, request)),
        invalidatesTags: (result, error, arg) => [
          { type: QueryTags.HolidayCalendar, id: 'LIST' },
          { type: QueryTags.HolidayCalendar, id: arg.id },
          { type: QueryTags.ActivityLog, id: arg.id },
        ],
      },
    ),
    deleteHolidayCalendar: builder.mutation<void, string>({
      queryFn: (id) => run(() => getHolidayCalendarsClient().delete(id)),
      invalidatesTags: [{ type: QueryTags.HolidayCalendar, id: 'LIST' }],
    }),
    addHoliday: builder.mutation<string, HolidayMutationArg>({
      queryFn: ({ calendarId, request }) =>
        run(() => getHolidayCalendarsClient().addHoliday(calendarId, request)),
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.HolidayCalendar, id: 'LIST' },
        { type: QueryTags.HolidayCalendar, id: arg.calendarId },
        { type: QueryTags.ActivityLog, id: arg.calendarId },
      ],
    }),
    changeHoliday: builder.mutation<
      void,
      HolidayMutationArg & { holidayId: string }
    >({
      queryFn: ({ calendarId, holidayId, request }) =>
        run(() =>
          getHolidayCalendarsClient().changeHoliday(
            calendarId,
            holidayId,
            request,
          ),
        ),
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.HolidayCalendar, id: arg.calendarId },
        { type: QueryTags.ActivityLog, id: arg.calendarId },
      ],
    }),
    removeHoliday: builder.mutation<
      void,
      { calendarId: string; holidayId: string }
    >({
      queryFn: ({ calendarId, holidayId }) =>
        run(() =>
          getHolidayCalendarsClient().removeHoliday(calendarId, holidayId),
        ),
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.HolidayCalendar, id: 'LIST' },
        { type: QueryTags.HolidayCalendar, id: arg.calendarId },
        { type: QueryTags.ActivityLog, id: arg.calendarId },
      ],
    }),
  }),
})

export const {
  useGetHolidayCalendarsQuery,
  useGetHolidayCalendarQuery,
  useGetHolidayCalendarActivitiesQuery,
  useLazyGetHolidayCalendarActivitiesQuery,
  useCreateHolidayCalendarMutation,
  useUpdateHolidayCalendarMutation,
  useDeleteHolidayCalendarMutation,
  useAddHolidayMutation,
  useChangeHolidayMutation,
  useRemoveHolidayMutation,
} = holidayCalendarsApi
