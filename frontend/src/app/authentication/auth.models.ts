export interface RegisterCredentials {
  name: string;
  password: string;
}

export interface LoginCredentials {
  name: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  userName: string;
}
