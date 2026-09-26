import { describe, expect, it } from 'vitest'
import { workflowApprovalView } from './workflow-approval-state'

describe('workflowApprovalView', () => {
  it('keeps approval controls limited to a pending workflow', () => {
    expect(workflowApprovalView('waiting_for_approval', false, false)).toBe('pending')
    expect(workflowApprovalView('completed', false, false)).toBe('completed')
    expect(workflowApprovalView('rejected', false, false)).toBe('rejected')
  })

  it('exposes safe loading and retryable error states rather than stale controls', () => {
    expect(workflowApprovalView(undefined, true, false)).toBe('loading')
    expect(workflowApprovalView(undefined, false, true)).toBe('error')
    expect(workflowApprovalView('approved', false, false)).toBe('hidden')
  })
})
