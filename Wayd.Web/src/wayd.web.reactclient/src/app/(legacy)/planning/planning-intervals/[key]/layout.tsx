import { PlanningIntervalNav } from './_components'

const PlanningIntervalLayout = async (props: {
  params: Promise<{ key: string }>
  children: React.ReactNode
}) => {
  const { key } = await props.params
  const piKey = Number(key)

  return (
    <>
      <PlanningIntervalNav piKey={piKey} />
      {props.children}
    </>
  )
}

export default PlanningIntervalLayout
