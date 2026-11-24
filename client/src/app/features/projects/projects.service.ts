import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { LookupService } from '../../core/services/lookup.service';
import { PageQuery, PagedResult } from '../../shared/models/api.models';
import { toHttpParams } from '../../shared/models/http-params';
import {
  CreateProjectRequest,
  ProjectDetails,
  ProjectStatus,
  ProjectSummary,
  UpdateProjectRequest,
} from '../../shared/models/project.models';

export interface ProjectQuery extends PageQuery {
  status?: ProjectStatus;
}

@Injectable({ providedIn: 'root' })
export class ProjectsService {
  private readonly http = inject(HttpClient);
  private readonly lookups = inject(LookupService);
  private readonly baseUrl = '/api/v1/projects';

  search(query: ProjectQuery): Observable<PagedResult<ProjectSummary>> {
    return this.http.get<PagedResult<ProjectSummary>>(this.baseUrl, { params: toHttpParams({ ...query }) });
  }

  get(id: string): Observable<ProjectDetails> {
    return this.http.get<ProjectDetails>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateProjectRequest): Observable<ProjectDetails> {
    return this.http.post<ProjectDetails>(this.baseUrl, request).pipe(tap(() => this.lookups.invalidateProjects()));
  }

  update(id: string, request: UpdateProjectRequest): Observable<ProjectDetails> {
    return this.http.put<ProjectDetails>(`${this.baseUrl}/${id}`, request).pipe(tap(() => this.lookups.invalidateProjects()));
  }

  archive(id: string, rowVersion: string): Observable<ProjectDetails> {
    return this.http
      .post<ProjectDetails>(`${this.baseUrl}/${id}/archive`, { rowVersion })
      .pipe(tap(() => this.lookups.invalidateProjects()));
  }

  restore(id: string, rowVersion: string): Observable<ProjectDetails> {
    return this.http
      .post<ProjectDetails>(`${this.baseUrl}/${id}/restore`, { rowVersion })
      .pipe(tap(() => this.lookups.invalidateProjects()));
  }
}
