import { UserRole } from './api.models';

export interface User {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
  isActive: boolean;
  lastLoginAtUtc: string | null;
  createdAtUtc: string;
}

export interface UserLookup {
  id: string;
  displayName: string;
}

export interface CreateUserRequest {
  email: string;
  displayName: string;
  role: UserRole;
  initialPassword: string;
}
