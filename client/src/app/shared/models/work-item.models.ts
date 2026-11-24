export const WORK_ITEM_STATUSES = ['Open', 'InProgress', 'InReview', 'Completed', 'Cancelled'] as const;
export type WorkItemStatus = (typeof WORK_ITEM_STATUSES)[number];

export const WORK_ITEM_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const;
export type WorkItemPriority = (typeof WORK_ITEM_PRIORITIES)[number];

export interface WorkItemSummary {
  id: string;
  projectId: string;
  projectKey: string;
  title: string;
  status: WorkItemStatus;
  priority: WorkItemPriority;
  assigneeId: string | null;
  assigneeName: string | null;
  dueDate: string | null;
  createdAtUtc: string;
  modifiedAtUtc: string | null;
}

export interface WorkItemDetails {
  id: string;
  projectId: string;
  projectKey: string;
  projectName: string;
  projectOwnerId: string;
  projectIsArchived: boolean;
  title: string;
  description: string | null;
  status: WorkItemStatus;
  priority: WorkItemPriority;
  reporterId: string;
  reporterName: string;
  assigneeId: string | null;
  assigneeName: string | null;
  dueDate: string | null;
  completedAtUtc: string | null;
  createdAtUtc: string;
  modifiedAtUtc: string | null;
  modifiedByName: string | null;
  allowedTransitions: WorkItemStatus[];
  rowVersion: string;
  /** Computed by the API from the same rules it enforces. */
  permissions: WorkItemPermissions;
}

export interface WorkItemPermissions {
  canEdit: boolean;
  canChangeStatus: boolean;
  canAssignOthers: boolean;
  canSelfAssign: boolean;
}

export interface WorkItemComment {
  id: string;
  workItemId: string;
  authorId: string;
  authorName: string;
  body: string;
  isEdited: boolean;
  createdAtUtc: string;
  modifiedAtUtc: string | null;
}

export interface WorkItemFilters {
  projectId?: string;
  status?: WorkItemStatus[];
  priority?: WorkItemPriority;
  assignedToMe?: boolean;
  overdue?: boolean;
}

export interface CreateWorkItemRequest {
  projectId: string;
  title: string;
  description: string | null;
  priority: WorkItemPriority;
  assigneeId: string | null;
  dueDate: string | null;
}

export interface UpdateWorkItemRequest {
  title: string;
  description: string | null;
  priority: WorkItemPriority;
  dueDate: string | null;
  rowVersion: string;
}
