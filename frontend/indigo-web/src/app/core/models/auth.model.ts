export type Rol = 'Admin' | 'Vendedor';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  nombreCompleto: string;
}

export interface AuthSession {
  token: string;
  expiresAt: string;
  email: string;
  roles: Rol[];
}
