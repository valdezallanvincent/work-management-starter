export type ProjectStatus = 'Active' | 'Archived';

export interface ProjectSummary {
  id: string;
  key: string;
  name: string;
  status: ProjectStatus;
  ownerId: string;
  ownerName: string;
  openWorkItemCount: number;
  createdAtUtc: string;
}

export interface ProjectDetails {
  id: string;
  key: string;
  name: string;
  description: string | null;
  status: ProjectStatus;
  ownerId: string;
  ownerName: string;
  archivedAtUtc: string | null;
  createdAtUtc: string;
  createdByName: string | null;
  modifiedAtUtc: string | null;
  modifiedByName: string | null;
  workItemCountsByStatus: Record<string, number>;
  rowVersion: string;
  canManage: boolean;
}

export interface ProjectLookup {
  id: string;
  key: string;
  name: string;
}

export interface CreateProjectRequest {
  key: string;
  name: string;
  description: string | null;
  ownerId: string | null;
}

export interface UpdateProjectRequest {
  name: string;
  description: string | null;
  ownerId: string;
  rowVersion: string;
}
