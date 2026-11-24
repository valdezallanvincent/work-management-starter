import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PageQuery, PagedResult } from '../../shared/models/api.models';
import { toHttpParams } from '../../shared/models/http-params';
import {
  CreateWorkItemRequest,
  UpdateWorkItemRequest,
  WorkItemComment,
  WorkItemDetails,
  WorkItemFilters,
  WorkItemPriority,
  WorkItemStatus,
  WorkItemSummary,
} from '../../shared/models/work-item.models';

export type WorkItemQuery = PageQuery & WorkItemFilters;

@Injectable({ providedIn: 'root' })
export class WorkItemsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/work-items';

  search(query: WorkItemQuery): Observable<PagedResult<WorkItemSummary>> {
    return this.http.get<PagedResult<WorkItemSummary>>(this.baseUrl, { params: toHttpParams({ ...query }) });
  }

  get(id: string): Observable<WorkItemDetails> {
    return this.http.get<WorkItemDetails>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateWorkItemRequest): Observable<WorkItemDetails> {
    return this.http.post<WorkItemDetails>(this.baseUrl, request);
  }

  update(id: string, request: UpdateWorkItemRequest): Observable<WorkItemDetails> {
    return this.http.put<WorkItemDetails>(`${this.baseUrl}/${id}`, request);
  }

  changeStatus(item: WorkItemDetails, status: WorkItemStatus): Observable<WorkItemDetails> {
    return this.http.put<WorkItemDetails>(`${this.baseUrl}/${item.id}/status`, { status, rowVersion: item.rowVersion });
  }

  changePriority(item: WorkItemDetails, priority: WorkItemPriority): Observable<WorkItemDetails> {
    return this.http.put<WorkItemDetails>(`${this.baseUrl}/${item.id}/priority`, { priority, rowVersion: item.rowVersion });
  }

  assign(item: WorkItemDetails, assigneeId: string | null): Observable<WorkItemDetails> {
    return this.http.put<WorkItemDetails>(`${this.baseUrl}/${item.id}/assignee`, { assigneeId, rowVersion: item.rowVersion });
  }

  comments(id: string): Observable<WorkItemComment[]> {
    return this.http.get<WorkItemComment[]>(`${this.baseUrl}/${id}/comments`);
  }

  addComment(id: string, body: string): Observable<WorkItemComment> {
    return this.http.post<WorkItemComment>(`${this.baseUrl}/${id}/comments`, { body });
  }

  editComment(id: string, commentId: string, body: string): Observable<WorkItemComment> {
    return this.http.put<WorkItemComment>(`${this.baseUrl}/${id}/comments/${commentId}`, { body });
  }

  deleteComment(id: string, commentId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}/comments/${commentId}`);
  }
}
