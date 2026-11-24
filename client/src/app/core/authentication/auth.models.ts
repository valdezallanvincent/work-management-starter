import { UserRole } from '../../shared/models/api.models';

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
}

export interface AccessTokenResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: CurrentUser;
}
