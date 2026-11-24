import { WorkItemPriority, WorkItemStatus } from './work-item.models';

export interface Dashboard {
  activeProjectCount: number;
  openWorkItemCount: number;
  assignedToMeOpenCount: number;
  overdueCount: number;
  completedLast7DaysCount: number;
  byStatus: { status: WorkItemStatus; count: number }[];
  openByPriority: { priority: WorkItemPriority; count: number }[];
}

export interface NotificationItem {
  id: string;
  message: string;
  workItemId: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface NotificationList {
  items: NotificationItem[];
  unreadCount: number;
}
