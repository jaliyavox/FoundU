import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel } from '@/components/layout/dashboard-panel'
import { decideAgentWorkflow, getAgentWorkflow } from './claims-api'
import { workflowApprovalView } from './workflow-approval-state'

/** Staff-only control for a paused, non-authoritative coordinator workflow. */
export function WorkflowApprovalPanel({ claimId, workflowId }: { claimId: string; workflowId: string }) {
  const queryClient = useQueryClient()
  const workflow = useQuery({ queryKey: ['claim-agent-workflow', claimId, workflowId], queryFn: () => getAgentWorkflow(claimId, workflowId) })
  const decide = useMutation({
    mutationFn: (decision: 'approved' | 'rejected') => decideAgentWorkflow(claimId, workflowId, decision),
    onSuccess: (updated) => {
      queryClient.setQueryData(['claim-agent-workflow', claimId, workflowId], updated)
      queryClient.invalidateQueries({ queryKey: ['claim', claimId] })
      queryClient.invalidateQueries({ queryKey: ['claim-queue'] })
      queryClient.invalidateQueries({ queryKey: ['claim-agent-runs', claimId] })
      toast.success(updated.status === 'rejected' ? 'Workflow rejected.' : 'Workflow resumed safely.')
    },
    onError: () => toast.error('Could not update the AI workflow. The claim decision controls are still available.'),
  })

  const view = workflowApprovalView(workflow.data?.status, workflow.isPending, workflow.isError)
  if (view === 'hidden') return null
  if (view === 'loading') {
    return <DashboardPanel role="status" className="text-sm text-muted-foreground">Checking workflow approval status…</DashboardPanel>
  }
  if (view === 'error') {
    return (
      <DashboardPanel role="alert" className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">Could not load workflow approval status. Claim decision controls remain available.</p>
        <Button variant="outline" onClick={() => workflow.refetch()}>Try again</Button>
      </DashboardPanel>
    )
  }
  if (view === 'rejected' || view === 'completed') {
    return (
      <DashboardPanel role="status" className="text-sm text-muted-foreground">
        {view === 'rejected' ? 'This AI workflow was rejected and will not resume.' : 'This AI workflow completed safely.'}
      </DashboardPanel>
    )
  }
  const busy = decide.isPending
  const safeActionSummary = workflow.data?.safeActionSummary ?? 'A staff member must review this workflow before it continues.'
  return (
    <DashboardPanel className="flex flex-col gap-3" aria-live="polite">
      <div>
        <h2 className="font-heading text-base font-medium">Human approval required</h2>
        <p className="pt-1 text-sm text-muted-foreground">
          {safeActionSummary}
        </p>
      </div>
      <p className="text-xs text-muted-foreground">This continues AI coordination only. It does not approve or reject the claim.</p>
      <div className="flex flex-wrap gap-2">
        <Button onClick={() => decide.mutate('approved')} disabled={busy} aria-label="Approve AI workflow">
          {busy && <Loader2Icon className="animate-spin" aria-hidden="true" />}
          Approve workflow
        </Button>
        <Button variant="destructive" onClick={() => decide.mutate('rejected')} disabled={busy} aria-label="Reject AI workflow">
          Reject workflow
        </Button>
      </div>
    </DashboardPanel>
  )
}
