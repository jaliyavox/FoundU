import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { DashboardPanel } from '@/components/layout/dashboard-panel'
import { decideAgentWorkflow, getAgentWorkflow } from './claims-api'

/** Staff-only control for a paused, non-authoritative coordinator workflow. */
export function WorkflowApprovalPanel({ claimId, workflowId }: { claimId: string; workflowId: string }) {
  const queryClient = useQueryClient()
  const workflow = useQuery({ queryKey: ['claim-agent-workflow', claimId, workflowId], queryFn: () => getAgentWorkflow(claimId, workflowId) })
  const decide = useMutation({
    mutationFn: (decision: 'approved' | 'rejected') => decideAgentWorkflow(claimId, workflowId, decision),
    onSuccess: (updated) => {
      queryClient.setQueryData(['claim-agent-workflow', claimId, workflowId], updated)
      queryClient.invalidateQueries({ queryKey: ['claim-agent-runs', claimId] })
      toast.success(updated.status === 'rejected' ? 'Workflow rejected.' : 'Workflow resumed safely.')
    },
    onError: () => toast.error('Could not update the AI workflow. The claim decision controls are still available.'),
  })

  if (workflow.isError || !workflow.data || workflow.data.status !== 'waiting_for_approval') return null
  const busy = decide.isPending
  return (
    <DashboardPanel className="flex flex-col gap-3" aria-live="polite">
      <div>
        <h2 className="font-heading text-base font-medium">Human approval required</h2>
        <p className="pt-1 text-sm text-muted-foreground">
          {workflow.data.safeActionSummary ?? 'A staff member must review this workflow before it continues.'}
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
