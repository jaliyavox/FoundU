export type WorkflowApprovalView = 'loading' | 'error' | 'pending' | 'rejected' | 'completed' | 'hidden'

export function workflowApprovalView(status: string | undefined, isPending: boolean, isError: boolean): WorkflowApprovalView {
  if (isPending) return 'loading'
  if (isError) return 'error'
  if (status === 'waiting_for_approval') return 'pending'
  if (status === 'rejected') return 'rejected'
  if (status === 'completed') return 'completed'
  return 'hidden'
}
